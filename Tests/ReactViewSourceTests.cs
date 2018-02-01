using System;
using NUnit.Framework;
using WebViewControl;

namespace Tests {

    public class TestReactCustomSourceView : TestReactView {

        protected override string Source => WebView.BuildEmbeddedResourceUrl(GetType().Assembly, GetType().Assembly.GetName().Name, base.Source);
    }

    public class ReactViewSourceTests : ReactViewTestBase<TestReactCustomSourceView> {

        [Test(Description = "Test loading a react component with a fullpath source")]
        public void ComponentWithFullpathSourceIsLoaded() {
            // reaching this point means success
        }
    }
}