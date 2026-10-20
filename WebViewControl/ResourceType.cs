namespace WebViewControl {

    // values match the Chromium resource types (CefResourceType), so that they can be cast between both
    public enum ResourceType {
        MainFrame = 0,
        SubFrame = 1,
        Stylesheet = 2,
        Script = 3,
        Image = 4,
        FontResource = 5,
        SubResource = 6,
        Xhr = 13,
        ServiceWorker = 15
    }
}
