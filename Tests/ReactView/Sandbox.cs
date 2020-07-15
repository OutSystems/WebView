using System;
using Avalonia.Controls;

namespace Tests.ReactView {

    internal class Sandbox : IDisposable {

        private TestReactView View { get; }

        public Sandbox(Window window, string propertyValue, TimeSpan timeout) : this(propertyValue) {
            AttachTo(window);
            WaitReady(timeout);
        }

        public Sandbox(string propertyValue) {
            View = new TestReactView();
            View.PropertyValue = propertyValue;
        }

        public void AttachTo(Window window) {
            window.Content = View;
        }

        public string GetFirstRenderHtml() {
            return View.EvaluateMethod<string>("getFirstRenderHtml");
        }

        public string GetHtml() {
            return View.EvaluateMethod<string>("getHtml");
        }

        public string GetPropertyValue() {
            return View.EvaluateMethod<string>("getPropertyValue");
        }

        public string PropertyValue { get => View.PropertyValue; set => View.PropertyValue = value; }

        public void WaitReady(TimeSpan timeout) {
            ReactViewTestBase.WaitFor(() => View.IsReady, timeout, "View is ready");
        }

        public void Dispose() {
            View.Dispose();
        }
    }
}