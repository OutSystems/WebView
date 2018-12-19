using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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

            private static readonly Regex StackFrameRegex = new Regex(@"at\s*(?<method>.*?)\s(?<location>[^\s]+):(?<line>\d+):(?<column>\d+)", RegexOptions.Compiled);
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
                Task.Factory.StartNew(FlushScripts, flushTaskCancelationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            private void StopFlush() {
                flushTaskCancelationToken.Cancel();
                if (flushRunning) {
                    stoppedFlushHandle.WaitOne();
                }
                flushTaskCancelationToken.Dispose();

                // signal any pending js evaluations
                foreach (var pendingScript in pendingScripts.ToArray()) {
                    pendingScript.WaitHandle.Set();
                }

                pendingScripts.Dispose();
            }

            private ScriptTask QueueScript(string script, TimeSpan? timeout = default(TimeSpan?), bool awaitable = false) {
                if (OwnerWebView.isDisposing) {
                    return null;
                }
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
                System.Diagnostics.Debugger.Launch();
                var scriptWithErrorHandling = WrapScriptWithErrorHandling(script);

                var scriptTask = QueueScript(scriptWithErrorHandling, timeout, true);
                if (scriptTask == null) {
                    return GetResult<T>(null); // webview is disposing
                }

                if (!flushRunning) {
                    var succeeded = scriptTask.WaitHandle.WaitOne(timeout ?? TimeSpan.FromSeconds(15)); // wait with timeout if flush is not running yet to avoid hanging forever
                    if (!succeeded) {
                        throw new JavascriptException("Timeout", "Javascript engine is not initialized");
                    }
                } else {
                    var succeeded = scriptTask.WaitHandle.WaitOne();
                    if (!succeeded) {
                        throw new JavascriptException("Timeout", (timeout.HasValue ? $"More than {timeout.Value.TotalMilliseconds}ms elapsed" : "Timeout ocurred") + $" evaluating the script: '{script}'");
                    }
                }

                if (scriptTask.Exception != null) {
                    throw scriptTask.Exception;
                }

                if (scriptTask.Result == null) {
                    return GetResult<T>(null); // webview is disposing
                }

                if (scriptTask.Result.Success) {
                    return GetResult<T>(scriptTask.Result.Result);
                }
                
                throw ParseResponseException(scriptTask.Result);
            }

            public T EvaluateScriptFunction<T>(string functionName, bool serializeParams, params object[] args) {
                return EvaluateScript<T>(MakeScript(functionName, serializeParams, args));
            }

            public void ExecuteScriptFunction(string functionName, bool serializeParams, params object[] args) {
                QueueScript(MakeScript(functionName, serializeParams, args));
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

            private static string MakeScript(string functionName, bool serializeParams, object[] args) {
                var argsSerialized = args.Select(a => Serialize(a, serializeParams));
                return functionName + "(" + string.Join(",", argsSerialized) + ")";
            }

            private static string Serialize(object value, bool serializeValue) {
                if (value == null) {
                    return "null";
                }
                if (serializeValue) {
                    if (value is string valueText) {
                        return HttpUtility.JavaScriptStringEncode(valueText, true);
                    }
                    if (value is IEnumerable innerValues) {
                        return "[" + string.Join(",", innerValues.Cast<object>().Select(v => Serialize(v, serializeValue))) + "]";
                    }
                }
                // TODO complex types
                return value.ToString();
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
                        var jsStack = jsError.Stack.Substring(Math.Min(jsError.Stack.Length, (jsError.Name + ": " + jsError.Message).Length))
                                                   .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                        var parsedStack = new List<JavascriptStackFrame>();

                        foreach(var stackFrame in jsStack) {
                            var frameParts = StackFrameRegex.Match(stackFrame);
                            if (frameParts.Success) {
                                parsedStack.Add(new JavascriptStackFrame() {
                                    FunctionName = frameParts.Groups["method"].Value,
                                    SourceName = frameParts.Groups["location"].Value,
                                    LineNumber = int.Parse(frameParts.Groups["line"].Value),
                                    ColumnNumber = int.Parse(frameParts.Groups["column"].Value)
                                });
                            }
                        }
                        
                        return new JavascriptException(jsError.Name, jsError.Message, parsedStack.ToArray());
                    }
                }

                return new JavascriptException("Javascript Error", response.Message);
            }

            internal static bool IsInternalException(string exceptionMessage) {
                return exceptionMessage.EndsWith(InternalException);
            }
        }
    }
}