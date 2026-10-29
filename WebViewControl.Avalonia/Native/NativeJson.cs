using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebViewControl.Native {

    /// <summary>
    /// Json conversion of the values exchanged between .net and javascript.
    /// </summary>
    internal static class NativeJson {

        // parameters coming from javascript: be lenient (property names casing, enums as numbers or names, numbers as strings)
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: true), new InferredObjectConverter() }
        };

        // values sent to javascript: enums are numbers (as in the typescript enums), names are kept as defined in .net
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions {
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            // the objects returned to javascript may reference each other (parent/child links, ...): a back reference is written as null
            // instead of failing the whole call ("A possible object cycle was detected")
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            MaxDepth = 256
        };

        public static string Serialize(object value) {
            if (value == null) {
                return "null";
            }
            return JsonSerializer.Serialize(value, value.GetType(), WriteOptions);
        }

        public static object Deserialize(JsonElement element, Type type) {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined) {
                return type.IsValueType ? Activator.CreateInstance(type) : null;
            }
            return element.Deserialize(type, ReadOptions);
        }

        /// <summary>
        /// Converts the result of a script evaluation: null (or undefined) becomes the default value of the type (an empty array for array types).
        /// </summary>
        public static T DeserializeResult<T>(string json) {
            var targetType = typeof(T);
            if (string.IsNullOrWhiteSpace(json) || json == "null") {
                if (targetType.IsArray) {
                    return (T)(object)Array.CreateInstance(targetType.GetElementType(), 0);
                }
                return default;
            }
            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }

        /// <summary>
        /// Reads json values into the closest .net type, when the target type is <see cref="object"/>.
        /// </summary>
        private sealed class InferredObjectConverter : JsonConverter<object> {

            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                switch (reader.TokenType) {
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.Number:
                        return reader.TryGetInt64(out var integer) ? (object)integer : reader.GetDouble();
                    case JsonTokenType.String:
                        return reader.TryGetDateTime(out var date) ? (object)date : reader.GetString();
                    case JsonTokenType.StartArray:
                        var list = new List<object>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
                            list.Add(Read(ref reader, typeof(object), options));
                        }
                        return list.ToArray();
                    case JsonTokenType.StartObject:
                        var dictionary = new Dictionary<string, object>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                            var key = reader.GetString();
                            reader.Read();
                            dictionary[key] = Read(ref reader, typeof(object), options);
                        }
                        return dictionary;
                    default:
                        return null;
                }
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }
    }
}
