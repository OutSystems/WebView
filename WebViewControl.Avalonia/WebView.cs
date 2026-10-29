#pragma warning disable CS0067, CS0414 // events kept for API compatibility but not raised yet

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using WebViewControl.Native;

namespace WebViewControl {

    public partial class WebView : BaseControl, IDisposable, INativeBrowserOwner {

        private const string MainFrameName = null;

        private object SyncRoot { get; } = new object();

        private INativeBrowser browser;
        private NativeBrowserHost host;
        private bool isDeveloperToolsOpened;
        private Action pendingInitialization;
        private string htmlToLoad;
        private volatile bool isDisposing;
        private bool isKeyboardInputDisabled;

        private CountdownEvent JavascriptPendingCalls { get; } = new CountdownEvent(1);

        private CancellationTokenSource AsyncCancellationTokenSource { get; } = new CancellationTokenSource();

        public event Action WebViewInitialized;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event BeforeNavigateEventHandler BeforeNavigate;

        /// <summary>
        /// NOTE: This event is not executed on the UI thread. Do not use Dipatcher.Invoke (use BeginInvoke) while in the context of this event otherwise it may lead to a dead-lock.
        /// </summary>
        public event BeforeResourceLoadEventHandler BeforeResourceLoad;

        public event NavigatedEventHandler Navigated;
        public event LoadFailedEventHandler LoadFailed;
        public event ResourceLoadFailedEventHandler ResourceLoadFailed;

        // NOTE: downloads are not supported by the native web views yet, the events are kept for API compatibility
        public event DownloadProgressChangedEventHandler DownloadProgressChanged;
        public event DownloadStatusChangedEventHandler DownloadCompleted;
        public event DownloadStatusChangedEventHandler DownloadCancelled;

        public event JavascriptContextCreatedEventHandler JavascriptContextCreated;
        public event Action TitleChanged;
        public event UnhandledAsyncExceptionEventHandler UnhandledAsyncException;
        public event Action</*url*/string> PopupOpening;

        internal event Action Disposed;
        internal event JavascriptContextReleasedEventHandler JavascriptContextReleased;
        internal event JavacriptDialogShowEventHandler JavacriptDialogShown;

        // NOTE: dragging of files/text is not supported by the native web views yet, the events are kept for API compatibility
        internal event FilesDraggingEventHandler FilesDragging;
        internal event TextDraggingEventHandler TextDragging;

        private static int domainId = 1;

        // the web view keeps the same zoom level for all the pages under the same domain
        // having different domains will prevent synced zoom
        private string CurrentDomainId { get; }

        private string DefaultLocalUrl { get; }

        /// <summary>
        /// Executed when a web view is initialized. Can be used to attach or configure the webview before it's ready.
        /// </summary>
        public static event Action<WebView> GlobalWebViewInitialized;

        public static GlobalSettings Settings { get; } = new GlobalSettings();

        public WebView() : this(false) { }

