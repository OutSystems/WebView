using System;
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
        /// Webpack dev server url. Setting this value will enable hot reload. eg: new Uri("http://localhost:8080")
        /// </summary>
        public virtual Uri DevServerURI => null;

        /// <summary>
        /// When true, native methods called from javascript will block and wait until the native method returns.
        /// Use this setting to prevent reentrancy, ie, native calls will be executed sequentially and one at a time.
        /// Defaults to false, calls can be executed in parallel and reentrancy can occur.
        /// </summary>
        public virtual bool ForceNativeSyncCalls => false;
    }
}
