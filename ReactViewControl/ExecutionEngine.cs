using System;
using System.Collections.Concurrent;
using WebViewControl;

namespace ReactViewControl {

    internal class ExecutionEngine : IExecutionEngine {

        private string generation;
        private string frameName;
        private WebView webView;

        private ConcurrentQueue<Tuple<IViewModule, string, object[]>> PendingExecutions { get; } = new ConcurrentQueue<Tuple<IViewModule, string, object[]>>();

        private string FormatMethodInvocation(IViewModule module, string methodCall) {
            return ReactViewRender.ModulesObjectName + "(\"" + frameName + "\",\"" + generation + "\",\"" + module.Name + "\")." + methodCall;
        }

        public void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            if (webView != null) {
                var method = FormatMethodInvocation(module, methodCall);
                webView.ExecuteScriptFunctionWithSerializedParams(method, args);
            } else {
                PendingExecutions.Enqueue(Tuple.Create(module, methodCall, args));
            }
        }

        public T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) {
            if (webView == null) {
                return default(T);
            }
            var method = FormatMethodInvocation(module, methodCall);
            return webView.EvaluateScriptFunctionWithSerializedParams<T>(method, args);
        }

        public void Start(WebView webView, string frameName, string generation) {
            this.generation = generation;
            this.frameName = frameName;
            this.webView = webView;
            while (true) {
                if (PendingExecutions.TryDequeue(out var pendingScript)) {
                    var method = FormatMethodInvocation(pendingScript.Item1, pendingScript.Item2);
                    webView.ExecuteScriptFunctionWithSerializedParams(method, pendingScript.Item3);
                } else {
                    // nothing else to execute
                    break;
                }
            }
        }

        public void MergeWorkload(IExecutionEngine executionEngine) {
            if (this != executionEngine && executionEngine is ExecutionEngine otherEngine) {
                var pendingExecutions = otherEngine.PendingExecutions.ToArray();
                foreach (var execution in pendingExecutions) {
                    PendingExecutions.Enqueue(execution);
                }
            }
        }
    }
}