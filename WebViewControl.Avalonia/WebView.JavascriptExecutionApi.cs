using System;
using System.Threading.Tasks;

namespace WebViewControl {

    partial class WebView {

        /// <summary>
        /// Registers an object with the specified name in the window context of the browser
        /// </summary>
        /// <param name="name">Name of the object that will be available on the JS window context.</param>
        /// <param name="objectToBind">.Net object instance that will be bound to the JS proxy object.</param>
        /// <param name="interceptCall">Optional method that allows intercepting every call.</param>
        /// <returns>True if the object was registered or false if the object was already registered before</returns>
        public bool RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall = null) {
            if (interceptCall == null) {
                interceptCall = target => target();
            }
            return InnerRegisterJavascriptObject(name, objectToBind, interceptCall);
        }
        
        /// <summary>
        /// Registers an object with the specified name in the window context of the browser.
        /// Use this overload with async interceptor methods.
        /// </summary>
        /// <param name="name">Name of the object that will be available on the JS window context.</param>
        /// <param name="objectToBind">.Net object instance that will be bound to the JS proxy object.</param>
        /// <param name="interceptCall">Async method that allows intercepting every call.</param>
        /// <returns>True if the object was registered or false if the object was already registered before</returns>
        public bool RegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, Task<object>> interceptCall) {
            return InnerRegisterJavascriptObject(name, objectToBind, interceptCall);
        }
        
        private bool InnerRegisterJavascriptObject<T>(string name, object objectToBind, Func<Func<object>, T> interceptCall) where T : class {
            if (chromium.IsJavascriptObjectRegistered(name)) {
                return false;
            }

            T CallTargetMethod(Func<object> target) {
                if (isDisposing) {
                    return default;
                }

                var isAsync = false;
                try {
                    JavascriptPendingCalls.AddCount();
                    
                    if (isDisposing) {
                        // check again, to avoid concurrency problems with dispose
                        return default;
                    }
                    
                    var result = interceptCall(target);
                    if (result is Task task) {
                        task.ContinueWith(t => JavascriptPendingCalls.Signal());
                        isAsync = true;
                    }
                    
                    return result;
                } finally {
                    if (!isAsync) {
                        JavascriptPendingCalls.Signal();
                    }
                }
            }

            chromium.RegisterJavascriptObject(objectToBind, name, CallTargetMethod);

            return true;
        }

        /// <summary>
        /// Unregisters an object with the specified name in the window context of the browser
        /// </summary>
        /// <param name="name"></param>
        public void UnregisterJavascriptObject(string name) {
            chromium.UnregisterJavascriptObject(name);
        }

        public Task<T> EvaluateScript<T>(string script, string frameName = MainFrameName, TimeSpan? timeout = null) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScript<T>(script, timeout: timeout);
            }
            return Task.FromResult(default(T));
        }

        public void ExecuteScript(string script, string frameName = MainFrameName) {
            GetJavascriptExecutor(frameName)?.ExecuteScript(script);
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            ExecuteScriptFunctionInFrame(functionName, MainFrameName, args);
        }

        public void ExecuteScriptFunctionInFrame(string functionName, string frameName, params string[] args) {
            GetJavascriptExecutor(frameName)?.ExecuteScriptFunction(functionName, false, args);
        }

        public Task<T> EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return EvaluateScriptFunctionInFrame<T>(functionName, MainFrameName, args);
        }

        public Task<T> EvaluateScriptFunctionInFrame<T>(string functionName, string frameName, params string[] args) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScriptFunction<T>(functionName, false, args);
            }
            return Task.FromResult(default(T));
        }

        protected void ExecuteScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            ExecuteScriptFunctionWithSerializedParamsInFrame(functionName, MainFrameName, args);
        }

        protected void ExecuteScriptFunctionWithSerializedParamsInFrame(string functionName, string frameName, params object[] args) {
            GetJavascriptExecutor(frameName)?.ExecuteScriptFunction(functionName, true, args);
        }

        protected Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string functionName, params object[] args) {
            return EvaluateScriptFunctionWithSerializedParamsInFrame<T>(functionName, MainFrameName, args);
        }

        protected Task<T> EvaluateScriptFunctionWithSerializedParamsInFrame<T>(string functionName, string frameName, params object[] args) {
            var jsExecutor = GetJavascriptExecutor(frameName);
            if (jsExecutor != null) {
                return jsExecutor.EvaluateScriptFunction<T>(functionName, true, args);
            }
            return Task.FromResult(default(T));
        }
    }
}
