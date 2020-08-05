using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Tests.ReactView {

    internal class Sandbox : IDisposable {

        private TestReactView View { get; }

        private Sandbox(Window window, string propertyValue) : this(propertyValue) {
            AttachTo(window);
        }

        public Sandbox(string propertyValue) {
            View = new TestReactView {
                PropertyValue = propertyValue
            };
        }

        public static async Task<Sandbox> InitializeAsync(Window window, string propertyValue) {
            var sandbox = new Sandbox(window, propertyValue);
            await sandbox.Initialize();

            return sandbox;
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

        public string PropertyValue { 
            get => View.PropertyValue; 
            set => View.PropertyValue = value; 
        }

        public async Task Initialize() {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            void OnReady() {
                taskCompletionSource.SetResult(true);
            }
            View.Ready += OnReady;

            try {
                await taskCompletionSource.Task;

            } finally {
                View.Ready -= OnReady;
            }
        }

        public void Dispose() {
            View.Dispose();
        }
    }
}