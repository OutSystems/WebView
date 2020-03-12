using WebViewControl;

namespace ReactViewControl {

    public class ReactViewFactory {

        /// <summary>
        /// The default stylesheet.
        /// </summary>
        public virtual ResourceUrl DefaultStyleSheet => null;

        /// <summary>
        /// Place plugins initialization here and return the plugins modules instances.
        /// </summary>
        /// <returns></returns>
        public virtual IViewModule[] InitializePlugins() => new IViewModule[0];

        /// <summary>
        /// Shows developers tools when the control is instantiated.
        /// </summary>
        public virtual bool ShowDeveloperTools => false;

        /// <summary>
        /// Developer tools become available pressing F12.
        /// </summary>
        public virtual bool EnableDebugMode => false;

        /// <summary>
        /// The view is cached and preloaded. First render occurs earlier.
        /// </summary>
        public virtual bool EnableViewPreload => true;

        /// <summary>
        /// Specifies the behavior of calls from javascript to native.
        /// If true, a javascript call to a native method will block until the native method returns.
        /// Defaults to false, calls can be executed in parallel.
        /// </summary>
        public virtual bool SyncNativeCalls => false;
    }
}
