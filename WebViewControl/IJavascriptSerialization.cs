using System;
using System.Collections.Generic;
using JavascriptObject = System.Collections.Generic.Dictionary<string, object>;

namespace WebViewControl {
    interface IJavascriptSerialization {

        string SerializeArray(IEnumerable<IJavascriptObject> arr);
        string SerializeArray(IEnumerable<JavascriptObject> arr);
        string SerializeArray(IEnumerable<string> arr);
        string SerializeJavascriptObject(IJavascriptObject o);
        string SerializeJavascriptObject(JavascriptObject o);
        string SerializeJavascriptObjectValue(object o);
        string SerializeString(string str);
        string SerializeBoolean(bool boolean);
        T[] ToArray<T, S>(JavascriptObject obj, Func<S, T> converter);
    }
}
