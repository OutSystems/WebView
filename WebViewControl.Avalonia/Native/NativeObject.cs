using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace WebViewControl.Native {

    /// <summary>
    /// A .net object registered to be called from javascript (<see cref="WebView.RegisterJavascriptObject(string, object, Func{Func{object}, object})"/>).
    /// </summary>
    internal sealed class NativeObject {

        private readonly Dictionary<string, NativeMethod> methods;

        public NativeObject(string name, object target, Func<Func<object>, object> interceptCall) {
            Name = name;
            Target = target;
            InterceptCall = interceptCall;
            methods = target.GetType()
                            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                            .Where(m => !m.IsSpecialName)
                            .GroupBy(m => ToJavascriptMemberName(m.Name))
                            .ToDictionary(g => g.Key, g => new NativeMethod(g.First()));
        }

        public string Name { get; }

        public object Target { get; }

        public Func<Func<object>, object> InterceptCall { get; }

        public IEnumerable<string> MethodNames => methods.Keys;

        public bool TryGetMethod(string name, out NativeMethod method) => methods.TryGetValue(name ?? "", out method);

        private static string ToJavascriptMemberName(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    internal sealed class NativeMethod {

        private readonly MethodInfo method;
        private readonly Type[] parameterTypes;
        private readonly int mandatoryParametersCount;
        private readonly bool hasParamsArray;

        public NativeMethod(MethodInfo method) {
            this.method = method;
            var parameters = method.GetParameters();
            hasParamsArray = parameters.Length > 0 && parameters[parameters.Length - 1].GetCustomAttribute<ParamArrayAttribute>() != null;
            mandatoryParametersCount = parameters.Length - (hasParamsArray ? 1 : 0);
            parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
        }

        /// <summary>
        /// Converts the arguments (json array) sent by javascript into the method parameters.
        /// </summary>
        public object[] ConvertArguments(string argumentsJson) {
            var elements = new List<JsonElement>();
            if (!string.IsNullOrWhiteSpace(argumentsJson)) {
                using (var document = JsonDocument.Parse(argumentsJson)) {
                    foreach (var element in document.RootElement.EnumerateArray()) {
                        elements.Add(element.Clone());
                    }
                }
            }

            if (elements.Count < mandatoryParametersCount) {
                throw new ArgumentException($"Number of original arguments provided does not match the number of {method.Name} method required parameters.");
            }

            var arguments = new object[parameterTypes.Length];
            for (var i = 0; i < mandatoryParametersCount; i++) {
                arguments[i] = NativeJson.Deserialize(elements[i], parameterTypes[i]);
            }

            if (hasParamsArray) {
                var elementType = parameterTypes[parameterTypes.Length - 1].GetElementType();
                var extra = elements.Count - mandatoryParametersCount;
                var paramsArray = Array.CreateInstance(elementType, extra);
                for (var i = 0; i < extra; i++) {
                    paramsArray.SetValue(NativeJson.Deserialize(elements[mandatoryParametersCount + i], elementType), i);
                }
                arguments[arguments.Length - 1] = paramsArray;
            }

            return arguments;
        }

        public object Invoke(object target, object[] arguments) {
            try {
                return method.Invoke(target, arguments);
            } catch (TargetInvocationException e) {
                ExceptionDispatchInfo.Capture(e.InnerException ?? e).Throw();
                return null;
            }
        }
    }
}
