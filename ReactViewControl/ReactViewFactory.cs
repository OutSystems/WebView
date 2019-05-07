using System.Linq;
using WebViewControl;

namespace ReactViewControl {

    public class ReactViewFactory {

        public virtual ResourceUrl DefaultStyleSheet => null;

        public virtual IViewModule[] Plugins => Enumerable.Empty<IViewModule>().ToArray();

        public virtual bool ShowDeveloperTools => false;

        public virtual bool EnableDebugMode => false;
    }
}
