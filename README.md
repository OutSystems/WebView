# WebView

WebView is a Avalonia/WPF control that wraps [CefGlue](https://github.com/OutSystems/CefGlue) webview control providing a better and simple API.
It also provides the following additional features:
- Strongly-typed javascript evaluation: results of javascript evaluation returns the appropriate type
- Scripts are aggregated and executed in bulk for improved performance
- Ability to evaluate javascript synchronously
- Javascript error handling with call stack information
- Events to intercept and respond to resources loading
- Events to track file download progress
- Ability to load embedded resources using custom protocols
- Ability to disable history navigation
- Error handling
- Proxy configuration support
- Runs under AnyCPU configuration (works both on x64 and x86 configurations)
- Option to run in [offscreen rendering mode](https://bitbucket.org/chromiumembedded/cef/wiki/GeneralUsage#markdown-header-off-screen-rendering)

# Getting Started

Check out the [Sample](SampleWebView.Avalonia) project to see how the WebView works.

# TODO
- Improve documentation
