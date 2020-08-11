using System;
using ReactViewControl;

namespace Example {

    public abstract class ExtendedReactView : ReactView {

        protected override ReactViewFactory Factory => new ExtendedReactViewFactory();

        public ExtendedReactView(IViewModule mainModule) : base(mainModule) {
            Settings.StylePreferenceChanged += OnColorPreferenceChanged;
        }

        protected override void InnerDispose() {
            base.InnerDispose();
            Settings.StylePreferenceChanged -= OnColorPreferenceChanged;
        }

        private void OnColorPreferenceChanged() {
            RefreshDefaultStyleSheet();
        }
    }
}