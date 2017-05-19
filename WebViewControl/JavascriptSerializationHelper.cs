using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JavascriptObject = System.Collections.Generic.Dictionary<string, object>;

namespace WebViewControl {

    public interface IJavascriptObject {
        JavascriptObject ToJavascriptObject();
    }

    public interface IJavascriptEnumValue {
        string ToJavascriptEnumValue();
    }

    public static class JavascriptSerializer {

        public static string Serialize(IEnumerable<string> arr) {
            return "[" + string.Join(",", arr.Select(si => Serialize(si))) + "]";
        }

        public static string Serialize(IEnumerable<IJavascriptObject> arr) {
            return Serialize(arr.Select(o => o.ToJavascriptObject()));
        }

        public static string Serialize(IEnumerable<JavascriptObject> arr) {
            return "[" + string.Join(",", arr.Select(o => Serialize(o))) + "]";
        }

        // TODO this should not be dynamic
        public static string SerializeJavascriptObject(object o) {
            if (o == null) return "null";
            if (o is ValueType) return o.ToString().ToLowerInvariant();
            if (o is string) return Serialize((string)o);
            if (o is JavascriptObject) return Serialize((JavascriptObject)o);
            if (o is IJavascriptObject) return Serialize((IJavascriptObject)o);
            if (o is IJavascriptEnumValue) return ((IJavascriptEnumValue)o).ToJavascriptEnumValue();
            if (o is IEnumerable<string>) return Serialize((IEnumerable<string>)o);
            if (o is IEnumerable<JavascriptObject>) return Serialize((IEnumerable<JavascriptObject>)o);
            if (o is IEnumerable<IJavascriptObject>) return Serialize((IEnumerable<IJavascriptObject>)o);
            throw new ArgumentException("unexpected argument type: " + o.GetType().FullName);
        }

        public static string Serialize(IJavascriptObject o) {
            return Serialize(o.ToJavascriptObject());
        }

        public static string Serialize(JavascriptObject o) {
            return "{" + string.Join(",", o.Select(kvp => Serialize(kvp.Key) + ":" + SerializeJavascriptObject(kvp.Value))) + "}";
        }

        public static string Serialize(string str) {
            return str == null ? "null" : "\"" + Regex.Escape(str).Replace("\"", "\\\"") + "\"";
        }

        public static string Serialize(bool boolean) {
            return boolean.ToString().ToLowerInvariant();
        }
    }
}
