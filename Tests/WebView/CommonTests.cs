using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using NUnit.Framework;

namespace Tests.WebView {

    public class CommonTests : WebViewTestBase {

        [Test(Description = "Attached listeners are called")]
        public void ListenersAreCalled() {
            var listener1Counter = 0;
            var listener2Counter = 0;
            var taskCompletionSourceListener1 = new TaskCompletionSource<bool>();
            var taskCompletionSourceListener21 = new TaskCompletionSource<bool>();
            var taskCompletionSourceListener22 = new TaskCompletionSource<bool>();

            var listener1 = TargetView.AttachListener("event1_name");
            listener1.Handler += delegate {
                listener1Counter++;
                taskCompletionSourceListener1.SetResult(true);
            };

            var listener2 = TargetView.AttachListener("event2_name");
            listener2.Handler += delegate {
                listener2Counter++;
                taskCompletionSourceListener21.SetResult(true);
            };
            listener2.Handler += delegate {
                listener2Counter++;
                taskCompletionSourceListener22.SetResult(true);
            };

            var loadTask = LoadAndWaitReady($"<html><script>{listener1}{listener2}</script><body></body></html>");
            WaitFor(loadTask, taskCompletionSourceListener1.Task, taskCompletionSourceListener21.Task, taskCompletionSourceListener22.Task);

            Assert.AreEqual(1, listener1Counter);
            Assert.AreEqual(2, listener2Counter);
        }

        [Test(Description = "Attached listeners are called in Dispatcher thread")]
        public void ListenersAreCalledInDispatcherThread() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            bool? canAccessDispatcher = null;

            var listener = TargetView.AttachListener("event_name");
            listener.UIHandler += delegate { 
                canAccessDispatcher = Dispatcher.UIThread.CheckAccess();
                taskCompletionSource.SetResult(true);
            };

            var loadTask = LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");
            WaitFor(loadTask, taskCompletionSource.Task);

            Assert.IsTrue(canAccessDispatcher);
        }

        [Test(Description = "Unhandled Exception event is called when an async unhandled error occurs inside a listener")]
        public void UnhandledExceptionEventIsCalledOnListenerError() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            const string ExceptionMessage = "hey";
            Exception exception = null;

            WithUnhandledExceptionHandling(() => {
                var listener = TargetView.AttachListener("event_name");
                listener.Handler += delegate {
                    taskCompletionSource.SetResult(true);
                    throw new Exception(ExceptionMessage);
                };

                var loadTask = LoadAndWaitReady($"<html><script>{listener}</script><body></body></html>");
                WaitFor(loadTask, taskCompletionSource.Task);
                Assert.IsTrue(exception.Message.Contains(ExceptionMessage));
            }, 
            e => {
                exception = e;
                return true;
            });
        }

        [Test(Description = "Before navigate hook is called")]
        public void BeforeNavigateHookCalled() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var beforeNavigatedCalled = false;
            TargetView.BeforeNavigate += (request) => {
                taskCompletionSource.SetResult(true);
                request.Cancel();
                beforeNavigatedCalled = true;
            };
            TargetView.Address = "https://www.google.com";
            WaitFor(taskCompletionSource.Task);
            Assert.IsTrue(beforeNavigatedCalled);
        }

        [Test(Description = "Javascript evaluation on navigated event does not block")]
        public void JavascriptEvaluationOnNavigatedDoesNotBlock() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var navigated = false;
            TargetView.Navigated += (_, __) => {
                taskCompletionSource.SetResult(true);
                TargetView.EvaluateScript<int>("1+1");
                navigated = true;
            };
            var loadTask = LoadAndWaitReady("<html><body></body></html>");
            WaitFor(loadTask, taskCompletionSource.Task);
            Assert.IsTrue(navigated);
        }

        // TODO failing
        [Test(Description = "Setting zoom works as expected")]
        public void ZoomWorksAsExpected() {
            var loadTask = LoadAndWaitReady("<html><body></body></html>");

            const double Zoom = 1.5;
            TargetView.ZoomPercentage = Zoom;

            Dispatcher.UIThread.RunJobs(DispatcherPriority.MinValue);
            WaitFor(loadTask);

            Assert.AreEqual(Zoom, TargetView.ZoomPercentage);
        }

        // TODO failing
        [Test(Description = "Tests that the webview is disposed when host window is not shown")]
        public void WebViewIsDisposedWhenHostWindowIsNotShown() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var view = new WebViewControl.WebView();
            var window = new Window {
                Title = CurrentTestName
            };

            try {
                window.Content = view;

                var disposed = false;
                view.Disposed += delegate {
                    taskCompletionSource.SetResult(true);
                    disposed = true;
                };

                window.Close();

                WaitFor(taskCompletionSource.Task);
                Assert.IsTrue(disposed);
            } finally {
                window.Close();
            }
        }

        [Test(Description = "Tests that the webview is disposed when host window is not shown")]
        public void WebViewIsNotDisposedWhenUnloaded() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var view = new WebViewControl.WebView();

            var disposed = false;
            view.Disposed += delegate {
                taskCompletionSource.SetResult(true);
                disposed = true;
            };

            var window = new Window {
                Title = CurrentTestName,
                Content = view
            };

            try {    
                window.Show();
                window.Content = null;
                Assert.IsFalse(disposed);

                window.Content = view;
                window.Close();
                WaitFor(taskCompletionSource.Task);
                Assert.IsTrue(disposed);
            } finally {
                window.Close();
            }
        }
    }
}
