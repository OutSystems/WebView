using System.Collections;
using System.Collections.Generic;
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
            if (o is JavascriptObject jso) return Serialize(jso);
            if (o.GetType().IsPrimitive) return o.ToString().ToLowerInvariant(); // ints, bools, ... but not structs
            if (handleComplexType != null) return handleComplexType(o);
            return SerializeComplexType(o);
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

        private static string SerializeComplexType(object obj) {
            var fields = obj.GetType().GetProperties().Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string));
            // order members to create a stable serialization
            return Serialize(fields.OrderBy(f => f.Name).Select(f => new KeyValuePair<string, object>(f.Name, f.GetValue(obj, null))));
        }

        internal static string GetJavascriptName(string str) {
            if (string.IsNullOrEmpty(str)) {
                return string.Empty;
            }

            return char.ToLowerInvariant(str[0]) + str.Substring(1);
        }
    }
}
