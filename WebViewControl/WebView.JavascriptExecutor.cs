using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;

namespace WebViewControl {

    partial class WebView {

        [DataContract]
        internal class JsError {
            [DataMember(Name = "stack")]
            public string Stack;
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "message")]
            public string Message;
        }

        internal class ScriptTask {

            public ScriptTask(string script, TimeSpan? timeout = default(TimeSpan?), bool awaitable = false) {
                Script = script;
                if (awaitable) {
                    WaitHandle = new ManualResetEvent(false);
                }
                Timeout = timeout;
            }

            public string Script { get; private set; }

            public ManualResetEvent WaitHandle { get; private set; }

            public JavascriptResponse Result { get; set; }

            public Exception Exception { get; set; }

            public TimeSpan? Timeout { get; set; }
        }

        internal class JavascriptExecutor : IDisposable {

            private readonly WebView OwnerWebView;
            private readonly BlockingCollection<ScriptTask> pendingScripts = new BlockingCollection<ScriptTask>();
            private readonly CancellationTokenSource flushTaskCancelationToken = new CancellationTokenSource();
            private readonly ManualResetEvent stoppedFlushHandle = new ManualResetEvent(false);
            private bool flushRunning;

            public JavascriptExecutor(WebView ownerWebView) {
                OwnerWebView = ownerWebView;
                OwnerWebView.JavascriptContextCreated += OnJavascriptContextCreated;
                OwnerWebView.RenderProcessCrashed += StopFlush;
            }

            private void OnJavascriptContextCreated() {
                OwnerWebView.JavascriptContextCreated -= OnJavascriptContextCreated;
                Task.Factory.StartNew(FlushScripts, flushTaskCancelationToken.Token);
            }

            private void StopFlush() {
                flushTaskCancelationToken.Cancel();
                if (flushRunning) {
                    stoppedFlushHandle.WaitOne();
                }
                flushTaskCancelationToken.Dispose();
            }

            private ScriptTask QueueScript(string script, TimeSpan? timeout = default(TimeSpan?), bool awaitable = false) {
                var scriptTask = new ScriptTask(script, timeout, awaitable);
                pendingScripts.Add(scriptTask);
                return scriptTask;
            }

            private void FlushScripts() {
                OwnerWebView.WithErrorHandling(() => {
                    try {
                        flushRunning = true;
                        while (!flushTaskCancelationToken.IsCancellationRequested) {
                            InnerFlushScripts();
                        }
                    } catch (OperationCanceledException) {
                        // stop
                    } finally {
                        flushRunning = false;
                        stoppedFlushHandle.Set();
                    }
                });
            }

            private void InnerFlushScripts() {
                ScriptTask scriptToEvaluate = null;
                var scriptsToExecute = new List<ScriptTask>();

                do {
                    var scriptTask = pendingScripts.Take(flushTaskCancelationToken.Token);
                    if (scriptTask.WaitHandle == null) {
                        scriptsToExecute.Add(scriptTask);
                    } else { 
                        scriptToEvaluate = scriptTask;
                        break; // this script result needs to be handled separately
                    }
                } while (pendingScripts.Count > 0);

                if (scriptsToExecute.Count > 0) {
                    OwnerWebView.chromium.EvaluateScriptAsync(string.Join(";" + Environment.NewLine, scriptsToExecute.Select(s => s.Script))).Wait(flushTaskCancelationToken.Token);
                }

                if (scriptToEvaluate != null) {
                    // evaluate and signal waiting thread
                    Task<JavascriptResponse> task = null;
                    try {
                        task = OwnerWebView.chromium.EvaluateScriptAsync(scriptToEvaluate.Script, scriptToEvaluate.Timeout ?? OwnerWebView.DefaultScriptsExecutionTimeout);
                        task.Wait(flushTaskCancelationToken.Token);
                        scriptToEvaluate.Result = task.Result;
                    } catch(Exception e) {
                        if (task == null || !task.IsCanceled) {
                            // not cancelled (if cancelled -> timeout)
                            scriptToEvaluate.Exception = e;
                        }
                    } finally {
                        scriptToEvaluate.WaitHandle.Set();
                    }
                }
            }

            public T EvaluateScript<T>(string script, TimeSpan? timeout = default(TimeSpan?)) {
                if (!flushRunning) {
                    return default(T);
                }

                var scriptWithErrorHandling = "try {" + script + Environment.NewLine + "} catch (e) { throw JSON.stringify({ stack: e.stack, message: e.message, name: e.name }) }";

                var scriptTask = QueueScript(scriptWithErrorHandling, timeout, true);
                scriptTask.WaitHandle.WaitOne();

                if (scriptTask.Exception != null) {
                    throw scriptTask.Exception;
                }

                if (scriptTask.Result == null) {
                    throw new JavascriptException("Timeout", (timeout.HasValue ? $"More than {timeout.Value.TotalMilliseconds}ms elapsed" : "Timeout ocurred") + $" evaluating the script: '{script}'", new string[0]);
                }

                if (scriptTask.Result.Success) {
                    return GetResult<T>(scriptTask.Result.Result);
                }

                // try parse js exception
                var jsErrorJSON = scriptTask.Result.Message;
                jsErrorJSON = jsErrorJSON.Substring(Math.Max(0, jsErrorJSON.IndexOf("{")));
                jsErrorJSON = jsErrorJSON.Substring(0, jsErrorJSON.LastIndexOf("}") + 1);

                if (!string.IsNullOrEmpty(jsErrorJSON)) {
                    JsError jsError = null;
                    try {
                        jsError = DeserializeJSON<JsError>(jsErrorJSON);
                    } catch {
                        // ignore will throw error at the end   
                    }
                    if (jsError != null) {
                        jsError.Name = jsError.Name ?? "";
                        jsError.Message = jsError.Message ?? "";
                        jsError.Stack = jsError.Stack ?? "";
                        var jsStack = jsError.Stack.Substring(Math.Min(jsError.Stack.Length, (jsError.Name + ": " + jsError.Message).Length)).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        jsStack = jsStack.Select(l => l.Substring("    at ".Length)).ToArray();

                        throw new JavascriptException(jsError.Name, jsError.Message, jsStack);
                    }
                }

                throw new JavascriptException("Javascript Error", scriptTask.Result.Message, new string[0]);
            }

            public T EvaluateScriptFunction<T>(string functionName, params string[] args) {
                return EvaluateScript<T>(MakeScript(functionName, args));
            }

            public void ExecuteScriptFunction(string functionName, params string[] args) {
                QueueScript(MakeScript(functionName, args));
            }

            public void ExecuteScript(string script) {
                QueueScript(script);
            }

            public void BindVariable(string variableName, object objectToBind) {
                if (objectToBind == null) {
                    throw new InvalidOperationException("objectToBind");
                }
                OwnerWebView.RegisterJavascriptObject(variableName, objectToBind);
            }

            private T GetResult<T>(object result) {
                var targetType = typeof(T);
                if (IsBasicType(targetType)) {
                    if (result == null) {
                        return default(T);
                    }
                    return (T)result;
                }
                if (result == null && targetType.IsArray) {
                    // return empty arrays when value is null and return type is array
                    return (T)(object)Array.CreateInstance(targetType.GetElementType(), 0);
                }
                return (T)OwnerWebView.binder.Bind(result, targetType);
            }

            public void Dispose() {
                StopFlush();
            }

            private static bool IsBasicType(Type type) {
                return type.IsPrimitive || type.IsEnum || type == typeof(string);
            }

            private static string MakeScript(string functionName, string[] args) {
                var argsSerialized = args.Select(a => a == null ? "null" : a);
                return functionName + "(" + string.Join(",", argsSerialized) + ")";
            }

            private static T DeserializeJSON<T>(string json) {
                var serializer = new DataContractJsonSerializer(typeof(JsError));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                    return (T)serializer.ReadObject(stream);
                }
            }
        }
    }
}