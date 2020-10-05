# WebView

WebView is a Avalonia/WPF control that wraps [CefGlue](https://gitlab.com/joaompneves/cefglue) webview control.
Provides the following additional features:
- Strongly-typed javascript evaluation: results of javascript evaluation returns the appropriate type
- Scripts are aggregated and executed in bulk for improved performance
- Synchronous javascript evaluation
- Javascript error handling with stack information
- Events to intercept and respond to resource load
- Events to track file download progress
- Ability to load embedded resources using a custom protocol
- Ability to disable history navigation
- Error handling
- Proxy configuration support
- Runs under AnyCPU configuration (works both on x64 and x86 configurations)
- Option to run in offscreen mode 

# Build pre-requisites
- NodeJS

# TODO
- Improve documentation
