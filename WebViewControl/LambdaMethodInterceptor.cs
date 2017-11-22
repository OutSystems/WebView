using System;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class LambdaMethodInterceptor : IMethodInterceptor {

        private readonly Func<Func<object>, object> interceptCall;

        public LambdaMethodInterceptor(Func<Func<object>, object> interceptCall) {
            this.interceptCall = interceptCall;
        }

        object IMethodInterceptor.Intercept(Func<object> originalMethod, string methodName) {
            return interceptCall(originalMethod);
        }
    }
}
