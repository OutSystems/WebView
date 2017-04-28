using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;
using JavascriptObject = System.Collections.Generic.IDictionary<string, object>;

namespace WebViewControl {

    partial class WebView {

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
            private readonly CefSharp.ModelBinding.DefaultBinder binder = new CefSharp.ModelBinding.DefaultBinder(new CefSharp.ModelBinding.DefaultFieldNameConverter());
            private long executionCounter = 0;

            public JavascriptExecutor(WebView ownerWebView) {
                OwnerWebView = ownerWebView;
                OwnerWebView.WebViewInitialized += () => Task.Factory.StartNew(FlushScripts, flushTaskCancelationToken.Token);
            }

            // TODO JMN cef3
            protected string JavascriptExecutionWrapper {
                get { return null; }
            }

            // TODO JMN cef3
            protected string JavascriptExceptionGrabber {
                get { return null; }
            }

            public string GetJavascriptContext() {
                return InternalEvaluateScript<string>(
                    @"'href:' + window.location.href + '\n' + " +
                    @"'props:' + (function(r) {for (var p in window) {r+=p+'|'} return r})('')"
                    , TimeSpan.FromSeconds(10));
            }

            public JavascriptObject GetLastJavascriptError() {
                if (JavascriptExceptionGrabber == null) {
                    return null;
                } else {
                    return InternalEvaluateScript< JavascriptObject>(JavascriptExceptionGrabber, TimeSpan.FromSeconds(10));
                }
            }

            [DebuggerNonUserCode]
            private void SafeExecuteScript(Action action) {
                if (OwnerWebView.isDisposing) {
                    return;
                }

                try {
                    action();
                } catch (JavascriptException originalException) {
                    Exception jsException = null;
                    try {
                        try {
                            var jsError = GetLastJavascriptError();
                            if (jsError != null) {
                                jsException = new ParsedJSException(
                                    (string)jsError["name"],
                                    (string)jsError["message"], null, null);
                                    // TODO JMN cef3
                                    /*ToArray<string>((JSObject) jsError["stack"])
                                        .Select(s => "   " + s.TrimStart(' '))
                                        .Concat("   at " + javascript)
                                        .ToArray(),
                                    ToArray<string>((JSObject) jsError["messagelog"]));*/
                            }
                        } catch {
                            // sometimes we get a "TraceUtils is not defined"
                            // can we get some more info on that?
                            try {
                                var ctx = GetJavascriptContext();
                                jsException = new Exception(ctx, originalException);
                            } catch {
                                // ignore, lets throw the original
                            }
                        }
                    } catch {
                        // ignore, lets throw the original
                    }
                    if (jsException != null) {
                        throw jsException;
                    }
                    throw;
                }
            }

            private string MakeScript(string functionName, string[] args) {
                var argsSerialized = args.Select(a => a == null ? "null" : a);
                if (JavascriptExecutionWrapper == null) {
                    return functionName + "(" + string.Join(",", argsSerialized) + ")";
                } else {
                    return JavascriptExecutionWrapper + "(" + string.Join(",", (new[] { functionName }).Concat(argsSerialized)) + ")";
                }
            }

            private void QueueScript(string script) {
                var counter = IncCounter();
                pendingScripts.Enqueue(Tuple.Create(counter, script));
                hasScriptsEvent.Set();
            }

            private async void FlushScripts() {
                try {
                    while (!flushTaskCancelationToken.IsCancellationRequested) {
                        await InnerFlushScripts();
                    }
                } catch (OperationCanceledException) {
                    // stop
                }
            }

            private async Task InnerFlushScripts() {
                WaitHandle.WaitAny(new[] { hasScriptsEvent, flushTaskCancelationToken.Token.WaitHandle });

                var scriptsToExecute = pendingScripts.Select(s => s.Item2).ToArray();
                var aggregatedScripts = string.Join(";", scriptsToExecute);

                if (!string.IsNullOrEmpty(aggregatedScripts)) {
                    await OwnerWebView.chromium.EvaluateScriptAsync(aggregatedScripts);
                }

                // remove the executed scripts from the queue
                for(var i = 0; i < scriptsToExecute.Length; i++) {
                    Tuple<long, string> script;
                    if (!pendingScripts.TryDequeue(out script)) {
                        throw new InvalidOperationException("Expected Dequeue to succeed");
                    }
                }

                pendingFlushEvent.Set();
            }

            public T EvaluateScript<T>(string script, TimeSpan? timeout = default(TimeSpan?)) {
                T result = default(T);
                var now = IncCounter();
                Tuple<long, string> pendingScript;
                while (pendingScripts.TryPeek(out pendingScript) && pendingScript.Item1 < now) {
                    pendingFlushEvent.WaitOne();
                }

                SafeExecuteScript(() => result = InternalEvaluateScript<T>(script + " /* " + now + "*/", timeout ?? OwnerWebView.DefaultScriptsExecutionTimeout));
                return result;
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
                task.Wait();
                if (task.Result.Success) {
                    return GetResult<T>(task.Result.Result);
                }
                throw new JavascriptException(task.Result.Message);
            }

            private T GetResult<T>(object result) {
                var targetType = typeof(T);
                if (IsBasicType(targetType)) {
                    return (T) result;
                }
                if (result == null && targetType.IsArray) {
                    // return empty arrays when value is null and return type is array
                    return (T) (object) Array.CreateInstance(targetType.GetElementType(), 0);
                }
                return (T) binder.Bind(result, targetType);
            }

            private static bool IsBasicType(Type type) {
                return type.IsPrimitive || type.IsEnum || type == typeof(string);
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
        }
    }
}
