using System;
using Xilium.CefGlue;

namespace WebViewControl {

    internal partial class ChromiumBrowser {

        internal void CreateBrowser(IntPtr? hostViewHandle = null) {
            CreateBrowser(1, 1, hostViewHandle);
        }

        internal CefBrowser GetBrowser() => UnderlyingBrowser;
    }
}
