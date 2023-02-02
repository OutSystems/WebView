using Xilium.CefGlue;

namespace WebViewControl {

    public enum ResourceType {
        Stylesheet = CefResourceType.Stylesheet,
        Script = CefResourceType.Script,
        Image = CefResourceType.Image,
        FontResource = CefResourceType.FontResource,
        Xhr = CefResourceType.Xhr,
        ServiceWorker = CefResourceType.ServiceWorker
    }
}
