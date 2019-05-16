namespace WebViewControl {

    public enum ResourceType {
        Stylesheet = CefSharp.ResourceType.Stylesheet,
        Script = CefSharp.ResourceType.Script,
        Image = CefSharp.ResourceType.Image,
        FontResource = CefSharp.ResourceType.FontResource,
        Xhr = CefSharp.ResourceType.Xhr,
        ServiceWorker = CefSharp.ResourceType.ServiceWorker
    }
}
