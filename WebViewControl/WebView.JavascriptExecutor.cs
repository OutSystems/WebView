using System;
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
using Xilium.CefGlue;
using Xilium.CefGlue.Common.Events;

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

            public ScriptTask(string script, string functionName, Action<string> evaluate = null) {
                Script = script;
                Evaluate = evaluate;
                FunctionName = functionName;
            }

            public string Script { get; }

            /// <summary>
            /// We store the function name apart from the script and use it later in the exception details 
            /// this prevents any params to be shown in the message because they can contain sensitive information
            /// </summary>
            public string FunctionName { get; }

            public Action<string> Evaluate { get; }
        }

        internal class JavascriptExecutor : IDisposable {

            private const string InternalException = "|WebViewInternalException";

            private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
            private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(15);

            private static Regex StackFrameRegex { get; } = new Regex(@"at\s*(?<method>.*?)\s\(?(?<location>[^\s]+):(?<line>\d+):(?<column>\d+)", RegexOptions.Compiled);
            
            private BlockingCollection<ScriptTask> PendingScripts { get; } = new BlockingCollection<ScriptTask>();
            private CancellationTokenSource FlushTaskCancelationToken { get; } = new CancellationTokenSource();

            private WebView OwnerWebView { get; }

#if DEBUG
            private string Id { get; } = Guid.NewGuid().ToString();
#endif

            private CefFrame frame;
            private Task flushTask;

            public JavascriptExecutor(WebView owner, CefFrame frame = null) {
                OwnerWebView = owner;
                this.frame = frame;
            }

            public bool IsValid => frame == null || frame.IsValid; // consider valid when not bound (yet) or frame is valid

            private bool IsFlushTaskInitializing => flushTask == null || flushTask.Status < TaskStatus.Running;

            public void StartFlush(CefFrame frame) {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"{nameof(StartFlush)} ('{Id}')");
#endif
                lock (FlushTaskCancelationToken) {
                    if (flushTask != null || !frame.IsValid || FlushTaskCancelationToken.IsCancellationRequested) {
                        return;
                    }
                    this.frame = frame;
                    flushTask = Task.Factory.StartNew(FlushScripts, FlushTaskCancelationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
            }

            private void StopFlush() {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"{nameof(StopFlush)} ('{Id}')");
#endif
                lock (FlushTaskCancelationToken) {
                    if (FlushTaskCancelationToken.IsCancellationRequested) {
                        return;
                    }
                    FlushTaskCancelationToken.Cancel();
                    PendingScripts.CompleteAdding();
                }
            }

            private ScriptTask QueueScript(string script, string functionName = null, Action<string> evaluate = null) {
                lock (FlushTaskCancelationToken) {
                    if (FlushTaskCancelationToken.IsCancellationRequested) {
                        return null;
                    }

                    var scriptTask = new ScriptTask(script, functionName, evaluate);
                    PendingScripts.Add(scriptTask);
                    return scriptTask;
                }
            }

            private void FlushScripts() {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"{nameof(FlushScripts)} running ('{Id}')");
#endif
                OwnerWebView.ExecuteWithAsyncErrorHandling(InnerFlushScripts);
            }

            private void InnerFlushScripts() {
                try {
                    var scriptsToExecute = new List<ScriptTask>();
                    foreach (var scriptTask in PendingScripts.GetConsumingEnumerable()) {
                        if (scriptTask.Evaluate == null) {
                            scriptsToExecute.Add(scriptTask);
                        }
                        if ((PendingScripts.Count == 0 || scriptTask.Evaluate != null) && scriptsToExecute.Count > 0) {
                            BulkExecuteScripts(scriptsToExecute);
                            scriptsToExecute.Clear();
                        }
                        scriptTask.Evaluate?.Invoke(scriptTask.Script);
                    }
                } catch (OperationCanceledException) {
                    // stop
                } finally {
                    PendingScripts.Dispose();
                    FlushTaskCancelationToken.Dispose();
                }
            }

            private void BulkExecuteScripts(IEnumerable<ScriptTask> scriptsToExecute) {
                var script = string.Join(";" + Environment.NewLine, scriptsToExecute.Select(s => s.Script));
                if (frame.IsValid) {
                    var frameName = frame.Name;
                    try {
                        var timeout = OwnerWebView.DefaultScriptsExecutionTimeout ?? DefaultTimeout;
                        var task = OwnerWebView.chromium.EvaluateJavaScript<object>(WrapScriptWithErrorHandling(script), timeout: timeout);
                        task.Wait(FlushTaskCancelationToken.Token);
                    } catch (OperationCanceledException) { 
                        // ignore
                    } catch (Exception e) {
                        var evaluatedScriptFunctions = scriptsToExecute.Select(s => s.FunctionName);
                        OwnerWebView.ForwardUnhandledAsyncException(ParseException(e, evaluatedScriptFunctions), frameName);
                    }
                }
            }

            public async Task<T> EvaluateScript<T>(string script, string functionName = null, TimeSpan? timeout = null) {
                const string TimeoutExceptionName = "Timeout";
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"{nameof(EvaluateScript)} '{script}' on ('{Id}')");
#endif
                var evaluationTask = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

                void Evaluate(string scriptToEvaluate) {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Evaluating '{script}' on ('{Id}')");
#endif
                    try {
                        var innerEvaluationTask = OwnerWebView.chromium.EvaluateJavaScript<T>(WrapScriptWithErrorHandling(scriptToEvaluate), timeout: timeout);
                        innerEvaluationTask.Wait(FlushTaskCancelationToken.Token);
                        evaluationTask.SetResult(GetResult<T>(innerEvaluationTask.Result));
                    } catch (Exception e) {
                        if (FlushTaskCancelationToken.IsCancellationRequested) {
                            evaluationTask.SetResult(GetResult<T>(default(T)));
                        } else if (e.InnerException is TaskCanceledException) {
                            evaluationTask.SetException(new JavascriptException(TimeoutExceptionName, "Script evaluation timed out"));
                        } else {
                            evaluationTask.SetException(ParseException(e, new[] { functionName }));
                        }
                    }
                }

                var scriptTask = QueueScript(script, functionName, Evaluate);
                if (scriptTask == null) {
                    return default;
                }

                if (timeout.HasValue) {
                    var tasks = new [] {
                        evaluationTask.Task,
                        Task.Delay(timeout.Value)
                    };

                    // wait with timeout if flush is not running yet to avoid hanging forever
                    var task = await Task.WhenAny(tasks).ConfigureAwait(false);

                    if (task != evaluationTask.Task) {
                        if (IsFlushTaskInitializing) {
                            throw new JavascriptException(TimeoutExceptionName, $"Javascript engine is not initialized after {InitializationTimeout.Seconds}s");
                        }
                        // flush is already running, timeout will fire from the evaluation
                    }
                } 

                return await evaluationTask.Task;
            }

            public Task<T> EvaluateScriptFunction<T>(string functionName, bool serializeParams, params object[] args) {
                return EvaluateScript<T>(MakeScript(functionName, serializeParams, args, emitReturn: true), functionName);
            }

            public void ExecuteScriptFunction(string functionName, bool serializeParams, params object[] args) {
                QueueScript(MakeScript(functionName, serializeParams, args, emitReturn: false), functionName);
            }

            public void ExecuteScript(string script) {
                QueueScript(script);
            }

            private T GetResult<T>(object result) {
                var targetType = typeof(T);
                if (result == null) {
                    if (targetType.IsArray) {
                        // return empty arrays when value is null and return type is array
                        return (T)(object)Array.CreateInstance(targetType.GetElementType(), 0);
                    }
                    return default(T); // return default T (its safer, because we allow returning null and converting into a default struct value)
                }
                if (IsBasicType(targetType)) {
                    return (T)result;
                }
                return (T)result;
            }

            public void Dispose() {
                StopFlush();
            }

            private static bool IsBasicType(Type type) {
                return type.IsPrimitive || type.IsEnum || type == typeof(string);
            }

            private static string MakeScript(string functionName, bool serializeParams, object[] args, bool emitReturn) {
                string SerializeParam(object value) {
                    if (serializeParams || value == null) {
                        return JavascriptSerializer.Serialize(value);
                    }
                    // TODO complex types
                    return value.ToString();
                }
                var argsSerialized = args.Select(SerializeParam);
                return $"{(emitReturn ? "return " : string.Empty)}{functionName}({string.Join(",", argsSerialized)})";
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

            private static Exception MakeTimeoutException(string functionName, TimeSpan timeout) {
                return new JavascriptException("Timeout", $"More than {timeout.TotalMilliseconds}ms elapsed evaluating: '{functionName}'");
            }

            private static Exception ParseException(Exception exception, IEnumerable<string> evaluatedScriptFunctions) {
                var jsErrorJSON = ((exception is AggregateException aggregateException) ? aggregateException.InnerExceptions.FirstOrDefault(e => IsInternalException(e.Message))?.Message : exception.Message) ?? "";

                // try parse js exception
                jsErrorJSON = jsErrorJSON.Substring(Math.Max(0, jsErrorJSON.IndexOf("{")));
                jsErrorJSON = jsErrorJSON.Substring(0, jsErrorJSON.LastIndexOf("}") + 1);

                var evaluatedStackFrames = evaluatedScriptFunctions.Where(f => !string.IsNullOrEmpty(f))
                                                                   .Select(f => new JavascriptStackFrame(f, "eval", 0, 0));

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

                        parsedStack.AddRange(evaluatedStackFrames);

                        foreach (var stackFrame in jsStack) {
                            var frameParts = StackFrameRegex.Match(stackFrame);
                            if (frameParts.Success) {
                                parsedStack.Add(new JavascriptStackFrame(frameParts.Groups["method"].Value, frameParts.Groups["location"].Value, int.Parse(frameParts.Groups["column"].Value), int.Parse(frameParts.Groups["line"].Value)));
                            }
                        }

                        return new JavascriptException(jsError.Name, jsError.Message, parsedStack);
                    }
                }

                return new JavascriptException(exception.Message, evaluatedStackFrames, exception.StackTrace);
            }

            internal static bool IsInternalException(string exceptionMessage) {
                return exceptionMessage.EndsWith(InternalException);
            }
        }
    }
}