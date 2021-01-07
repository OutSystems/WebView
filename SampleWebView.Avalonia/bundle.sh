#!/bin/sh

dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=osx-x64

TARGETAPP=bin/Debug/netcoreapp3.1/osx-x64/publish/SampleWebView.app/Contents/MacOS
chmod +x "$TARGETAPP/Xilium.CefGlue.BrowserProcess"
chmod +x "$TARGETAPP/SampleWebView.Avalonia"
