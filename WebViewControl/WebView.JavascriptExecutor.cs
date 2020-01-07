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

            public ScriptTask(string script, string functionName, Action evaluate = null) {
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

            public Action Evaluate { get; }
        }

        internal class JavascriptExecutor : IDisposable {

            private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

            private static Regex StackFrameRegex { get; } = new Regex(@"at\s*(?<method>.*?)\s\(?(?<location>[^\s]+):(?<line>\d+):(?<column>\d+)", RegexOptions.Compiled);

            private const string InternalException = "|WebViewInternalException";
            
            private BlockingCollection<ScriptTask> PendingScripts { get; } = new BlockingCollection<ScriptTask>();
            private CancellationTokenSource FlushTaskCancelationToken { get; } = new CancellationTokenSource();
            private ManualResetEvent StoppedFlushHandle { get; } = new ManualResetEvent(false);

            private CefFrame frame;
            private volatile bool isFlushRunning;

            private WebView OwnerWebView { get; }

#if DEBUG
            private int Id { get; }
#endif
            public JavascriptExecutor(WebView owner, CefFrame frame = null) {
                OwnerWebView = owner;
#if DEBUG
                Id = GetHashCode();
#endif
                if (frame != null) {
                    StartFlush(frame);
                }
            }

            public bool IsValid => frame == null || frame.IsValid; // consider valid when not bound (yet) or frame is valid

            public void StartFlush(CefFrame frame) {
                lock (FlushTaskCancelationToken) {
                    if (this.frame != null || !frame.IsValid || FlushTaskCancelationToken.IsCancellationRequested) {
                        return;
                    }
                    this.frame = frame;
                }
                Task.Factory.StartNew(FlushScripts, FlushTaskCancelationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            private void StopFlush() {
                lock (FlushTaskCancelationToken) {
                    if (FlushTaskCancelationToken.IsCancellationRequested) {
                        return;
                    }
                    FlushTaskCancelationToken.Cancel();
                }
                if (isFlushRunning) {
                    StoppedFlushHandle.WaitOne();
                }

                PendingScripts.Dispose();
                FlushTaskCancelationToken.Dispose();
            }

            private ScriptTask QueueScript(string script, string functionName = null, Action evaluate = null) {
                if (OwnerWebView.isDisposing) {
                    return null;
                }

                var scriptTask = new ScriptTask(script, functionName, evaluate);
                PendingScripts.Add(scriptTask);
                return scriptTask;
            }

            private void FlushScripts() {
                OwnerWebView.ExecuteWithAsyncErrorHandling(() => {
                    try {
                        isFlushRunning = true;
                        while (!FlushTaskCancelationToken.IsCancellationRequested) {
                            InnerFlushScripts();
                        }
                    } catch (OperationCanceledException) {
                        // stop
                    } finally {
                        isFlushRunning = false;
                        StoppedFlushHandle.Set();
                    }
                });
            }

            private void InnerFlushScripts() {
                ScriptTask scriptToEvaluate = null;
                var scriptsToExecute = new List<ScriptTask>();

                do {
                    var scriptTask = PendingScripts.Take(FlushTaskCancelationToken.Token);
                    if (scriptTask.Evaluate == null) {
                        scriptsToExecute.Add(scriptTask);
                    } else { 
                        scriptToEvaluate = scriptTask;
                        break; // this script result needs to be handled separately
                    }
                } while (PendingScripts.Count > 0);

                if (scriptsToExecute.Count > 0) {
                    var script = string.Join(";" + Environment.NewLine, scriptsToExecute.Select(s => s.Script));
                    if (frame.IsValid) {
                        var frameName = frame.Name;
                        try {
                            var task = OwnerWebView.chromium.EvaluateJavaScript<object>(WrapScriptWithErrorHandling(script));
                            var timeout = OwnerWebView.DefaultScriptsExecutionTimeout ?? DefaultTimeout;
                            task.Wait((int)timeout.TotalMilliseconds, FlushTaskCancelationToken.Token);
                        } catch (Exception e) {
                            var evaluatedScriptFunctions = scriptsToExecute.Select(s => s.FunctionName);
                            OwnerWebView.ExecuteWithAsyncErrorHandlingOnFrame(() => throw ParseException(e, evaluatedScriptFunctions), frameName);
                        }
                    }
                }

                if (scriptToEvaluate != null) {
                    scriptToEvaluate.Evaluate();
                }
            }

            public T EvaluateScript<T>(string script, string functionName = null, TimeSpan? timeout = null) {
                var result = default(T);
                Exception exception = null;
                var waitHandle = new ManualResetEvent(false);
                var scriptWithErrorHandling = WrapScriptWithErrorHandling(script);

                void Evaluate() {
                    var effectiveTimeout = timeout ?? OwnerWebView.DefaultScriptsExecutionTimeout ?? DefaultTimeout;

                    try {
                        var task = OwnerWebView.chromium.EvaluateJavaScript<T>(scriptWithErrorHandling);

                        if (task.Wait((int)effectiveTimeout.TotalMilliseconds, FlushTaskCancelationToken.Token)) {
                            result = task.Result;
                            return;
                        }

                        exception = MakeTimeoutException(functionName, effectiveTimeout);

                    } catch (TaskCanceledException) {
                        exception = MakeTimeoutException(functionName, effectiveTimeout);

                    } catch (AggregateException e) {
                        if (e.InnerExceptions.Count == 1) {
                            // throw the only and wrapped exception
                            exception = e.InnerException;
                        } else {
                            exception = e;
                        }

                    } catch (Exception e) {
                        exception = e;

                    } finally {
                        waitHandle.Set();
                    }
                }

                var scriptTask = QueueScript(scriptWithErrorHandling, functionName, Evaluate);
                if (scriptTask != null) {
                    if (!isFlushRunning) {
                        var initializationTimeout = timeout ?? TimeSpan.FromSeconds(15);
                        var succeededWaitHandleIndex = WaitHandle.WaitAny(new[] { waitHandle, FlushTaskCancelationToken.Token.WaitHandle }, initializationTimeout); // wait with timeout if flush is not running yet to avoid hanging forever
                        if (succeededWaitHandleIndex == WaitHandle.WaitTimeout) {
                            throw new JavascriptException("Timeout", $"Javascript engine is not initialized after {initializationTimeout.Seconds}s");
                        }
                    } else {
                        WaitHandle.WaitAny(new[] { waitHandle, FlushTaskCancelationToken.Token.WaitHandle });
                    }

                    if (exception != null) {
                        throw ParseException(exception, new[] { functionName });
                    }
                }

                return GetResult<T>(result);
            }

            public T EvaluateScriptFunction<T>(string functionName, bool serializeParams, params object[] args) {
                return EvaluateScript<T>(MakeScript(functionName, serializeParams, args), functionName);
            }

            public void ExecuteScriptFunction(string functionName, bool serializeParams, params object[] args) {
                QueueScript(MakeScript(functionName, serializeParams, args), functionName);
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

            private static string MakeScript(string functionName, bool serializeParams, object[] args) {
                string SerializeParam(object value) {
                    if (serializeParams || value == null) {
                        return JavascriptSerializer.Serialize(value);
                    }
                    // TODO complex types
                    return value.ToString();
                }
                var argsSerialized = args.Select(SerializeParam);
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