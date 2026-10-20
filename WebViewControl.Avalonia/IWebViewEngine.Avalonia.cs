using Avalonia.Controls;

namespace WebViewControl {

    partial interface IWebViewEngine {

        /// <summary>
        /// The control that renders the web content, hosted by the <see cref="WebView"/>.
        /// </summary>
        Control View { get; }
    }
}
