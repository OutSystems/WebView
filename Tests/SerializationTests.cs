using System.Collections.Generic;
using NUnit.Framework;
using WebViewControl;

namespace Tests {
    public class SerializationTests {

        private enum SerializationEnum {
            Value0,
            Value1
        }

        [Test(Description = "Tests that javascript data serialization works as expected")]
        public void JavascriptDataSerialization() {
            Assert.AreEqual("true", JavascriptSerializer.Serialize(true));
            Assert.AreEqual("false", JavascriptSerializer.Serialize(false));

            Assert.AreEqual("1", JavascriptSerializer.Serialize(1));
            Assert.AreEqual("-1", JavascriptSerializer.Serialize(-1));
            Assert.AreEqual("1.1", JavascriptSerializer.Serialize(1.1));
            Assert.AreEqual("-1.1", JavascriptSerializer.Serialize(-1.1));

            Assert.AreEqual("\"hello \\\"world\\\"\"", JavascriptSerializer.Serialize("hello \"world\""));

            Assert.AreEqual("1", JavascriptSerializer.Serialize(SerializationEnum.Value1));

            Assert.AreEqual("[1,2,3]", JavascriptSerializer.Serialize(new[] { 1, 2, 3 }));

            Assert.AreEqual("{\"prop-a\":\"value-a\",\"prop-b\":\"value-b\",\"prop-c\":1.1}", JavascriptSerializer.Serialize(new Dictionary<string, object>() { { "prop-b", "value-b" }, { "prop-a", "value-a" }, { "prop-c", 1.1 } }));
        }
    }
}
