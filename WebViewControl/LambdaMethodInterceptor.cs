using System;
using CefSharp.ModelBinding;

namespace WebViewControl {

    internal class LambdaMethodInterceptor : IMethodInterceptor {

        private Func<Func<object>, object> InterceptCall { get; }

        public LambdaMethodInterceptor(Func<Func<object>, object> interceptCall) {
            InterceptCall = interceptCall;
        }

        object IMethodInterceptor.Intercept(Func<object> originalMethod, string methodName) {
            return InterceptCall(originalMethod);
        }
    }
}
