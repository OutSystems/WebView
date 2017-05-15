using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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

        internal class JavascriptExecutor : IDisposable {

            // TODO JMN cef
            //private static readonly TraceSwitch TraceSwitch = new TraceSwitch("WebView.JavascriptExecutor", string.Empty) {
            //    Level = TraceLevel.Verbose
            //};

            private readonly WebView OwnerWebView;
            private readonly ConcurrentQueue<Tuple<long, string>> pendingScripts = new ConcurrentQueue<Tuple<long, string>>();
            private readonly AutoResetEvent hasScriptsEvent = new AutoResetEvent(false);
            private readonly AutoResetEvent pendingFlushEvent = new AutoResetEvent(false);
            private readonly CancellationTokenSource flushTaskCancelationToken = new CancellationTokenSource();
            private long executionCounter = 0;

            public JavascriptExecutor(WebView ownerWebView) {
                OwnerWebView = ownerWebView;
                OwnerWebView.JavascriptContextCreated += OnJavascriptContextCreated;
                OwnerWebView.RenderProcessCrashed += () => flushTaskCancelationToken.Cancel();
            }

            private void OnJavascriptContextCreated() {
                OwnerWebView.JavascriptContextCreated -= OnJavascriptContextCreated;
                Task.Factory.StartNew(FlushScripts, flushTaskCancelationToken.Token);
            }

            [DebuggerNonUserCode]
            private T InternalSafeEvaluateScript<T>(string script, TimeSpan? timeout) {
                if (OwnerWebView.isDisposing) {
                    return default(T);
                }

                try {
                    script = "try {" + script + "; } catch (e) { throw JSON.stringify({ stack: e.stack, message: e.message, name: e.name }) }";
                    return InternalEvaluateScript<T>(script, timeout ?? OwnerWebView.DefaultScriptsExecutionTimeout);
                } catch (JavascriptException originalException) {
                    var jsErrorJSON = originalException.Message;
                    jsErrorJSON = jsErrorJSON.Substring(Math.Max(0, jsErrorJSON.IndexOf("{")));
                    jsErrorJSON = jsErrorJSON.Substring(0, jsErrorJSON.LastIndexOf("}") + 1);
                    var jsError = DeserializeJSON<JsError>(jsErrorJSON);
                    jsError.Name = jsError.Name ?? "";
                    jsError.Message = jsError.Message ?? "";
                    jsError.Stack = jsError.Stack ?? "";
                    var jsStack = jsError.Stack.Substring(Math.Min(jsError.Stack.Length, (jsError.Name + ": " + jsError.Message).Length)).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    jsStack = jsStack.Select(l => l.Substring("    at ".Length)).ToArray();
                    throw new JavascriptException(jsError.Name, jsError.Message, jsStack);
                }
            }

            private void QueueScript(string script) {
                var counter = IncCounter();
                pendingScripts.Enqueue(Tuple.Create(counter, script));
                hasScriptsEvent.Set();
            }

            private void FlushScripts() {
                try {
                    while (!flushTaskCancelationToken.IsCancellationRequested) {
                        InnerFlushScripts();
                    }
                } catch (OperationCanceledException) {
                    // stop
                }
            }

            private void InnerFlushScripts() {
                WaitHandle.WaitAny(new[] { hasScriptsEvent, flushTaskCancelationToken.Token.WaitHandle });

                try {
                    if (flushTaskCancelationToken.IsCancellationRequested) {
                        return;
                    }

                    var scriptsToExecute = pendingScripts.Select(s => s.Item2).ToArray();
                    var aggregatedScripts = string.Join(";", scriptsToExecute);

                    if (!string.IsNullOrEmpty(aggregatedScripts)) {
                        OwnerWebView.chromium.EvaluateScriptAsync(aggregatedScripts).Wait(flushTaskCancelationToken.Token);
                    }

                    // remove the executed scripts from the queue
                    for (var i = 0; i < scriptsToExecute.Length; i++) {
                        Tuple<long, string> script;
                        if (!pendingScripts.TryDequeue(out script)) {
                            throw new InvalidOperationException("Expected Dequeue to succeed");
                        }
                    }
                } finally {
                    // unblock anyone waiting
                    pendingFlushEvent.Set();
                }
            }

            public T EvaluateScript<T>(string script, TimeSpan? timeout = default(TimeSpan?)) {
                var now = IncCounter();
                Tuple<long, string> pendingScript;
                while (!flushTaskCancelationToken.IsCancellationRequested && pendingScripts.TryPeek(out pendingScript) && pendingScript.Item1 < now) {
                    pendingFlushEvent.WaitOne();
                }

                return InternalSafeEvaluateScript<T>(script, timeout);
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

            private T InternalEvaluateScript<T>(string script, TimeSpan? timeout = default(TimeSpan?)) {
                var task = OwnerWebView.chromium.EvaluateScriptAsync(script, timeout);
                task.Wait(flushTaskCancelationToken.Token);

                if (task.Result.Success) {
                    return GetResult<T>(task.Result.Result);
                }
                throw new JavascriptException("Error", task.Result.Message, new string[0]);
            }

            private T GetResult<T>(object result) {
                var targetType = typeof(T);
                if (IsBasicType(targetType)) {
                    return (T)result;
                }
                if (result == null && targetType.IsArray) {
                    // return empty arrays when value is null and return type is array
                    return (T)(object)Array.CreateInstance(targetType.GetElementType(), 0);
                }
                return (T)OwnerWebView.binder.Bind(result, targetType);
            }

            public void Dispose() {
                flushTaskCancelationToken.Cancel();
                flushTaskCancelationToken.Dispose();
            }

            private long IncCounter() {
                lock (this) {
                    return ++executionCounter;
                }
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