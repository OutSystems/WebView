using System;
using System.Collections.Generic;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class JsObjectBinder : DefaultBinder {

        private readonly ReactView reactView;

        public JsObjectBinder(ReactView reactView) : base(new DefaultFieldNameConverter()) {
            this.reactView = reactView;
        }

        public override object Bind(object obj, Type modelType) {
            if (modelType.IsInterface) {
                // TODO check if its a ViewObject
                // int .. might not be enough
                // trackcode
                var trackCode = (int)((Dictionary<string, object>)obj)["Value"];
                return reactView.GetTrackedObject(trackCode);
            }
            return base.Bind(obj, modelType);
        }
    }
}
