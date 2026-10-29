# WebViewControl.Avalonia (native web view)

Avalonia `WebView` control that renders using the **web view of the operating system**, instead of bundling Chromium (CefGlue):

| OS      | Engine      | Status |
|---------|-------------|--------|
| macOS   | `WKWebView` | supported. Verified on ARM64; the x64 code path (different Objective-C struct-return ABI) is implemented but **not yet tested** |
| Windows | `WebView2`  | **not implemented yet** (`PlatformNotSupportedException` when a view is created) |
| Linux   | -           | not supported |

It exposes the same API (same `WebViewControl` namespace and type names) as the `WebViewControl-Avalonia` package that `ReactViewControl.Avalonia`
used before, so the code of ReactView, the sample and the tests did not need to change (apart from a few `#if NATIVE_WEBVIEW` blocks, see below).

## Why

|                                   | CefGlue (before) | Native (now) |
|-----------------------------------|------------------|--------------|
| Sample build output (macOS ARM64, Debug) | 670 MB    | 123 MB       |
| Memory of the running sample (RSS, all processes) | ~1,013 MB (7 processes) | ~326 MB (5 processes) |

The web engine ships with the operating system, so it is neither bundled nor loaded once per application.

## How it works

```
WebView (public API, Avalonia Control)
   ├── NativeBrowserHost : NativeControlHost       hosts the native view inside the Avalonia window
   └── INativeBrowser                              one implementation per operating system
        └── MacBrowser                             WKWebView, driven through the Objective-C runtime (no native shim library)
```

* **Resources**: `embedded://`, `custom://` and `local://` are registered as `WKURLSchemeHandler`s. Requests go through the same
  `BeforeResourceLoad` / `ResourceHandler` pipeline as before.
* **JavaScript -> .NET**: `Resources/NativeBridge.js` is injected in every document (before any page script). It provides the `cefglue.checkObjectBound`
  contract and the `window[name]` proxies of the objects registered with `RegisterJavascriptObject`. Calls are posted to a `WKScriptMessageHandler`
  and always return promises.
* **.NET -> JavaScript**: scripts are queued until the document's javascript context exists, sent in batches with `evaluateJavaScript`, and results
  (or errors, as `JavascriptException`) are posted back through the same bridge. Promises returned by scripts are awaited.
* **Threading**: all native calls happen in the UI thread. Objective-C classes (the delegate/handler object) are registered at runtime.

## Differences to the CefGlue based control

Things that behave differently or are not available (yet):

* **No Windows implementation** (a `WebView2` backend can be added behind `INativeBrowser`).
* **Synchronous waits on the UI thread** (`ExecutionEngine.EvaluateMethod<T>`, generated for methods returning values) are supported by running a nested
  application event loop while waiting, because WKWebView only delivers results through the UI thread. Prefer the async variants.
* `ExternalResourceRequested`: `http(s)` requests cannot be intercepted by WKWebView, so it never fires.
  Content that the application needs to serve itself must use one of the custom schemes (e.g. `CustomResourceRequested`).
* `IsSecurityDisabled` (set by ReactView on every view) disables WebKit's same origin policy, as `--disable-web-security` did with cef:
  cross origin `fetch`/XHR work without CORS headers and cross origin images do not taint canvases. It must be set before the browser is
  initialized. Without it, such requests fail with WebKit's generic `Load failed` error.
* `ResourceHandler.Redirect`: WKWebView does not follow redirects of custom schemes, so the redirect is resolved natively and its content is served
  in place of the requested url: `http(s)` targets are fetched (`HttpClient`), other targets go through the resources pipeline again. The page keeps the original url.
* Request headers cannot be changed (`SetHeader*` only changes the copy seen by handlers).
* `ProxyAuthentication` is provided to the http authentication challenges (as the cef based control did) and cached for the session, so it is shared by
  all the views. Verified with server authentication; authenticating *proxies* use the same path but were not verified. `IgnoreCertificateErrors` accepts untrusted server certificates.
* **Drag of files/text** (`FilesDragging`, `TextDragging`) and **downloads** events are not raised.
* JavaScript dialogs (`alert`/`confirm`/`prompt`) are dismissed unless something subscribes to the (internal) `JavacriptDialogShown` event.
* Keyboard events go directly to the native view (they do not pass through Avalonia). `ReactView` discards them inside the page while disabled.
* `GlobalSettings`: `OsrEnabled` and the command line switches are accepted but ignored. `CachePath` is ignored (WKWebView controls its storage location);
  with `PersistCache = false` a non persistent store shared by all the views is used.
* `Settings.UserAgent`, `BackgroundColor` and `EnableVideoAutoplay` are applied on a best-effort basis (not covered by checks); `ZoomPercentage` maps to `WKWebView.pageZoom`.
* Content is Safari's WebKit instead of Chromium: expect (rare) rendering/CSS/JS differences, and that the engine version follows the operating system.

## Developer tools

`AllowDeveloperTools` / `ShowDeveloperTools()` use WebKit's (private) inspector API, and open the Web Inspector in its **own window**
(by default WebKit attaches it inside the view, which conflicts with the layout of the native control host). F12 toggles it when `AllowDeveloperTools`
is set (on a Mac keyboard that usually requires holding `fn`). Right-click "Inspect Element" attaches the inspector inside the view: prefer `F12`/`ShowDeveloperTools()`.
The page is also inspectable from Safari's *Develop* menu.

## Diagnostics

Set the `WEBVIEW_NATIVE_TRACE=1` environment variable to write the bridge traffic (resource requests and responses, messages from the page,
page console output, failed `fetch`/XHR requests with their url, unhandled async exceptions) to the standard error.

## `#if NATIVE_WEBVIEW`

`ReactViewControl.Avalonia` defines `NATIVE_WEBVIEW`. The sources shared with the WPF version have three small conditional blocks:
the `CefKeyEvent` handler (keyboard blocking is done inside the page instead) and the synchronous wait of `ExecutionEngine.EvaluateMethod<T>`.
