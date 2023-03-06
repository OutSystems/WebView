using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using NUnit.Framework;
using WebViewControl;

namespace Tests.WebView {

    public class IsolateCommonTests : WebViewTestBase {
        
        protected override Task SetUp() {
            return Task.CompletedTask;
        }

        protected override Task TearDown() {
            return Task.CompletedTask;
        }

        /*[Test(Description = "Tests that the webview is disposed when host window is not shown")]
        public async Task WebViewIsNotDisposedWhenUnloaded() {
            await Run(async () => {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                var view = new WebViewControl.WebView();
                view.Disposed += delegate {
                    taskCompletionSource.SetResult(true);
                };
                
                var window = new Window {
                    Title = CurrentTestName,
                    Content = view
                };

                try {
                    window.Show();

                    window.Content = null;
                    Assert.IsFalse(taskCompletionSource.Task.IsCompleted);

                    window.Content = view;
                } finally {
                    window.Close();
                }
                var disposed = await taskCompletionSource.Task;
                Assert.IsTrue(disposed);
            });
        }*/
    }
}
