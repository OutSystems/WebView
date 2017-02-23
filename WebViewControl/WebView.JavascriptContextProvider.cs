using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JavascriptObject = System.Collections.Generic.Dictionary<string, object>;

namespace WebViewControl {

    partial class WebView {

        internal class JavascriptContextProvider : IJavascriptContextProvider {

            private static readonly TraceSwitch TraceSwitch = new TraceSwitch("WebView.JavascriptContextProvider", string.Empty) {
                Level = TraceLevel.Verbose
            };

            private readonly WebView OwnerWebView;
            private readonly BlockingCollection<string> pendingScripts = new BlockingCollection<string>();
            private readonly AutoResetEvent pendingFlushEvent = new AutoResetEvent(false);

            public JavascriptContextProvider(WebView ownerWebView) {
                OwnerWebView = ownerWebView;
                OwnerWebView.BrowserInitialized += () => Task.Factory.StartNew(FlushScripts);
            }

            // TODO: mmv: TraceUtils.callAndGrabStack
            protected virtual string JavascriptExecutionWrapper {
                get { return null; }
            }

            // TODO: mmv: TraceUtils.getLastError()
            protected virtual string JavascriptExceptionGrabber {
                get { return null; }
            }

            public virtual string GetJavascriptContext() {
                return (string) OwnerWebView.InternalEvaluateScript(
                    @"'href:' + window.location.href + '\n' + " +
                    @"'props:' + (function(r) {for (var p in window) {r+=p+'|'} return r})('')"
                    , TimeSpan.FromSeconds(10));
            }

            public virtual JavascriptObject GetLastJavascriptError() {
                if (JavascriptExceptionGrabber == null) {
                    return null;
                } else {
                    return (JavascriptObject)OwnerWebView.InternalEvaluateScript(JavascriptExceptionGrabber, TimeSpan.FromSeconds(10));
                }
            }

            [DebuggerNonUserCode]
            private void SafeExecuteScript(Action<string> action, string javascript) {
                //System.Diagnostics.Debug.WriteLine("JAVASCRIPT ... " + functionName);
                //stopWatch.Start();

                if (OwnerWebView.isDisposing) {
                    return;
                }

                try {
                    action(javascript);
#if EXTRA_DEBUG_LOGS
                    jsCtxTraceSwitch.WriteLine("Run: " + javascript);
#endif
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
                } finally {
                    //stopWatch.Stop();
                    //System.Diagnostics.Debug.WriteLine("JAVASCRIPT END " + functionName + "[acc:" + stopWatch.ElapsedMilliseconds + "ms]");
                }
            }

            [DebuggerNonUserCode]
            private void SafeExecuteScript(Action<string> action, string functionName, params object[] args) {
                SafeExecuteScript(action, MakeScript(functionName, args));
            }

            private string MakeScript(string functionName, object[] args) {
                var argsSerialized = args.Select(a => a == null ? "null" : a.ToString());
                if (JavascriptExecutionWrapper == null) {
                    return functionName + "(" + string.Join(",", argsSerialized) + ")";
                } else {
                    return JavascriptExecutionWrapper + "(" + string.Join(",", (new[] { functionName }).Concat(argsSerialized)) + ")";
                }
            }

            private void QueueScript(string script) {
                pendingScripts.Add(script);
            }

            private void FlushScripts() {
                while (!pendingScripts.IsCompleted) {
                    InnerFlushScripts();
                }
            }

            private void InnerFlushScripts() {
                var bulkScript = new StringBuilder();
                //var watch = new Stopwatch();
                //watch.Start();
                var i = 0;
                do {
                    var script = pendingScripts.Take();
                    bulkScript.AppendLine(script);
                    i++;
                } while (pendingScripts.Count > 0);

                var allScript = bulkScript.ToString();
                if (!string.IsNullOrEmpty(allScript)) {
                    //watch.Stop();
                    //Console.WriteLine(watch.ElapsedTicks + " / " + i);
                    OwnerWebView.InternalExecuteScript(allScript);
                }
                pendingFlushEvent.Set();
            }

            //[DebuggerNonUserCode]
            public virtual T EvaluateScriptFunction<T>(string functionName, params object[] args) {
#if EXTRA_DEBUG_LOGS
                jsCtxTraceSwitch.WriteLine("WantEv: " + functionName);
#endif
                T result = default(T);
                //var watch = new Stopwatch();
                //watch.Start();
                var pendingScriptsCount = pendingScripts.Count;
                if (pendingScriptsCount > 0) {
                    pendingFlushEvent.WaitOne();
                }
                //watch.Stop();
                //Console.WriteLine("had to wait: " + watch.ElapsedTicks);

                SafeExecuteScript(script => result = (T)OwnerWebView.InternalEvaluateScript(script), functionName, args);
                return result;
            }

            [DebuggerNonUserCode]
            public virtual void ExecuteScriptFunction(string functionName, params object[] args) {
#if EXTRA_DEBUG_LOGS
                jsCtxTraceSwitch.WriteLine("WantEx: " + functionName);
#endif
                QueueScript(MakeScript(functionName, args));
            }

            public virtual void BindVariable(string variableName, object objectToBind) {
                if (objectToBind == null) {
                    throw new InvalidOperationException("objectToBind");
                }
                OwnerWebView.RegisterJavascriptObject(variableName, objectToBind);
            }
        }
    }
}
