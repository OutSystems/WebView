using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tests.WebView {

    public static class Assertions {

        public static async Task<T> AssertThrows<T>(AsyncTestDelegate action) where T : Exception {
            try {
                await action();
                Assert.Fail("Should have thrown exception");
            } catch (Exception exception) {
                // ThrowsAsync is not working well
                Assert.IsInstanceOf<T>(exception);
                return (T)exception;
            }
            return null;
        }
    }
}
