using WebViewControl;

namespace ReactViewControl {

    internal static class ViewModuleExtensions {

        public static string GetNativeObjectFullName(this IViewModule module, string frameName) {
            return "$" + (frameName == ReactViewRender.MainViewFrameName ? frameName : frameName + "$") + module.NativeObjectName;
        }
    }
}
