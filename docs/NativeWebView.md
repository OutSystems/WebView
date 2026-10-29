> **Note:** this implementation now lives in `WebViewControl.Avalonia` of the WebView repository (it was developed as
> `WebViewControl.Native.Avalonia` in the ReactView repository). Paths and project names below that refer to ReactView or to
> `WebViewControl.Native.Avalonia` are historical.

# Native WebView support in ReactView (macOS / WKWebView)

Reference of everything that was done to make the Avalonia version of ReactView render with the **operating system's web view**
(WKWebView on macOS) instead of a bundled Chromium (CefGlue / `WebViewControl-Avalonia`), and of the problems found while running
ODC Studio / Service Studio (PhoenixIDE) on top of it.

> Status at the time of writing: macOS (ARM64) works end to end with the Sample and with Studio. **Windows has no implementation yet.**
> Some of the latest changes are only partially verified, see [Status and open items](#10-status-and-open-items).

---

## Contents

1. [Why and results](#1-why-and-results)
2. [Architecture](#2-architecture)
3. [What changed in the repository](#3-what-changed-in-the-repository)
4. [How it works](#4-how-it-works)
5. [Settings and features: mapping to WKWebView](#5-settings-and-features-mapping-to-wkwebview)
6. [Problems found and how they were fixed](#6-problems-found-and-how-they-were-fixed)
7. [Differences to the CefGlue based control (limitations)](#7-differences-to-the-cefglue-based-control-limitations)
8. [Diagnostics](#8-diagnostics)
9. [Testing](#9-testing)
10. [Status and open items](#10-status-and-open-items)
11. [PhoenixIDE (Studio) integration](#11-phoenixide-studio-integration)
12. [Building and running on an ARM64 Mac](#12-building-and-running-on-an-arm64-mac)

---

## 1. Why and results

The goal was to stop bundling Chromium (about 300 MB of binaries per platform) and to use what the OS already ships.

| | CefGlue (before) | Native WKWebView (now) |
|---|---|---|
| Sample build output (macOS ARM64, Debug) | 670 MB | 123 MB |
| Sample running: total RSS of all its processes | ~1,013 MB (7 processes) | ~326 MB (5 processes) |

* Sizes: `du -sh Sample.Avalonia/bin/ARM64/Debug/net8.0`, from a clean build, before and after. `libcef.dylib` alone was 169 MB and
  `CefGlueBrowserProcess` 101 MB; the remaining 123 MB is Avalonia's own native libraries and the app.
* Memory: summed `ps` RSS 25 s after launch. For CEF it is the process tree; for WKWebView the `com.apple.WebKit.*` processes that
  appeared after launch were added. RSS double counts shared pages, but the same method was used on both sides.

---

## 2. Architecture

The change keeps ReactView's shared sources almost untouched. Instead of editing ReactView, a **new project exposes the same public API
(`namespace WebViewControl`, same type names) that `WebViewControl-Avalonia` exposed**, implemented on top of WKWebView.
`ReactViewControl.Avalonia`, the Sample and the tests simply reference it instead of the NuGet package.

```
Sample.Avalonia / PhoenixIDE
        |
ReactViewControl.Avalonia            (shared sources with the WPF project, a few  #if NATIVE_WEBVIEW  blocks)
        |
WebViewControl.Native.Avalonia       <- NEW. Same API as WebViewControl-Avalonia
   |-- WebView (Avalonia Control)    public API: events, JS execution, object binding, resources, ...
   |-- NativeBrowserHost             NativeControlHost that hosts the native view inside an Avalonia window
   |-- INativeBrowser                one implementation per OS
   |      '-- MacBrowser             WKWebView, driven through the Objective-C runtime (no native shim, no NuGet package)
   '-- Resources/NativeBridge.js     JS side of the native <-> JS bridge, injected in every document
```

Key design decisions:

* **No native library and no NuGet package for macOS.** WKWebView is driven from C# through the Objective-C runtime
  (`objc_msgSend`, runtime-registered classes, blocks). WebKit is `dlopen`ed on first use.
* **Backend seam (`INativeBrowser` / `INativeBrowserOwner`)** so a WebView2 (Windows) backend can be added without touching `WebView`.
* **One bridge script for all backends**: `NativeBridge.js` talks to `webkit.messageHandlers` (WKWebView) or `chrome.webview` (WebView2).

---

## 3. What changed in the repository

### 3.1 New project: `WebViewControl.Native.Avalonia/`

(~4,500 lines; `net8.0`; the only package dependency is `Avalonia`.)

| Path | Role |
|---|---|
| `WebView.cs` | Core of the public `WebView` control: lifecycle, properties/settings, navigation, dispose. Implements `INativeBrowserOwner`. |
| `WebView.Scripts.cs` | Script queue and execution (`ExecuteScript*`, `EvaluateScript*`), JS to .NET object binding (`RegisterJavascriptObject`), messages from the page, JS context lifecycle, `WaitForResult` (synchronous wait). |
| `WebView.Callbacks.cs` | Resource pipeline (`BeforeResourceLoad`, embedded resources, redirects), navigation callbacks, popups, dialogs, authentication, certificate errors. |
| `WebView.Avalonia.cs` | Avalonia specifics: `Address` property, focus, UI thread helpers, `InitializeBrowser` (preload). |
| `WebView.JavascriptException.cs`, `WebViewExtensions.cs` | `WebView.JavascriptException`, `IsMainFrame`. |
| `Api/` | Public types ported from the WebView repo without any CEF dependency: `ResourceUrl`, `ResourceHandler`, `Request`, `ResourcesManager`, `GlobalSettings`, `EditCommands`, `JavascriptSerializer`, `ProxyAuthentication`, `RenderProcess*Exception`, delegates, ... |
| `Native/INativeBrowser.cs` | The backend contract: `INativeBrowser`, `INativeBrowserOwner`, `ResourceRequest`, `ResourceResponse` (including `ReadContent`). |
| `Native/NativeBrowserHost.cs` | `NativeControlHost` that creates/destroys the native view and reports the host size. |
| `Native/NativeBrowserFactory.cs` | Chooses the backend per OS (macOS only for now; others throw `PlatformNotSupportedException`). |
| `Native/NativeJson.cs`, `Native/NativeObject.cs` | JSON conversion and reflection-based dispatch of the calls coming from JavaScript. |
| `Native/NativeBridgeScript.cs` | Loads `Resources/NativeBridge.js` (embedded resource). |
| `Native/NativeTrace.cs`, `Native/NativeWatchdog.cs` | Diagnostics (see [8](#8-diagnostics)). |
| `Mac/Objc.cs` | Objective-C runtime interop: `objc_msgSend` overloads, `NSString`/`NSData`/`NSDictionary` helpers, runtime class builder, block invocation. |
| `Mac/MacBridgeClass.cs` | The Objective-C class `RVNativeWebViewBridge`, registered at runtime. One instance per web view is the `WKURLSchemeHandler`, `WKScriptMessageHandler`, `WKNavigationDelegate` and `WKUIDelegate`. |
| `Mac/MacBrowser.cs` | `INativeBrowser` implementation: creates and configures the `WKWebView`, scheme tasks, navigation, dev tools, hosting/parking. |
| `Mac/MacEventLoop.cs` | Nested `NSApplication` event loop slice used by synchronous waits. |
| `Resources/NativeBridge.js` | The JS bridge (see [4.2](#42-the-javascript-bridge)). |
| `README.md` | Short summary of the project. |

### 3.2 Changes to existing files

| File | Change |
|---|---|
| `ReactViewControl.Avalonia/ReactViewControl.Avalonia.csproj` | `ProjectReference` to the new project instead of the `WebViewControl-Avalonia` package; defines `NATIVE_WEBVIEW`; description updated. |
| `Sample.Avalonia/Sample.Avalonia.csproj`, `Tests.ReactView/Tests.ReactView.csproj` | Removed the `WebViewControl-Avalonia` package reference (the web view now comes through `ReactViewControl.Avalonia`). |
| `Directory.Packages.props` | Removed the `WebViewControl-Avalonia(-ARM64)` central versions. The WPF ones stay. |
| `ReactView.sln` | Added the new project (same configurations as `ReactViewControl.Avalonia`; edited by hand to keep the diff to 16 lines). |
| `ReactViewControl/ReactViewRender.cs` | `#if NATIVE_WEBVIEW`: no `Xilium.CefGlue` import / `KeyPressed` handler (the one CEF type that leaked into shared code); `DisableInputInteractions` sets `WebView.IsKeyboardInputDisabled`. |
| `ReactViewControl/ExecutionEngine.cs` | `#if NATIVE_WEBVIEW`: the synchronous `EvaluateMethod<T>` uses `WebView.WaitForResult` instead of `.Result` (see [4.5](#45-threading-and-synchronous-waits)). |
| `README.md` | New "Web view engine" section; notes the version rule only applies to the WPF/cef version. |
| `.gitignore` | `!WebViewControl.Native.Avalonia/Resources/NativeBridge.js`. The repo ignores `*.js`, which had silently hidden the bridge script from git (see [6](#6-problems-found-and-how-they-were-fixed)). |

The WPF project is unaffected: all shared-source edits are `#if NATIVE_WEBVIEW` blocks (it still compiles; checked with
`dotnet build ReactViewControl/ReactViewControl.csproj -p:EnableWindowsTargeting=true`).

---

## 4. How it works

### 4.1 Resources (`embedded://`, `custom://`, `local://`)

The three schemes are registered as `WKURLSchemeHandler`s (WKWebView cannot register `http`/`https`).
For each request `MacBrowser` creates a `MacResourceRequest` and `WebView` runs the same pipeline as before in a worker thread:

* `embedded://` : `ResourcesManager` lookup in the assemblies' embedded resources, then `BeforeResourceLoad`, then `ResourceLoadFailed` / unhandled
  exception when nothing responded (unless `IgnoreMissingResources`).
* `custom://` / `local://` : `BeforeResourceLoad` (ReactView forwards it to `CustomResourceRequested` handlers); `local://` also serves `LoadHtml`.
* The response (`ResourceResponse`) adds `Access-Control-Allow-*` headers (pages of one scheme are cross-origin to the others), appends
  `charset=utf-8` to text types, answers CORS preflights (`OPTIONS`) with 204 and unhandled requests with 404.
* **The content is always read from the start of the stream** (`ResourceResponse.ReadContent`, serialized with a lock): applications cache one
  stream per resource (Studio's `ImageStore`) and the cef handler always read from position 0.
* `Redirect(url)`: WKWebView does not follow redirects of custom schemes, so the redirect is resolved natively: `http(s)` targets are fetched
  with `HttpClient`, other targets go through the pipeline again (max 10 hops). The page keeps the original URL.

### 4.2 The JavaScript bridge

`Resources/NativeBridge.js` is injected (`WKUserScript`, document start, main frame only) before any page script. It provides the contract
the TypeScript loader already used:

* `cefglue.checkObjectBound(name)`: resolves when `window[name]` exists; otherwise asks native to bind it (and waits if the object is
  registered later).
* `window[name].method(...args)`: a proxy created by `__rv.defineObject`; every call posts `{t:'call'}` and returns a promise.
  Methods are the public instance methods of the registered .NET object, with the first letter in lower case.
* Page to native messages (`t`): `ctx` (JS context ready), `call`, `bind`, `res` (result of a script run by native), `jserr`
  (uncaught error / unhandled rejection), `console` (trace only), `f12`.
* Native to page (`window.__rv`): `defineObject`, `undefineObject`, `bound`, `resolve`, `reject`, `reply`, `fail`,
  `setKeyboardDisabled`, `setContextMenuDisabled`, `setDeveloperKeysEnabled`, `setTrace`.
* The benign `ResizeObserver loop ...` notice is **not** reported as an error (it is an `ErrorEvent` without an exception, repeats on
  every frame, and CEF never reported it).
* The script only runs in the top frame (WebView2 injects in every frame).

### 4.3 Script execution

`WebView.Scripts.cs` queues scripts until the document's JS context exists (`ctx` message), batches the ones queued together and sends
them with `evaluateJavaScript` (no completion block). Each script is wrapped so that errors and results come back through the bridge:

* fire and forget (`ExecuteScript*`): errors become `UnhandledAsyncException` (`JavascriptException`, with the function name and a parsed
  stack for both V8 and JavaScriptCore formats).
* with result (`EvaluateScript<T>`): the script is the body of a function, promises are awaited, the result is serialized to JSON and
  deserialized with `System.Text.Json` (`null` becomes `default`, or an empty array for array types). Optional timeout.
* A new document (`didCommitNavigation`) invalidates the context: running evaluations complete with `default`,
  `JavascriptContextReleased`/`Created` are raised, registered objects are defined again.

### 4.4 .NET objects callable from JavaScript

`RegisterJavascriptObject(name, object, interceptCall)` (sync and `Task<object>` interceptor overloads, like before):

* Arguments arrive as a JSON array and are converted to the parameter types (`System.Text.Json`, case-insensitive property names, structs,
  enums as numbers or names, `params`, `object` parameters become `long`/`double`/`string`/`object[]`/`Dictionary`).
* Results are serialized to JSON (enums as numbers). `Task` results are awaited; exceptions reject the promise with the message.
* Calls go through `interceptCall` (ReactView uses it to post to Studio's contextual dispatcher) and are tracked so that `Dispose` waits for the
  pending ones.

### 4.5 Threading and synchronous waits

* Every native call happens on the UI thread (`NSApplication` main thread == Avalonia UI thread). Callbacks coming from Objective-C are
  `[UnmanagedCallersOnly]` methods that catch every exception (a managed exception must never cross into Objective-C) and forward it to
  `UnhandledAsyncException`.
* WKWebView delivers **all** results through the UI thread. CEF delivered them on its own threads, so a sync wait could not deadlock there.
  The generated `ExecutionEngine.EvaluateMethod<T>` (sync, used for every TypeScript method that returns a value) is therefore handled by
  `WebView.WaitForResult`: on a background thread it waits normally; **on the UI thread it runs a nested `NSApplication` event loop**
  (`MacEventLoop.ProcessEvents`) until the result arrives (60 s timeout). Prefer the async variants.
* Studio runs presenter code on its own background "contextual dispatcher" thread, so the UI thread must stay free for results to arrive.

### 4.6 Hosting the native view in Avalonia

`NativeBrowserHost : NativeControlHost` creates the native view when it is attached to a window and returns it to Avalonia.
Three WebKit facts shaped this part (each one was measured, see [6](#6-problems-found-and-how-they-were-fixed)):

1. **WebKit does not render a view that is not visible**: not in a window, in a hidden ancestor, or with an empty frame. `visibilityState` is
   `hidden`, `requestAnimationFrame` never fires, timers are throttled and `ResizeObserver` is not delivered.
2. ReactView's loader waits for a paint (`requestAnimationFrame`) before sending `notifyViewLoaded`, and windows with `SizeToContent`
   (dialogs, tooltips) wait for the page to report its content size before they are shown.
3. Avalonia hides the native view of a host whose bounds are empty, and a host is empty until the page reports its size.

So `MacBrowser` implements this rule (`UpdateWebViewParent`):

* The `WKWebView` lives inside a **wrapper `NSView`** (`hostView`) that Avalonia hosts. Avalonia can size the wrapper freely (even 0x0).
* While the host is **not attached or has no size yet**, the `WKWebView` is **parked** in a tiny, transparent, click-through window at
  floating level in the middle of the screen (`parkingWindow`, 1x1 px, alpha 0, `ignoresMouseEvents`, all spaces/transient, not in the
  windows menu) and keeps the size it was preloaded with (`InitializeBrowser(w, h)`), so the page lays out, renders, and can report its size
  (this is what the CEF based control did implicitly).
* When the host gets a real size, the view moves into the wrapper and takes the host size; when the host becomes empty again or is detached
  it is parked again.

Measured placements (a 1x1 window is enough; a large window under another window is occluded and does not work; normal level is occluded by
the main window, floating level is not): see [9.3](#93-rendering-experiments).

### 4.7 Developer tools

`AllowDeveloperTools` sets `WKWebView.isInspectable` (Safari's Develop menu) **and** the private preference `_developerExtrasEnabled` on the
configuration **before** the view is created (the view works on a copy of the configuration, setting it afterwards has no effect).
`ShowDeveloperTools()` calls the private `_inspector.show`; WebKit attaches the inspector inside the view hierarchy (it shrinks the page), which
conflicts with the native host layout, so it is **detached into its own window** as soon as it becomes visible. F12 is detected by the bridge
(the native view receives key events directly) and toggles it. On a Mac keyboard F12 usually needs `fn`. Right-click "Inspect Element"
still attaches the inspector.

---

## 5. Settings and features: mapping to WKWebView

| Feature (WebViewControl API) | Implementation |
|---|---|
| `IsSecurityDisabled` (ReactView sets it on every view) | `WKPreferences._setWebSecurityEnabled:NO` at creation. Cross-origin `fetch`/XHR work without CORS, cross-origin images do not taint canvases. Must be set before the browser is created. |
| `AllowDeveloperTools`, `ShowDeveloperTools`, F12 | See [4.7](#47-developer-tools). `CloseDeveloperTools` calls `_inspector.close`. |
| `ZoomPercentage` | `WKWebView.pageZoom` (1 = 100%). |
| `Settings.UserAgent` | `customUserAgent`. |
| `Settings.BackgroundColor` | `underPageBackgroundColor` (when not white). |
| `Settings.EnableVideoAutoplay` | `mediaTypesRequiringUserActionForPlayback = none`. |
| `Settings.PersistCache` / `CachePath` | `PersistCache`: default data store; otherwise one shared non-persistent store (so views with the same domain share local storage). `CachePath` is ignored. |
| `ProxyAuthentication` | Answered in `didReceiveAuthenticationChallenge` (basic/digest/NTLM...), cached for the session; not repeated when it was already rejected (no loops). |
| `IgnoreCertificateErrors` | Server trust challenge answered with the server's trust. |
| `DisableBuiltinContextMenus` | `contextmenu` is prevented inside the page. |
| `IsKeyboardInputDisabled` (new) | The bridge swallows `keydown`/`keypress`/`keyup` (replaces ReactView's `KeyPressed` handler). |
| `IsHistoryDisabled` | Back/forward navigations are cancelled in `decidePolicyForNavigationAction`. |
| `BeforeNavigate` | `decidePolicyForNavigationAction`. |
| `PopupOpening` | `createWebViewWithConfiguration` returns nil and raises the event (otherwise opens the browser). |
| `EditCommands` | `cut:`/`copy:`/`paste:`/`selectAll:` actions, `execCommand` for undo/redo/delete. |
| `Navigated`, `LoadFailed`, `TitleChanged`, `Address` | `didFinish`/`didFail*` delegates (cancelled loads are ignored). |
| `RenderProcess*Exception` | `webViewWebContentProcessDidTerminate` raises `RenderProcessCrashedException`. |

---

## 6. Problems found and how they were fixed

Chronological, with the way each one was verified.

| # | Symptom | Cause | Fix | Verified by |
|---|---|---|---|---|
| 1 | Sample could not be built on an Apple-silicon Mac without Rosetta ("Bad CPU type in executable") | The ViewGeneratorCore NuGet package hard-codes an x64 Node 12 | Local workaround: `packages/node.js.redist/12.18.4/tools/osx-x64/node` -> symlink to the system Node (original kept as `node.x64.orig`). Not a repo change (`packages/` is ignored). | Sample builds and bundles |
| 2 | Developer tools did not open | `setInspectable:` only enables remote inspection. The local inspector needs the private `_developerExtrasEnabled` preference, set on the configuration **before** the view is created (`WKWebView.configuration` is a copy). Even then WebKit attaches the inspector inside the host and shrinks the page | Set both; detach the inspector into a window once visible | Window list showed a `_WKInspectorWindow` ("Web Inspector"); F12 path simulated in the page |
| 3 | In Studio, requests from `embedded://` pages to the server failed with the generic `Load failed` (icons, redirects missing) | `IsSecurityDisabled` (set by ReactView on every view) was ignored; WebKit enforced CORS | Map it to `_setWebSecurityEnabled:NO`. Added tracing of failed `fetch`/XHR with their URL (WebKit does not say which) | Checks: fetch without CORS fails with security on, works with it off; canvas tainted / not tainted |
| 4 | Some icons empty (`200 image/png 0 bytes`) | Studio's `ImageStore` caches one `ViewResource` (one stream) per image and serves it to every request. The first request consumed it; the cef handler always seeked to 0, mine read from the current position | `ResourceResponse.ReadContent`: seek to 0, serialized by a lock, dispose when asked | Check serving one cached non-`MemoryStream` stream to 6 concurrent requests: failed before (`0,5000,0,0,0,0`), passes now |
| 5 | `Error: root.getSelection is not a function` on every click/focus; Studio's crash UX appeared and dialogs/tooltips stopped | PhoenixIDE's `TextSelectionChangeEventHandler.ts` calls `shadowRoot.getSelection()`, a Chromium-only extension | Fixed in PhoenixIDE (TypeScript): fall back to the focused input/textarea of the shadow root, nested roots and `getComposedRanges`. Unit tests added | Jest tests fail on the old code with the exact error, pass with the fix |
| 6 | `Error: ResizeObserver loop completed with undelivered notifications.` reported as a `JavascriptException`, ~45 times per second | Browsers dispatch it as an `ErrorEvent` with no exception; the bridge forwarded every `error` event | Ignore it when there is no `error` object (real errors still reported) | Real ResizeObserver loop in the harness: 45 reports before, 0 after; genuine error still reported |
| 7 | **The bridge fix was invisible to git** | `.gitignore` has `*.js`; `NativeBridge.js` (an embedded resource the project cannot build without) was never tracked. A clean clone would not build | `.gitignore` exception | `git status` shows the file |
| 8 | Tooltips never appeared (`GetTooltip` never called); dialogs and other auto-sized windows did not show | See [4.6](#46-hosting-the-native-view-in-avalonia). A view that is not visible never renders, so the loader's `waitForNextPaint` (`requestAnimationFrame`) never completes, `notifyViewLoaded` is never sent and `ExecutionEngine` keeps `RefreshTooltipContent` queued forever. A preloaded view has no window; in the tooltip `Popup` the host is empty (12x12 popup, 0x0 host) and Avalonia hides it | Parking window + wrapper view + "host has a size" rule | Rendering experiments (9.3) and a harness reproducing the tooltip popup on Avalonia 11.3.22: `hidden`/0 frames/never loaded before, `visible`/60 fps/loaded after, and the size handoff (see status) |

### Findings that did not need a code change

* Studio's `DoSlowOperation` just runs the operation (not a gate). `DoVerySlowOperation` disables keyboard interactions while it runs;
  an operation that never completes would make later commands silently do nothing (see [10](#10-status-and-open-items), double-click on a screen).
* During a normal Studio start, trivial JS to .NET calls waited 3-11 s. They were all pending behind the modal login frame of the contextual
  dispatcher (by design) and finished.
* The repo's NUnit suite (`Tests.ReactView`) cannot run on macOS: it starts Avalonia on a background thread, which `AvaloniaNativePlatform`
  rejects. This is independent of the web view.

---

## 7. Differences to the CefGlue based control (limitations)

* **No Windows implementation** (`PlatformNotSupportedException`). A WebView2 backend can be added behind `INativeBrowser`.
* **x64 Macs**: the code path (`objc_msgSend_stret` for struct returns) exists but was never executed.
* **`ExternalResourceRequested` never fires**: `http(s)` cannot be intercepted by WKWebView. Anything the application serves for external URLs
  must use a custom scheme (see the UI Editor images plan in [11.3](#113-open-ui-editor-images)).
* **Request headers cannot be changed** (`SetHeader*` only change the copy handlers see). **Redirects** are resolved natively ([4.1](#41-resources-embedded-custom-local)).
* **Files/text drag events and downloads are not raised.**
* **JavaScript dialogs** (`alert/confirm/prompt`) are dismissed unless something subscribes to the (internal) `JavacriptDialogShown` event.
* **Synchronous waits on the UI thread** pump a nested event loop ([4.5](#45-threading-and-synchronous-waits)).
* **Keyboard events** go straight to the native view (they do not pass through Avalonia).
* **`OsrEnabled` and `CommandLineSwitches`** are accepted and ignored; `CachePath` is ignored.
* **Right-click "Inspect Element"** attaches the inspector inside the view.
* The engine is Safari's WebKit: expect rare CSS/JS differences, and the engine version follows the OS. (Two examples found: shadow-root selection,
  ResizeObserver notices.)
* Authenticating **proxies** use the same authentication path as servers; only server authentication was verified.

---

## 8. Diagnostics

Set `WEBVIEW_NATIVE_TRACE=1` (stderr). Every line is prefixed with the thread (`[UI]` or `[thread N]`):

* resource requests and responses (status, type, size), requests cancelled by the page;
* every message from the page, including `jserr` and page `console` output; failed `fetch`/XHR with their URL;
* authentication challenges, parking of web views;
* **window dump every 4 s**: each window (title, class, visible, key, frame) and, for visible windows, the view hierarchy down to the `WKWebView`
  (frame, hidden, URL, loading), which is how the "hidden ancestor" problem was found;
* **watchdog** (own thread): any JS to .NET call or script evaluation pending for more than 3 s, with the name of the call and whether the UI
  thread is responsive, and a line when it finishes. Quiet for quick calls.

---

## 9. Testing

The repository's NUnit tests cannot run on macOS ([6](#findings-that-did-not-need-a-code-change)), so verification was done with self-checking
harness programs kept **outside the repository** (scratch projects referencing the library; Avalonia on the main thread). They are not part of
the repo yet; turning them into a project is an open item.

### 9.1 Behaviour checks (47 at the last full run)

Evaluation (int, string, null to empty array, arrays, promises, JS exceptions, timeout); native objects (arguments, strings with unicode and
quotes, structs, `Task` results, rejection on exceptions, `void`, `params`, `double[]`, dictionaries, late registration resolving a pending bind);
**synchronous wait on the UI thread without deadlock** (plain and with a native round trip); error reporting (`ExecuteScript` errors, uncaught
errors, ResizeObserver notices ignored while real errors are reported); resources (custom scheme `fetch`, query strings, missing embedded resource,
mime types, shared stream served to 6 requests); redirects (custom to http, custom to embedded, redirect wins over a loaded resource);
authentication (denied without credentials, wrong credentials do not loop, correct credentials work, session shared); web security on/off;
zoom; title; reload creating a new context with objects bound again; scripts queued before the first context; dispose completing pending evaluations.

### 9.2 Other automated checks

* PhoenixIDE jest: 4 tests for the selection helper (3 fail on the old code with the exact error).
* The Sample builds and runs; its trace shows the full lifecycle (loader, bundles, calls in both directions, nested views, custom resources).

### 9.3 Rendering experiments

Page with `requestAnimationFrame` counters and `document.visibilityState`:

| Case | Result |
|---|---|
| Web view not in a window | `hidden`, 0 frames |
| Same view attached to a visible window | `visible`, ~60 frames/s |
| 1x1 window, alpha 0 or 0.01, behind or in front | `visible` |
| 1200x900 window under another window | `hidden` (occluded) |
| 1x1, floating level, screen center, under the main window | `visible` |
| 1x1, normal level, under the main window | `hidden` (occluded) |
| Preloaded view hosted in a `Popup` (empty host), Avalonia 11.3.22, **before** the wrapper/size rule | `hidden`, 0 frames, never "loaded" |
| Same, **after** | `visible`, ~60 frames/s, loaded; viewport follows the host size (240x60, 320x90), back to the preload size when the host is empty |

---

## 10. Status and open items

**Committed in the ReactView branch** (`974c349`, `be319e3`): the new project and the shared-source/solution/csproj changes, the developer-tools
fix, the security flag, the shared-stream read, the redirect/authentication support and the trace/window dump.

**Uncommitted at the time of writing** (check `git status`):
* `.gitignore` and `Resources/NativeBridge.js` (the whole JS bridge, including every bridge fix: ResizeObserver, top-frame only, F12, failed
  `fetch` tracing, function name in script errors). **Until these are committed the committed project does not build from a clean clone.**
* The watchdog (`Native/NativeWatchdog.cs`, tracking in `WebView.Scripts.cs`) and the thread labels in `NativeTrace.cs`.
* The **parking window + wrapper view + host-size rule** (`Mac/MacBrowser.cs`, `Mac/Objc.cs`, `Native/INativeBrowser.cs` (`OnHostBoundsChanged`
  now takes the size in device independent units, new `OnHostDetached`), `Native/NativeBrowserHost.cs`).
* This document.

In PhoenixIDE the changes of [11.1](#111-changes-in-phoenixide-branch-idetechdebtrdev-0000-nativewwebview) are also uncommitted.

Verification state of the latest work:

* The parking window and the wrapper/host-size rule were verified with the harness (preload case and the tooltip `Popup` reproduction on
  Avalonia 11.3.22).
* **Not yet re-run after the wrapper change**: the full 47-check suite and a Studio run. These were interrupted and must be repeated.
* **Not verified in Studio**: that tooltips and dialogs now appear and `getTooltip` is called; the Preferences dialog. (The tooltip and dialog
  mechanisms were reproduced and fixed in a harness; the interactive paths could not be driven from the automation used.)
* **Double-click on a screen node not opening the UI Editor** (`TreePresenter.OnDoubleClickNode` runs but `UIEditorProvider` is not reached):
  **cause not found.** Three silent early exits in the presenter chain and the watchdog were documented; the watchdog output of a failing
  attempt is needed. It may also be the same hidden-view problem (the UI Editor view is preloaded).
* **WebView2 (Windows) backend**: the decision was to implement it, but it was **not implemented**.
  A first attempt added the `Microsoft.Web.WebView2` package unconditionally; that was removed because macOS must not depend on any package.
  The intended design: `INativeBrowser` implementation reusing `NativeBridge.js`, the resource pipeline, the object binding and the script queue;
  `Microsoft.Web.WebView2.Core` only; custom scheme registrations for the three schemes; the package referenced only when building for Windows.
* **Packaging/versioning**: `Version` is 5.120.8 (the base was 5.120.7). Packages are consumed from a local NuGet feed. NuGet caches by version, so a
  rebuilt package with the same version needs the cached folders under `PhoenixIDE/packages/<id>/<version>` deleted. The README versioning rule
  (`<major>.<cef_version>.<patch>`) no longer applies to the Avalonia packages.
* Harness programs are not in the repository.

---

## 11. PhoenixIDE (Studio) integration

### 11.1 Changes in PhoenixIDE (branch `ide/techdebt/RDEV-0000-NativewWebView`)

| File | Change |
|---|---|
| `Directory.Build.props` | `OSReactViewControlAvaloniaVersion` and `OSWebViewControlAvaloniaVersion` point to the new build (5.120.8). |
| `Directory.Packages.props` | `WebViewControl-Avalonia(-ARM64)` replaced by `WebViewControl-Native-Avalonia(-ARM64)`. |
| `ServiceStudio.ViewImplementation.Framework.csproj` | References `WebViewControl-Native-Avalonia$(ViewPackageSuffix)` (the old package must not be referenced: both define `WebViewControl.*` and the old one brings back the CEF binaries). |
| `NuGet.config` | Local feed (`/Users/<user>/Documents/LocalRepository`) during development. |
| `Framework/Helpers/TextSelectionChangeEventHandler.ts` (+ new jest test) | Fix for `ShadowRoot.getSelection` ([6](#6-problems-found-and-how-they-were-fixed), #5). |

No other C# change was needed: every `WebViewControl.*` type Studio uses (`ResourceHandler`, `ResourceUrl`, `WebView.Settings`,
`GlobalWebViewInitialized`, `RenderProcess*Exception`, `ProxyAuthentication`...) exists in the new assembly.

### 11.2 Studio behaviours to know

* All JS to .NET calls are posted by `ExtendedReactView.OnNativeMethodCalled` to the **contextual dispatcher** (own thread, frames); results go back
  through the UI thread. A page exception is sent to `ExceptionHandling.OnException` with `FullyVisibleIfRecurrent`, which is why a recurring page
  error (#5) turned into the crash UX and stopped dialogs/tooltips.
* `DialogWindow.ShowWindow` only shows a window with `SizeToContent` after `ReactView.IsLayoutReady`; `TooltipService.InnerShowTooltip` queues
  `RefreshTooltipContent` and opens a `Popup` (see #8).
* Studio uses **Avalonia 11.3.22**; ReactView compiles against 11.0.10. Test with the Studio version when hosting behaviour matters.
* The debug hot-reload (`DEV_SERVER_URI`) uses `ResourceHandler.Redirect`; it now works through the native redirect resolution.

### 11.3 Open: UI Editor images

`UIEditorViewAdapter` serves images through `ExternalResourceRequested` (intercepting the runtime server URLs set as `<base>` of the editor's content
iframe), which cannot work on WKWebView. Proposed approach (not implemented):

1. ReactView: accept path style custom resource URLs (`custom://resource/<frame>/<path>`), so relative URLs resolve under a `<base>`.
2. PhoenixIDE: give the editor a custom-scheme base URL that carries the real server URL in its path; serve it through `CustomResourceRequested`
   (rebuild the real URL and call the existing `imageStreamRequested`), falling back to a normal HTTP fetch when the IDE has no stream.
3. Already verified: a relative image under a `custom://` `<base>` in an iframe reaches the handler, and (with `IsSecurityDisabled`) such images do
   not taint canvases (screenshots keep working).

---

## 12. Building and running on an ARM64 Mac

```bash
# ReactView
cd ReactView
dotnet build Sample.Avalonia/Sample.Avalonia.csproj -p:Platform=ARM64 -c Debug
WEBVIEW_NATIVE_TRACE=1 dotnet Sample.Avalonia/bin/ARM64/Debug/net8.0/Sample.Avalonia.dll

# pack the packages (GeneratePackageOnBuild) into ReactView/nuget
dotnet build ReactViewControl.Avalonia/ReactViewControl.Avalonia.csproj -p:Platform=ARM64 -c Release
#   -> ReactViewControl-Avalonia-ARM64.<version>.nupkg and WebViewControl-Native-Avalonia-ARM64.<version>.nupkg
```

* If the build fails with "Bad CPU type in executable" for `node`, see problem #1 (no Rosetta). Prerequisites: .NET 8 SDK, Node.
* Studio: `cd PhoenixIDE/ServiceStudio && WEBVIEW_NATIVE_TRACE=1 dotnet Build/Debug/ODCStudio.dll -debug`.
* After repacking a package with the same version, delete the cached `webviewcontrol-native-avalonia-arm64/<version>` and
  `reactviewcontrol-avalonia-arm64/<version>` folders from the consuming repo's `packages/` folder before restoring.
