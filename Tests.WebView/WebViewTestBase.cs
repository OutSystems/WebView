using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    public class WebViewTestBase : TestBase<WebViewControl.WebView> {
        private List<string> registeredNativeObjects = new();

        protected override void InitializeView() {
            if (TargetView != null) {
                TargetView.UnhandledAsyncException += OnUnhandledAsyncException;
            }
            base.InitializeView();
        }

        protected override async Task AfterInitializeView() {
            await base.AfterInitializeView();

            var taskCompletionSource = new TaskCompletionSource<bool>();
            TargetView.WebViewInitialized += () => {
                taskCompletionSource.SetResult(true);
            };

            await taskCompletionSource.Task;
            await Load("<html><script>;</script><body>Test page</body></html>");
        }

        protected override Task TearDown() {
            registeredNativeObjects.ForEach(objName => TargetView.UnregisterJavascriptObject(objName));
            registeredNativeObjects.Clear();

            return base.TearDown();
        }

        protected Task Load(string html) {
            var taskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            void OnNavigated(string url, string frameName) {
                if (url != UrlHelper.AboutBlankUrl) {
                    TargetView.Navigated -= OnNavigated;
                    taskCompletionSource.SetResult(true);
                }
            }
            TargetView.Navigated += OnNavigated;
            TargetView.LoadHtml(html);
            return taskCompletionSource.Task;
        }

        protected async Task WithUnhandledExceptionHandling(AsyncTestDelegate action, Func<Exception, bool> onException) {
            void OnUnhandledException(UnhandledAsyncExceptionEventArgs e) {
                e.Handled = onException(e.Exception);
            }

            var failOnAsyncExceptions = FailOnAsyncExceptions;
            FailOnAsyncExceptions = false;
            TargetView.UnhandledAsyncException += OnUnhandledException;

            try {
                await action();
            } finally {
                TargetView.UnhandledAsyncException -= OnUnhandledException;
                FailOnAsyncExceptions = failOnAsyncExceptions;
            }
        }

        protected override bool ShowDebugConsole() {
            if (TargetView.IsDisposing) {
                return false;
            }
            TargetView.ShowDeveloperTools();
            return true;
        }

        protected void RegisterJavascriptObject(string name, object objectToBind,
            Func<Func<object>, object> interceptCall = null) {
            registeredNativeObjects.Add(name);
            TargetView.RegisterJavascriptObject(name, objectToBind, interceptCall);
        }

        protected Task RunScript(string script) {
            var boundChecks = registeredNativeObjects.Select(name => $"cefglue.checkObjectBound('{name}')");
            var html = $"<html><script>(async function() {{ await Promise.all([{string.Join(',', boundChecks)}]); {script} }})();</script><body></body></html>";

            return Load(html);
        }
    }
}
