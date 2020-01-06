using System;
using System.Collections.Concurrent;
using WebViewControl;

namespace ReactViewControl {

    internal class ExecutionEngine : IExecutionEngine {

        private bool isReady;
        private string generation;

        public ExecutionEngine(WebView webview, string frameName) {
            WebView = webview;
            FrameName = frameName;
        }

        private WebView WebView { get; }

        private string FrameName { get; }

        private ConcurrentQueue<Tuple<IViewModule, string, object[]>> PendingExecutions { get; } = new ConcurrentQueue<Tuple<IViewModule, string, object[]>>();

        private string FormatMethodInvocation(IViewModule module, string methodCall) {
            return ReactViewRender.ModulesObjectName + "(\"" + FrameName + "\",\"" + generation + "\",\"" + module.Name + "\")." + methodCall;
        }

        public virtual void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            if (isReady) {
                var method = FormatMethodInvocation(module, methodCall);
                WebView.ExecuteScriptFunctionWithSerializedParams(method, args);
            } else {
                PendingExecutions.Enqueue(Tuple.Create(module, methodCall, args));
            }
        }

        public virtual T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) {
            var method = FormatMethodInvocation(module, methodCall);
            return WebView.EvaluateScriptFunctionWithSerializedParams<T>(method, args);
        }

        public virtual void Start(string generation) {
            this.generation = generation;
            isReady = true;
            while (true) {
                if (PendingExecutions.TryDequeue(out var pendingScript)) {
                    var method = FormatMethodInvocation(pendingScript.Item1, pendingScript.Item2);
                    WebView.ExecuteScriptFunctionWithSerializedParams(method, pendingScript.Item3);
                } else {
                    // nothing else to execute
                    break;
                }
            }
        }
    }
}
