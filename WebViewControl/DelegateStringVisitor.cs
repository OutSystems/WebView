using System;
using System.Diagnostics;
using Xilium.CefGlue;

namespace WebViewControl;

public class DelegateStringVisitor(Action<string> action, string frameName) : CefStringVisitor {
    public string FrameName { get; } = frameName;

    protected override void Visit(string value) {
        // This is not guaranteed to be called from the UI thread...
        action?.Invoke(value);
    }
}