        /// <param name="useSharedDomain">Shared domains means that the webview default domain will always be the same. When <paramref ref="useSharedDomain"/> is false a
        /// unique domain is used for every webview.</param>
        internal WebView(bool useSharedDomain) {
            if (useSharedDomain) {
                CurrentDomainId = string.Empty;
            } else {
                CurrentDomainId = Interlocked.Increment(ref domainId).ToString();
            }

            DefaultLocalUrl = UrlHelper.DefaultLocalUrl.WithDomain(CurrentDomainId);

            Initialize();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Initialize() {
            // from now on the global settings cannot be changed
            Settings.IsLoaded = true;

            browser = NativeBrowserFactory.Create(this);
            host = new NativeBrowserHost(this, browser);
            EditCommands = new EditCommands(this);

            ExtraInitialize();

            GlobalWebViewInitialized?.Invoke(this);
        }

        partial void ExtraInitialize();

        ~WebView() {
            InnerDispose();
        }

        public void Dispose() {
            InnerDispose();
            GC.SuppressFinalize(this);
        }

        private void InnerDispose() {
            lock (SyncRoot) {
                if (isDisposing) {
                    return;
                }
                isDisposing = true;
            }

            var disposed = false;

            void InternalDispose() {
                if (disposed) {
                    return; // bail-out
                }

                disposed = true;

                AsyncCancellationTokenSource.Cancel();

                WebViewInitialized = null;
                BeforeNavigate = null;
                BeforeResourceLoad = null;
                Navigated = null;
                LoadFailed = null;
                DownloadProgressChanged = null;
                DownloadCompleted = null;
                DownloadCancelled = null;
                JavascriptContextCreated = null;
                TitleChanged = null;
                UnhandledAsyncException = null;
                JavascriptContextReleased = null;

                // complete whatever is waiting for the page before releasing the browser
                AbortPendingScripts();

                AsyncCancellationTokenSource.Dispose();

                // the native objects can only be released in the UI thread (the finalizer thread is not)
                var nativeBrowser = browser;
                RunInUI(() => nativeBrowser.Dispose());

                Disposed?.Invoke();
            }

            if (JavascriptPendingCalls.CurrentCount > 1) {
                // avoid dead-lock, wait for all pending calls to finish
                Task.Run(() => {
                    JavascriptPendingCalls.Signal(); // remove dummy entry
                    JavascriptPendingCalls.Wait();
                    InternalDispose();
                });
                return;
            }

            InternalDispose();
        }

        internal bool IsDisposing => isDisposing;

        public bool AllowDeveloperTools {
            get => browser.AreDeveloperToolsAllowed;
            set {
                browser.AreDeveloperToolsAllowed = value;
                ApplyDocumentSettings();
            }
        }

        private string InternalAddress {
            get => browser.Address;
            set {
                if (browser.Address != value) {
                    LoadUrl(value, MainFrameName);
                }
            }
        }

        public EditCommands EditCommands { get; private set; }

        public bool CanGoBack => browser.CanGoBack;

        public bool CanGoForward => browser.CanGoForward;

        public void GoBack() => browser.GoBack();

        public void GoForward() => browser.GoForward();

        /// <summary>
        /// Disables the same origin policy of the pages (cross origin requests are allowed). Must be set before the browser is initialized.
        /// </summary>
        public bool IsSecurityDisabled {
            get => browser.IsWebSecurityDisabled;
            set => browser.IsWebSecurityDisabled = value;
        }

        public bool IgnoreCertificateErrors { get; set; }

        public bool IsHistoryDisabled { get; set; }

        public TimeSpan? DefaultScriptsExecutionTimeout { get; set; }

        private bool disableBuiltinContextMenus;

        public bool DisableBuiltinContextMenus {
            get => disableBuiltinContextMenus;
            set {
                disableBuiltinContextMenus = value;
                ApplyDocumentSettings();
            }
        }

        public bool DisableFileDialogs { get; set; }

        public bool IsBrowserInitialized => browser.IsCreated;

        public bool IsJavascriptEngineInitialized => contextReady;

        public ProxyAuthentication ProxyAuthentication { get; set; }

        public bool IgnoreMissingResources { get; set; }

        public string Title => browser.Title;

        /// <summary>
        /// When enabled the keyboard events are not delivered to the page.
        /// </summary>
        public bool IsKeyboardInputDisabled {
            get => isKeyboardInputDisabled;
            set {
                isKeyboardInputDisabled = value;
                ApplyDocumentSettings();
            }
        }

        private double zoomPercentage = 1;

        public double ZoomPercentage {
            get => browser.IsCreated ? browser.ZoomFactor : zoomPercentage;
            set {
                zoomPercentage = value;
                ExecuteWhenInitialized(() => browser.ZoomFactor = value);
            }
        }

        public void ShowDeveloperTools() {
            ExecuteWhenInitialized(() => {
                browser.ShowDeveloperTools();
                isDeveloperToolsOpened = true;
            });
        }

        public void CloseDeveloperTools() {
            if (isDeveloperToolsOpened) {
                RunInUI(() => browser.CloseDeveloperTools());
                isDeveloperToolsOpened = false;
            }
        }

        private void ToggleDeveloperTools() {
            if (isDeveloperToolsOpened) {
                CloseDeveloperTools();
            } else {
                ShowDeveloperTools();
            }
        }

        public void LoadUrl(string address, string frameName = MainFrameName) {
            if (!this.IsMainFrame(frameName)) {
                throw new NotSupportedException("Loading urls in frames other than the main frame is not supported by the native web view");
            }

            if (address != DefaultLocalUrl) {
                htmlToLoad = null;
            }
            ExecuteWhenInitialized(() => browser.LoadUrl(address));
        }

        public void LoadResource(ResourceUrl resourceUrl, string frameName = MainFrameName) {
            LoadUrl(resourceUrl.WithDomain(CurrentDomainId), frameName);
        }

        public void LoadHtml(string html) {
            htmlToLoad = html;
            LoadUrl(DefaultLocalUrl, MainFrameName);
        }

        public void Reload(bool ignoreCache = false) {
            RunInUI(() => {
                if (IsBrowserInitialized && !browser.IsLoading) {
                    browser.Reload(ignoreCache);
                }
            });
        }

        /// <summary>
        /// Creates the underlying native web view, if it was not created yet. The native view is created when the control is
        /// attached to a window, preloading can be used to create it before that happens.
        /// </summary>
        internal void EnsureBrowserCreated(int width, int height) {
            if (isDisposing || browser.IsCreated) {
                return;
            }

            browser.Create(width, height);

            if (browser.IsCreated) {
                RunPendingInitialization();
            }
        }

        private void RunPendingInitialization() {
            Action pending;
            lock (SyncRoot) {
                pending = pendingInitialization;
                pendingInitialization = null;
            }
            if (pending != null) {
                ExecuteWithAsyncErrorHandling(pending);
            }
        }

        void INativeBrowserOwner.OnBrowserInitialized() {
            // when the creation is asynchronous, this is when what was waiting for the browser can run
            RunPendingInitialization();
            AsyncExecuteInUI(() => WebViewInitialized?.Invoke());
        }

        private void ExecuteWhenInitialized(Action action) {
            void Run() {
                if (isDisposing) {
                    return;
                }
                if (browser.IsCreated) {
                    action();
                    return;
                }
                lock (SyncRoot) {
                    pendingInitialization += action;
                }
            }

            RunInUI(Run);
        }

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandling(Action action) => ExecuteWithAsyncErrorHandlingOnFrame(action, null);

        [DebuggerNonUserCode]
        private void ExecuteWithAsyncErrorHandlingOnFrame(Action action, string frameName) {
            try {
                action();
            } catch (Exception e) {
                ForwardUnhandledAsyncException(e, frameName);
            }
        }

        private void ForwardUnhandledAsyncException(Exception e, string frameName = null) {
            if (isDisposing) {
                return;
            }

            var handled = false;

            var unhandledAsyncException = UnhandledAsyncException;
            if (unhandledAsyncException != null) {
                var eventArgs = new UnhandledAsyncExceptionEventArgs(e, frameName);
                unhandledAsyncException(eventArgs);
                handled = eventArgs.Handled;
            }

            if (!handled) {
                ForwardException(ExceptionDispatchInfo.Capture(e));
            }
        }

        private void ForwardException(ExceptionDispatchInfo exceptionInfo) {
            // same as the cef based web view: unhandled async exceptions that no one handled are not propagated
            NativeTrace.Write("unhandled async exception: " + exceptionInfo.SourceException);
        }

        protected virtual string GetRequestUrl(string url, ResourceType resourceType) => url;

        /// <summary>
        /// Called when the webview has received focus.
        /// </summary>
        protected virtual void OnGotFocus() { }

        /// <summary>
        /// Called when the webview is about to loose focus.
        /// </summary>
        protected virtual void OnLostFocus() { }

        /// <summary>
        /// Enables or disable support for accessibility tools like screen readers.
        /// The native web views always integrate with the platform's accessibility services.
        /// </summary>
        public void EnableAccessibility(bool enable) { }
    }
}
