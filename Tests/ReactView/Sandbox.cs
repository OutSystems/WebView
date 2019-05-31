using System;
using System.Windows;

namespace Tests.ReactView {

    internal class Sandbox : IDisposable {

        private readonly TestReactView view;

        public Sandbox(Window window, string propertyValue, TimeSpan timeout) : this(propertyValue) {
            AttachTo(window);
            WaitReady(timeout);
        }

        public Sandbox(string propertyValue) {
            view = new TestReactView();
            view.PropertyValue = propertyValue;
        }

        public void AttachTo(Window window) {
            window.Content = view;
        }

        public string GetFirstRenderHtml() {
            return view.EvaluateMethod<string>("getFirstRenderHtml");
        }

        public string GetHtml() {
            return view.EvaluateMethod<string>("getHtml");
        }

        public string GetPropertyValue() {
            return view.EvaluateMethod<string>("getPropertyValue");
        }

        public string PropertyValue { get => view.PropertyValue; set => view.PropertyValue = value; }

        public void WaitReady(TimeSpan timeout) {
            ReactViewTestBase.WaitFor(() => view.IsReady, timeout, "View is ready");
        }

        public void Dispose() {
            view.Dispose();
        }
    }
}