using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebViewControl {

    /// <summary>
    /// A .NET object exposed to the page through the javascript bridge of the native engines.
    /// Follows the CefGlue binding conventions: every public instance method is exposed with its name starting in lower case,
    /// arguments are deserialized into the parameter types, returned tasks are awaited and the result is sent back as JSON.
    /// </summary>
    internal sealed class NativeJavascriptObject {

        private readonly object target;
        private readonly Func<Func<object>, object> interceptCall;
        private readonly Dictionary<string, MethodInfo[]> methods;

        public NativeJavascriptObject(string name, object target, Func<Func<object>, object> interceptCall) {
            Name = name;
            this.target = target;
            this.interceptCall = interceptCall;
            methods = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => !m.IsSpecialName && !m.IsGenericMethodDefinition && m.DeclaringType != typeof(object))
                .GroupBy(m => ToJavascriptName(m.Name))
                .ToDictionary(g => g.Key, g => g.ToArray());
        }

        public string Name { get; }

        public IEnumerable<string> MethodNames => methods.Keys;

        /// <summary>
        /// Invokes the method with the arguments (a JSON array) and returns the JSON of the result.
        /// </summary>
        public async Task<string> InvokeAsync(string methodName, string argsJson) {
            if (!methods.TryGetValue(methodName ?? "", out var candidates)) {
                throw new MissingMethodException($"Method '{methodName}' not found in object '{Name}'");
            }

            JsonElement[] args;
            using (var argsDocument = JsonDocument.Parse(string.IsNullOrEmpty(argsJson) ? "[]" : argsJson)) {
                args = argsDocument.RootElement.EnumerateArray().Select(a => a.Clone()).ToArray();
            }

            var method = SelectMethod(candidates, args.Length);
            var parameters = ConvertArguments(method.GetParameters(), args);

            object Invoke() {
                try {
                    return method.Invoke(target, parameters);
                } catch (TargetInvocationException e) when (e.InnerException != null) {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    throw;
                }
            }

            var result = await UnwrapResult(interceptCall(Invoke)).ConfigureAwait(false);
            return NativeJson.Serialize(result);
        }

        private static string ToJavascriptName(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

        private static MethodInfo SelectMethod(MethodInfo[] candidates, int argsCount) {
            if (candidates.Length == 1) {
                return candidates[0];
            }
            bool Accepts(MethodInfo method) {
                var parameters = method.GetParameters();
                if (parameters.Length > 0 && IsParamArray(parameters[parameters.Length - 1])) {
                    return argsCount >= parameters.Length - 1;
                }
                return argsCount <= parameters.Length && argsCount >= parameters.Count(p => !p.IsOptional);
            }
            return candidates.FirstOrDefault(m => m.GetParameters().Length == argsCount) ?? candidates.FirstOrDefault(Accepts) ?? candidates[0];
        }

        private static bool IsParamArray(ParameterInfo parameter) => parameter.IsDefined(typeof(ParamArrayAttribute), false);

        private static object[] ConvertArguments(ParameterInfo[] parameters, JsonElement[] args) {
            var result = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++) {
                var parameter = parameters[i];
                if (i == parameters.Length - 1 && IsParamArray(parameter) && !(args.Length == parameters.Length && args[i].ValueKind == JsonValueKind.Array)) {
                    // the remaining arguments go into the params array
                    var elementType = parameter.ParameterType.GetElementType();
                    var paramsArray = Array.CreateInstance(elementType, Math.Max(0, args.Length - i));
                    for (var j = i; j < args.Length; j++) {
                        paramsArray.SetValue(NativeJson.Convert(args[j], elementType), j - i);
                    }
                    result[i] = paramsArray;
                } else if (i < args.Length) {
                    result[i] = NativeJson.Convert(args[i], parameter.ParameterType);
                } else if (parameter.HasDefaultValue) {
                    result[i] = parameter.DefaultValue;
                } else {
                    result[i] = parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
                }
            }
            return result;
        }

        private static async Task<object> UnwrapResult(object result) {
            // interceptors may return a task, whose result may be the task returned by the method itself
            while (result is Task task) {
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty(nameof(Task<object>.Result));
                result = resultProperty == null || resultProperty.PropertyType.FullName == "System.Threading.Tasks.VoidTaskResult" ? null : resultProperty.GetValue(task);
            }
            return result;
        }
    }
}
