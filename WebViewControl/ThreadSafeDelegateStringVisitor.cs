using System;

namespace WebViewControl;

public class ThreadSafeDelegateStringVisitor(
    Action<Action<string>, string> executor,
    Action<string> action,
    string frameName) : DelegateStringVisitor(action, frameName) {
    
    protected override void Visit(string value) {
        // Use WebView.AsyncExecuteInUI<string> to Invoke the delegate on the UI thread!
        executor.Invoke(action, value);
    }
}