using JavascriptObject = System.Collections.Generic.Dictionary<string, object>;

namespace WebViewControl {

    public interface IJavascriptObject {
        JavascriptObject ToJavascriptObject();
    }

    public interface IJavascriptEnumValue {
        string ToJavascriptEnumValue();
    }

    public interface IJavascriptContextProvider {

        T EvaluateScriptFunction<T>(string functionName, params object[] args);
        void ExecuteScriptFunction(string functionName, params object[] args);
        //void SafeExecuteScript(Action<string> action, string functionName, params object[] args);
        void BindVariable(string variableName, object objectToBind);

        JavascriptObject GetLastJavascriptError();
    }
}
