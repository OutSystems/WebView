using ReactViewControl;

namespace Example.Avalonia {

    public abstract class ExtendedReactView : ReactView {

        protected override ReactViewFactory Factory => new ExtendedReactViewFactory();

        public ExtendedReactView(IViewModule mainModule) : base(mainModule) {
            Settings.StylePreferenceChanged += OnStylePreferenceChanged;
        }

        protected override void InnerDispose() {
            base.InnerDispose();
            Settings.StylePreferenceChanged -= OnStylePreferenceChanged;
        }

        private void OnStylePreferenceChanged() {
            RefreshDefaultStyleSheet();
        }
    }
}
