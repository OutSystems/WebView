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

            private const string InternalException = "|WebViewInternalException";

            private readonly WebView OwnerWebView;
            private readonly BlockingCollection<ScriptTask> pendingScripts = new BlockingCollection<ScriptTask>();
            private readonly CancellationTokenSource flushTaskCancelationToken = new CancellationTokenSource();
            private readonly ManualResetEvent stoppedFlushHandle = new ManualResetEvent(false);
            private volatile bool flushRunning;

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
                OwnerWebView.ExecuteWithAsyncErrorHandling(() => {
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
                    var task = OwnerWebView.chromium.EvaluateScriptAsync(
                        WrapScriptWithErrorHandling(string.Join(";" + Environment.NewLine, scriptsToExecute.Select(s => s.Script))), 
                        OwnerWebView.DefaultScriptsExecutionTimeout);
                    task.Wait(flushTaskCancelationToken.Token);
                    var response = task.Result;
                    if (!response.Success) {
                        OwnerWebView.ExecuteWithAsyncErrorHandling(() => throw ParseResponseException(response));
                    }
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
                var scriptWithErrorHandling = WrapScriptWithErrorHandling(script);

                var scriptTask = QueueScript(scriptWithErrorHandling, timeout, true);
                if (!flushRunning) {
                    scriptTask.WaitHandle.WaitOne((timeout ?? TimeSpan.FromSeconds(5))); // wait with timeout if flush is not running yet to avoid hanging forever
                } else {
                    scriptTask.WaitHandle.WaitOne();
                }

                if (scriptTask.Exception != null) {
                    throw scriptTask.Exception;
                }

                if (scriptTask.Result == null) {
                    if (!flushRunning) {
                        throw new JavascriptException("Timeout", "Javascript engine is not initialized", new string[0]);
                    }
                    throw new JavascriptException("Timeout", (timeout.HasValue ? $"More than {timeout.Value.TotalMilliseconds}ms elapsed" : "Timeout ocurred") + $" evaluating the script: '{script}'", new string[0]);
                }

                if (scriptTask.Result.Success) {
                    return GetResult<T>(scriptTask.Result.Result);
                }
                
                throw ParseResponseException(scriptTask.Result);
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

            private static string WrapScriptWithErrorHandling(string script) {
                return "try {" + script + Environment.NewLine + "} catch (e) { throw JSON.stringify({ stack: e.stack, message: e.message, name: e.name }) + '" + InternalException + "' }";
            }

            private static T DeserializeJSON<T>(string json) {
                var serializer = new DataContractJsonSerializer(typeof(JsError));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                    return (T)serializer.ReadObject(stream);
                }
            }

            private static Exception ParseResponseException(JavascriptResponse response) {
                var jsErrorJSON = response.Message;

                // try parse js exception
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
                        jsStack = jsStack.Select(l => l.Substring(1)).ToArray(); // "    at" -> "   at"

                        return new JavascriptException(jsError.Name, jsError.Message, jsStack);
                    }
                }

                return new JavascriptException("Javascript Error", response.Message, new string[0]);
            }

            internal static bool IsInternalException(string exceptionMessage) {
                return exceptionMessage.EndsWith(InternalException);
            }
        }
    }
}