using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JavascriptObject = System.Collections.Generic.Dictionary<string, object>;

namespace WebViewControl {

    internal class JavascriptSerializationHelper : IJavascriptSerialization {
        
        public string SerializeArray(IEnumerable<string> arr) {
            return "[" + string.Join(",", arr.Select(si => SerializeString(si))) + "]";
        }

        public string SerializeArray(IEnumerable<IJavascriptObject> arr) {
            return SerializeArray(arr.Select(o => o.ToJavascriptObject()));
        }

        public string SerializeArray(IEnumerable<JavascriptObject> arr) {
            return "[" + string.Join(",", arr.Select(o => SerializeJavascriptObject(o))) + "]";
        }

        // TODO JMN cef3 this should not be dynamic
        public string SerializeJavascriptObjectValue(object o) {
            if (o == null) return "null";
            if (o is ValueType) return o.ToString().ToLowerInvariant();
            if (o is string) return SerializeString((string)o);
            if (o is JavascriptObject) return SerializeJavascriptObject((JavascriptObject)o);
            if (o is IJavascriptObject) return SerializeJavascriptObject((IJavascriptObject)o);
            if (o is IJavascriptEnumValue) return ((IJavascriptEnumValue)o).ToJavascriptEnumValue();
            if (o is IEnumerable<string>) return SerializeArray((IEnumerable<string>)o);
            if (o is IEnumerable<JavascriptObject>) return SerializeArray((IEnumerable<JavascriptObject>)o);
            if (o is IEnumerable<IJavascriptObject>) return SerializeArray((IEnumerable<IJavascriptObject>)o);
            throw new ArgumentException("unexpected argument type: " + o.GetType().FullName);
        }

        public string SerializeJavascriptObject(IJavascriptObject o) {
            return SerializeJavascriptObject(o.ToJavascriptObject());
        }

        public string SerializeJavascriptObject(JavascriptObject o) {
            return "{" + string.Join(",", o.Select(kvp => SerializeString(kvp.Key) + ":" + SerializeJavascriptObjectValue(kvp.Value))) + "}";
        }

        public string SerializeString(string str) {
            return str == null ? "null" : "\"" + Regex.Escape(str).Replace("\"", "\\\"") + "\"";
        }

        public string SerializeBoolean(bool boolean) {
            return boolean.ToString().ToLowerInvariant();
        }

        public T[] ToArray<T, S>(JavascriptObject obj, Func<S, T> converter) {
            if (obj == null) {
                return new T[0];
            }
            var result = new T[obj.Count];
            for (int i = 0; i < obj.Count; i++) {
                result[i] = (T)obj[i.ToString()];
            }
            return result;
        }
    }
}
