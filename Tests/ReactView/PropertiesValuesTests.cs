﻿using System.Windows;
using NUnit.Framework;

namespace Tests.ReactView {

    [Ignore("Needs browser setup")]
    public class PropertiesValuesTests : ReactViewTestBase {

        protected override TestReactView CreateView() {
            return null;
        }

        protected override void ShowDebugConsole() {
        }

        [Test(Description = "Test setting properties after component added to window but window is not visible yet.")]
        public void PropertyValuesArePassedToView() {
            const string PropertyValue = "test value";

            using (var sandbox = new Sandbox("initial value")) {
                var window = new Window() {
                    Title = CurrentTestName
                };
                try {
                    sandbox.AttachTo(window);

                    sandbox.PropertyValue = PropertyValue;

                    window.Show();

                    sandbox.WaitReady(DefaultTimeout);

                    var actualPropertyValue = sandbox.GetPropertyValue();
                    Assert.AreEqual(PropertyValue, actualPropertyValue);
                } finally {
                    window.Close();
                }
            }
        }
    }
}
