using System;
using System.Collections.Concurrent;
using WebViewControl;

namespace ReactViewControl {

    internal class ExecutionEngine : IExecutionEngine {

        private bool isReady;

        public ExecutionEngine(WebView webview, string frameName) {
            WebView = webview;
            FrameName = frameName;
        }

        private WebView WebView { get; }

        private string FrameName { get; }

        private ConcurrentQueue<Tuple<string, object[]>> PendingScripts { get; } = new ConcurrentQueue<Tuple<string, object[]>>();

        private string FormatMethodInvocation(IViewModule module, string methodCall) {
            return ReactViewRender.ModulesObjectName + "(\"" + FrameName + "\",\"" + module.Name + "\")." + methodCall;
        }

        public void ExecuteMethod(IViewModule module, string methodCall, params object[] args) {
            var method = FormatMethodInvocation(module, methodCall);
            if (isReady) {
                WebView.ExecuteScriptFunctionWithSerializedParams(method, args);
            } else {
                PendingScripts.Enqueue(Tuple.Create(method, args));
            }
        }

        public T EvaluateMethod<T>(IViewModule module, string methodCall, params object[] args) {
            var method = FormatMethodInvocation(module, methodCall);
            return WebView.EvaluateScriptFunctionWithSerializedParams<T>(method, args);
        }

        public void Start() {
            isReady = true;
            while (true) {
                if (PendingScripts.TryDequeue(out var pendingScript)) {
                    WebView.ExecuteScriptFunctionWithSerializedParams(pendingScript.Item1, pendingScript.Item2);
                } else {
                    // nothing else to execute
                    break;
                }
            }
        }
    }
}
