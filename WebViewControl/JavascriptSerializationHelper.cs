using System;
using System.Collections;
using System.Linq;
using System.Web;
using JavascriptObject = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, object>>;
using SerializationHandler = System.Func<object, string>;

namespace WebViewControl {

    public static class JavascriptSerializer {

        public static string Serialize(object o, SerializationHandler handleComplexType = null) {
            if (o == null) return "null";
            if (o is string str) return Serialize(str);
            if (o is IEnumerable col) return Serialize(col);
            if (o is ValueType) return o.ToString().ToLowerInvariant();
            if (o is JavascriptObject jso) return Serialize(jso);
            if (handleComplexType != null) return handleComplexType(o);
            return o.ToString();
        }

        public static string Serialize(JavascriptObject o, SerializationHandler handleComplexType = null) {
            return "{" + string.Join(",", o.Select(kvp => Serialize(kvp.Key) + ":" + Serialize(kvp.Value, handleComplexType))) + "}";
        }

        public static string Serialize(string str) {
            return str == null ? "null" : HttpUtility.JavaScriptStringEncode(str, true);
        }

        public static string Serialize(bool boolean) {
            return boolean.ToString().ToLowerInvariant();
        }

        public static string Serialize(IEnumerable arr, SerializationHandler handleComplexType = null) {
            return "[" + string.Join(",", arr.Cast<object>().Select(i => Serialize(i, handleComplexType))) + "]";
        }
    }
}
