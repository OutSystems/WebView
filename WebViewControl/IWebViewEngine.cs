using System;
using System.Threading.Tasks;

namespace WebViewControl {

    /// <summary>
    /// The seam between <see cref="WebView"/> and the browser engine that renders it.
    /// Engines: CEF (bundled Chromium, through CefGlue), WKWebView (macOS system WebKit) and WebView2 (Windows system Edge runtime).
    /// Engines report back to the owning <see cref="WebView"/> through its internal Handle* methods.
    /// </summary>
    internal partial interface IWebViewEngine : IDisposable {

        bool IsBrowserInitialized { get; }

        bool IsJavascriptEngineInitialized { get; }

        bool IsLoading { get; }

        string Address { get; }

        string Title { get; }

        bool CanGoBack { get; }

        bool CanGoForward { get; }

        /// <summary>
        /// Zoom percentage, where 1 means 100%.
        /// </summary>
        double ZoomPercentage { get; set; }

        /// <summary>
        /// Creates the underlying browser, if not created yet. Engines also create the browser on their own when the view is first shown.
        /// </summary>
        void CreateBrowser(int width, int height);

        /// <summary>
        /// Navigates the main frame. Navigations requested before the browser is created are performed once it is.
        /// </summary>
        void Navigate(string url);

        void GoBack();

        void GoForward();

        void Reload(bool ignoreCache);

        void ShowDeveloperTools();

        void CloseDeveloperTools();

        void SetAccessibilityState(bool enabled);

        void ExecuteEditCommand(EditCommand command);

        void Focus();

        /// <summary>
        /// Returns the frame with the specified name (main frame when the name is null or empty), or null when not found.
        /// </summary>
        IJavascriptFrame GetFrame(string frameName);

        /// <summary>
        /// Returns the names of the child frames.
        /// </summary>
        string[] GetFrameNames();

        bool IsJavascriptObjectRegistered(string name);

        /// <summary>
        /// Exposes the public methods of <paramref name="objectToBind"/> in the window object of the main frame, under <paramref name="name"/>.
        /// Every call goes through <paramref name="interceptCall"/>, which may return a <see cref="Task"/>.
        /// </summary>
        void RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall);

        void UnregisterJavascriptObject(string name);
    }

    /// <summary>
    /// A frame where javascript can be evaluated.
    /// </summary>
    internal interface IJavascriptFrame {

        string Name { get; }

        string Url { get; }

        bool IsMain { get; }

        bool IsValid { get; }

        /// <summary>
        /// Evaluates the script as the body of a function and returns its result. A thrown error faults the task with an exception whose
        /// message is the string representation of the thrown value. A timeout faults the task with a <see cref="TaskCanceledException"/>.
        /// </summary>
        Task<T> EvaluateScript<T>(string script, TimeSpan? timeout);

        void LoadUrl(string url);
    }

    internal enum EditCommand {
        Cut,
        Copy,
        Paste,
        SelectAll,
        Undo,
        Redo,
        Delete
    }

    internal enum RenderProcessTerminationStatus {
        Crashed,
        Killed,
        OutOfMemory,
        Unknown
    }

    /// <summary>
    /// Engine agnostic details of a key press.
    /// </summary>
    internal sealed class KeyEventDetails {

        public KeyEventDetails(int keyCode, char character) {
            KeyCode = keyCode;
            Character = character;
        }

        /// <summary>
        /// Windows virtual key code.
        /// </summary>
        public int KeyCode { get; }

        public char Character { get; }
    }
}
