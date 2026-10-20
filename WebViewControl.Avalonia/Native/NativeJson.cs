using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebViewControl {

    /// <summary>
    /// JSON conversions between the javascript bridge of the native engines and .NET.
    /// </summary>
    internal static class NativeJson {

        // .NET -> javascript: members are written with their declared names (the generated view contracts already use the javascript casing)
        private static JsonSerializerOptions WriteOptions { get; } = new() {
            IncludeFields = true
        };

        // javascript -> .NET: lenient on casing, numbers in strings and enums by name
        private static JsonSerializerOptions ReadOptions { get; } = new() {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string Serialize(object value) {
            return value == null ? "null" : JsonSerializer.Serialize(value, value.GetType(), WriteOptions);
        }

        public static T Deserialize<T>(string json) {
            if (json == null) {
                return default;
            }
            using (var document = JsonDocument.Parse(json)) {
                return (T)Convert(document.RootElement, typeof(T));
            }
        }

        public static object Convert(JsonElement value, Type type) {
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined) {
                return type.IsValueType && Nullable.GetUnderlyingType(type) == null ? Activator.CreateInstance(type) : null;
            }
            if (type == typeof(object)) {
                return ToPlainObject(value);
            }
            if (type == typeof(string) && value.ValueKind != JsonValueKind.String) {
                return value.GetRawText();
            }
            return value.Deserialize(type, ReadOptions);
        }

        /// <summary>
        /// Converts to strings, numbers, booleans, arrays (object[]) and objects (dictionaries).
        /// </summary>
        private static object ToPlainObject(JsonElement value) {
            switch (value.ValueKind) {
                case JsonValueKind.String:
                    return value.GetString();
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out var intValue)) {
                        return intValue;
                    }
                    if (value.TryGetInt64(out var longValue)) {
                        return longValue;
                    }
                    return value.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Array:
                    return value.EnumerateArray().Select(ToPlainObject).ToArray();
                case JsonValueKind.Object:
                    return value.EnumerateObject().ToDictionary(p => p.Name, p => ToPlainObject(p.Value));
                default:
                    return null;
            }
        }
    }
}
