using System.Linq;
using WebViewControl;

namespace ReactViewControl {

    public class ReactViewFactory {

        public virtual ResourceUrl DefaultStyleSheet => null;

        public virtual IViewModule[] Plugins => new IViewModule[0];

        public virtual bool ShowDeveloperTools => false;

        public virtual bool EnableDebugMode => false;

        public virtual bool EnableViewPreload => true;
    }
}
