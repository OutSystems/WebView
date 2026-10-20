using System.Windows;

namespace WebViewControl {

    partial interface IWebViewEngine {

        /// <summary>
        /// The element that renders the web content, hosted by the <see cref="WebView"/>.
        /// </summary>
        UIElement View { get; }
    }
}
