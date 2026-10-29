using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;
using WebViewControl.Native;

namespace WebViewControl {

    partial class WebView {

        private sealed class ScriptTask {

            public ScriptTask(string script, string functionName, bool awaitResult) {
                Script = script;
                FunctionName = functionName;
                if (awaitResult) {
                    Completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            public string Script { get; }

            /// <summary>
            /// We store the function name apart from the script and use it later in the exception details
            /// this prevents any params to be shown in the message because they can contain sensitive information
            /// </summary>
            public string FunctionName { get; }

            /// <summary>
            /// Null when the result of the script is not awaited.
            /// </summary>
            public TaskCompletionSource<string> Completion { get; }
        }

        // scripts wait until the javascript context of the document exists. Both collections are guarded by the pendingScripts lock.
        private readonly Queue<ScriptTask> pendingScripts = new Queue<ScriptTask>();
        private readonly Dictionary<int, ScriptTask> runningEvaluations = new Dictionary<int, ScriptTask>();
        private int nextEvaluationId = 1;
        private bool flushScheduled;

        // true when the javascript context of the current document is available. Only accessed in the UI thread.
        private bool contextReady;

        private readonly Dictionary<string, NativeObject> nativeObjects = new Dictionary<string, NativeObject>();
        private readonly HashSet<string> pendingBinds = new HashSet<string>();

        private static readonly TimeSpan DefaultSynchronousWaitTimeout = TimeSpan.FromSeconds(60);

        // ---- native objects ------------------------------------------------------------------------------------------

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
            return InnerRegisterJavascriptObject(name, objectToBind, target => interceptCall(target));
        }

        private bool InnerRegisterJavascriptObject(string name, object objectToBind, Func<Func<object>, object> interceptCall) {
            var nativeObject = new NativeObject(name, objectToBind, TrackPendingCalls(interceptCall));

            lock (nativeObjects) {
                if (nativeObjects.ContainsKey(name)) {
                    return false;
                }
                nativeObjects.Add(name, nativeObject);
            }

            RunInUI(() => {
                if (isDisposing || !contextReady) {
                    return; // the object is defined when the context is created
                }
                if (pendingBinds.Remove(name)) {
                    ConfirmBind(nativeObject);
                } else {
                    DefineNativeObject(nativeObject);
                }
            });

            return true;
        }

        /// <summary>
        /// Unregisters an object with the specified name in the window context of the browser
        /// </summary>
        public void UnregisterJavascriptObject(string name) {
            lock (nativeObjects) {
                nativeObjects.Remove(name);
            }

            RunInUI(() => {
                if (!isDisposing && contextReady) {
                    browser.RunScript($"window.__rv.undefineObject({JavascriptSerializer.Serialize(name)})");
                }
            });
        }

        private Func<Func<object>, object> TrackPendingCalls(Func<Func<object>, object> interceptCall) {
            return target => {
                if (isDisposing) {
                    return null;
                }

                var isAsync = false;
                try {
                    JavascriptPendingCalls.AddCount();

                    if (isDisposing) {
                        // check again, to avoid concurrency problems with dispose
                        return null;
                    }

                    var result = interceptCall(target);
                    if (result is Task task) {
                        task.ContinueWith(t => JavascriptPendingCalls.Signal());
                        isAsync = true;
                    }

                    return result;
                } catch (InvalidOperationException) when (isDisposing) {
                    return null;
                } finally {
                    if (!isAsync) {
                        try {
                            JavascriptPendingCalls.Signal();
                        } catch (InvalidOperationException) {
                            // disposed
                        }
                    }
                }
            };
        }

        private void DefineNativeObject(NativeObject nativeObject) {
            browser.RunScript($"window.__rv.defineObject({JavascriptSerializer.Serialize(nativeObject.Name)},{JavascriptSerializer.Serialize(nativeObject.MethodNames.ToArray())})");
        }

        private void ConfirmBind(NativeObject nativeObject) {
            DefineNativeObject(nativeObject);
            browser.RunScript($"window.__rv.bound({JavascriptSerializer.Serialize(nativeObject.Name)},true)");
        }

        private void HandleBindRequest(string name) {
            NativeObject nativeObject;
            lock (nativeObjects) {
                nativeObjects.TryGetValue(name, out nativeObject);
            }

            if (nativeObject != null) {
                ConfirmBind(nativeObject);
            } else {
                // the object might still be registered, answer when that happens
                pendingBinds.Add(name);
            }
        }

        private void HandleNativeCall(int callId, string objectName, string methodName, string argumentsJson) {
            NativeObject nativeObject;
            lock (nativeObjects) {
                nativeObjects.TryGetValue(objectName ?? "", out nativeObject);
            }

            if (nativeObject == null) {
                RejectNativeCall(callId, $"Object named {objectName} was not found. Make sure it was registered before.");
                return;
            }

            if (!nativeObject.TryGetMethod(methodName, out var method)) {
                RejectNativeCall(callId, $"Object does not have a {methodName} method.");
                return;
            }

            var tracking = NativeWatchdog.Track($"call from page {objectName}.{methodName}");
            NativeTrace.Write($"call {callId} from page: {objectName}.{methodName}");
            object result;
            try {
                var arguments = method.ConvertArguments(argumentsJson);
                result = nativeObject.InterceptCall(() => {
                    try {
                        return method.Invoke(nativeObject.Target, arguments);
                    } catch (Exception e) {
                        // runs where the interceptor decides (possibly another thread): the page only sees the message
                        NativeTrace.Write($"call {callId} {objectName}.{methodName} threw: {e}");
                        throw;
                    }
                });
            } catch (Exception e) {
                tracking.Dispose();
                NativeTrace.Write($"call {callId} {objectName}.{methodName} failed: {e}");
                RejectNativeCall(callId, e.Message);
                return;
            }

            if (result is Task task) {
                task.ContinueWith(t => RunInUI(() => {
                    tracking.Dispose();
                    CompleteNativeCall(callId, t);
                }), TaskScheduler.Default);
            } else {
                tracking.Dispose();
                ResolveNativeCall(callId, result);
            }
        }

        private void CompleteNativeCall(int callId, Task task) {
            if (task.IsFaulted) {
                RejectNativeCall(callId, (task.Exception.InnerExceptions.FirstOrDefault() ?? task.Exception).Message);
            } else if (task.IsCanceled) {
                RejectNativeCall(callId, "The task was canceled");
            } else {
                ResolveNativeCall(callId, GetTaskResult(task));
            }
        }

        private static object GetTaskResult(Task task) {
            var taskType = task.GetType();
            if (!taskType.IsGenericType) {
                return null;
            }
            var result = taskType.GetProperty("Result")?.GetValue(task);
            return result?.GetType().Name == "VoidTaskResult" ? null : result;
        }

        private void ResolveNativeCall(int callId, object result) {
            if (isDisposing || !contextReady) {
                return;
            }
            string json;
            try {
                json = NativeJson.Serialize(result);
            } catch (Exception e) {
                NativeTrace.Write($"serializing the result of call {callId} ({result?.GetType().FullName}) failed: {e}");
                RejectNativeCall(callId, $"{e.Message} (result type: {result?.GetType().FullName})");
                return;
            }
            NativeTrace.Write($"call {callId} resolved: {(json.Length > 120 ? json.Substring(0, 120) + "..." : json)}");
            browser.RunScript($"window.__rv.resolve({callId},{json})");
        }

        private void RejectNativeCall(int callId, string message) {
            if (isDisposing || !contextReady) {
                return;
            }
            browser.RunScript($"window.__rv.reject({callId},{JavascriptSerializer.Serialize(message)})");
        }

        // ---- messages from the page ----------------------------------------------------------------------------------

        void INativeBrowserOwner.OnScriptMessage(string json) {
            if (isDisposing) {
                return;
            }

            NativeTrace.Write("message from page: " + (json.Length > 300 ? json.Substring(0, 300) + "..." : json));

            using (var document = JsonDocument.Parse(json)) {
                var message = document.RootElement;
                switch (message.GetProperty("t").GetString()) {
                    case "ctx":
                        HandleContextCreated();
                        break;
                    case "call":
                        HandleNativeCall(message.GetProperty("id").GetInt32(), message.GetProperty("obj").GetString(), message.GetProperty("method").GetString(), message.GetProperty("args").GetString());
                        break;
                    case "bind":
                        HandleBindRequest(message.GetProperty("name").GetString());
                        break;
                    case "res":
                        HandleScriptResult(message);
                        break;
                    case "jserr":
                        ForwardUnhandledAsyncException(CreateJavascriptException(message.GetProperty("name").GetString(), message.GetProperty("message").GetString(), message.GetProperty("stack").GetString(), null), frameName: string.Empty);
                        break;
                    case "f12":
                        if (AllowDeveloperTools) {
                            ToggleDeveloperTools();
                        }
                        break;
                    case "console":
                        NativeTrace.Write($"[js {message.GetProperty("level").GetString()}] {message.GetProperty("text").GetString()}");
                        break;
                }
            }
        }

        private void HandleScriptResult(JsonElement message) {
            var id = message.GetProperty("id").GetInt32();
            var success = message.GetProperty("ok").GetBoolean();
            var value = message.GetProperty("v");

            if (id == 0) {
                // a script that was executed without waiting for the result has failed
                if (!success) {
                    ForwardUnhandledAsyncException(CreateScriptException(value, message), frameName: string.Empty);
                }
                return;
            }

            ScriptTask task;
            lock (pendingScripts) {
                if (!runningEvaluations.Remove(id, out task)) {
                    return;
                }
            }

            if (success) {
                task.Completion.TrySetResult(value.GetString());
            } else {
                task.Completion.TrySetException(CreateScriptException(value, message));
            }
        }

        private static Exception CreateScriptException(JsonElement error, JsonElement message) {
            var functionName = message.TryGetProperty("fn", out var fn) ? fn.GetString() : null;
            return CreateJavascriptException(error.GetProperty("name").GetString(), error.GetProperty("message").GetString(), error.GetProperty("stack").GetString(), functionName);
        }

        // ---- javascript context lifecycle ----------------------------------------------------------------------------

        private void HandleContextCreated() {
            if (contextReady) {
                // a new document without the previous one being reported as gone
                HandleContextReleased();
            }

            contextReady = true;

            ApplyDocumentSettings();

            NativeObject[] objects;
            lock (nativeObjects) {
                objects = nativeObjects.Values.ToArray();
            }
            foreach (var nativeObject in objects) {
                DefineNativeObject(nativeObject);
            }

            FlushScripts();

            ExecuteWithAsyncErrorHandling(() => JavascriptContextCreated?.Invoke(string.Empty));
        }

        private void HandleContextReleased() {
            if (!contextReady) {
                return;
            }

            contextReady = false;
            pendingBinds.Clear();

            // the scripts that were running on the previous document will not complete
            AbortRunningEvaluations();

            ExecuteWithAsyncErrorHandling(() => JavascriptContextReleased?.Invoke(string.Empty));
        }

        private void ApplyDocumentSettings() {
            RunInUI(() => {
                if (isDisposing || !contextReady) {
                    return;
                }

                string Flag(bool value) => value ? "true" : "false";

                browser.RunScript($"window.__rv.setKeyboardDisabled({Flag(isKeyboardInputDisabled)});" +
                                  $"window.__rv.setContextMenuDisabled({Flag(disableBuiltinContextMenus)});" +
                                  $"window.__rv.setDeveloperKeysEnabled({Flag(AllowDeveloperTools)});" +
                                  $"window.__rv.setTrace({Flag(NativeTrace.Enabled)});");
            });
        }

        private void AbortRunningEvaluations() {
            ScriptTask[] running;
            lock (pendingScripts) {
                running = runningEvaluations.Values.ToArray();
                runningEvaluations.Clear();
            }
            foreach (var task in running) {
                task.Completion.TrySetResult(null);
            }
        }

        private void AbortPendingScripts() {
            ScriptTask[] pending;
            lock (pendingScripts) {
                pending = pendingScripts.ToArray();
                pendingScripts.Clear();
            }
            foreach (var task in pending) {
                task.Completion?.TrySetResult(null);
            }
            AbortRunningEvaluations();
        }

        // ---- script execution ----------------------------------------------------------------------------------------

        private ScriptTask QueueScript(string script, string functionName, bool awaitResult) {
            ScriptTask task;
            lock (pendingScripts) {
                if (isDisposing) {
                    return null;
                }
                task = new ScriptTask(script, functionName, awaitResult);
                pendingScripts.Enqueue(task);
                if (flushScheduled) {
                    return task;
                }
                flushScheduled = true;
            }

            // scripts queued in sequence are sent to the browser together
            Dispatcher.UIThread.Post(FlushScripts, DispatcherPriority.Normal);
            return task;
        }

        /// <summary>
        /// Sends the queued scripts to the browser, if the javascript context is available. Must be called in the UI thread.
        /// </summary>
        private void FlushScripts() {
            var batch = new StringBuilder();

            lock (pendingScripts) {
                flushScheduled = false;

                if (!contextReady || isDisposing || !browser.IsCreated) {
                    return;
                }

                while (pendingScripts.Count > 0) {
                    var task = pendingScripts.Dequeue();
                    if (task.Completion == null) {
                        batch.AppendLine(WrapScriptToExecute(task));
                    } else {
                        var id = nextEvaluationId++;
                        runningEvaluations.Add(id, task);
                        batch.AppendLine(WrapScriptToEvaluate(id, task));
                    }
                }
            }

            if (batch.Length > 0) {
                browser.RunScript(batch.ToString());
            }
        }

        private static string WrapScriptToExecute(ScriptTask task) =>
            "(function(){try{" + task.Script + Environment.NewLine + "}catch(e){window.__rv.fail(0,e," + JavascriptSerializer.Serialize(task.FunctionName) + ")}})();";

        // the script is the body of a function, so it can return a value (that can also be a promise)
        private static string WrapScriptToEvaluate(int id, ScriptTask task) =>
            "(function(){try{var r=(function(){" + task.Script + Environment.NewLine + "}).call(window);" +
            "Promise.resolve(r).then(function(v){window.__rv.reply(" + id + ",v)},function(e){window.__rv.fail(" + id + ",e," + JavascriptSerializer.Serialize(task.FunctionName) + ")})" +
            "}catch(e){window.__rv.fail(" + id + ",e," + JavascriptSerializer.Serialize(task.FunctionName) + ")}})();";

        private async Task<T> EvaluateScriptInternal<T>(string script, string functionName, string frameName, TimeSpan? timeout) {
            if (!this.IsMainFrame(frameName)) {
                return default;
            }

            var scriptTask = QueueScript(script, functionName, awaitResult: true);
            if (scriptTask == null) {
                return default; // disposing
            }

            using (NativeWatchdog.Track($"script evaluation {functionName ?? "(script)"} (javascript context {(contextReady ? "ready" : "NOT created yet")})")) {
                return await AwaitEvaluation<T>(scriptTask, timeout).ConfigureAwait(false);
            }
        }

        private static async Task<T> AwaitEvaluation<T>(ScriptTask scriptTask, TimeSpan? timeout) {
            if (timeout.HasValue) {
                var winner = await Task.WhenAny(scriptTask.Completion.Task, Task.Delay(timeout.Value)).ConfigureAwait(false);
                if (winner != scriptTask.Completion.Task) {
                    throw new JavascriptException("Timeout", "Script evaluation timed out");
                }
            }

            var resultJson = await scriptTask.Completion.Task.ConfigureAwait(false);
            return NativeJson.DeserializeResult<T>(resultJson);
        }

        public Task<T> EvaluateScript<T>(string script, string frameName = MainFrameName, TimeSpan? timeout = null) {
            return EvaluateScriptInternal<T>(script, null, frameName, timeout);
        }

        public void ExecuteScript(string script, string frameName = MainFrameName) {
            if (this.IsMainFrame(frameName)) {
                QueueScript(script, null, awaitResult: false);
            }
        }

        public void ExecuteScriptFunction(string functionName, params string[] args) {
            ExecuteScriptFunctionInFrame(functionName, MainFrameName, args);
        }

        public void ExecuteScriptFunctionInFrame(string functionName, string frameName, params string[] args) {
            if (this.IsMainFrame(frameName)) {
                QueueScript(MakeScript(functionName, false, args, false), functionName, awaitResult: false);
            }
        }

        public Task<T> EvaluateScriptFunction<T>(string functionName, params string[] args) {
            return EvaluateScriptFunctionInFrame<T>(functionName, MainFrameName, args);
        }

        public Task<T> EvaluateScriptFunctionInFrame<T>(string functionName, string frameName, params string[] args) {
            return EvaluateScriptInternal<T>(MakeScript(functionName, false, args, true), functionName, frameName, timeout: null);
        }

        protected void ExecuteScriptFunctionWithSerializedParams(string functionName, params object[] args) {
            ExecuteScriptFunctionWithSerializedParamsInFrame(functionName, MainFrameName, args);
        }

        protected void ExecuteScriptFunctionWithSerializedParamsInFrame(string functionName, string frameName, params object[] args) {
            if (this.IsMainFrame(frameName)) {
                QueueScript(MakeScript(functionName, true, args, false), functionName, awaitResult: false);
            }
        }

        protected Task<T> EvaluateScriptFunctionWithSerializedParams<T>(string functionName, params object[] args) {
            return EvaluateScriptFunctionWithSerializedParamsInFrame<T>(functionName, MainFrameName, args);
        }

        protected Task<T> EvaluateScriptFunctionWithSerializedParamsInFrame<T>(string functionName, string frameName, params object[] args) {
            return EvaluateScriptInternal<T>(MakeScript(functionName, true, args, true), functionName, frameName, timeout: null);
        }

        private static string MakeScript(string functionName, bool serializeParams, object[] args, bool emitReturn) {
            string SerializeParam(object value) {
                if (serializeParams || value == null) {
                    return JavascriptSerializer.Serialize(value);
                }
                return value.ToString();
            }
            var argsSerialized = args.Select(SerializeParam);
            return $"{(emitReturn ? "return " : string.Empty)}{functionName}({string.Join(",", argsSerialized)})";
        }

        /// <summary>
        /// Waits synchronously for the result of an evaluation.
        /// The results are delivered through the UI thread, so when called from it the application events are processed while waiting
        /// (otherwise the UI thread would block the delivery of the very result it is waiting for).
        /// </summary>
        internal T WaitForResult<T>(Task<T> task) {
            if (task.IsCompleted || !Dispatcher.UIThread.CheckAccess()) {
                return task.Result;
            }

            var timeout = DefaultScriptsExecutionTimeout ?? DefaultSynchronousWaitTimeout;
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted) {
                FlushScripts();
                NativeBrowserFactory.ProcessEvents(10);

                if (stopwatch.Elapsed > timeout) {
                    throw new JavascriptException("Timeout", $"More than {timeout.TotalMilliseconds}ms elapsed waiting for a script evaluation");
                }
            }
            return task.Result;
        }

        // ---- javascript errors ---------------------------------------------------------------------------------------

        private static readonly Regex V8StackFrameRegex = new Regex(@"at\s*(?<method>.*?)\s\(?(?<location>[^\s]+):(?<line>\d+):(?<column>\d+)", RegexOptions.Compiled);
        private static readonly Regex JavascriptCoreStackFrameRegex = new Regex(@"^(?<method>[^@]*)@(?<location>.+):(?<line>\d+):(?<column>\d+)$", RegexOptions.Compiled);

        private static JavascriptException CreateJavascriptException(string name, string message, string stack, string evaluatedFunctionName) {
            var frames = new List<JavascriptStackFrame>();

            if (!string.IsNullOrEmpty(evaluatedFunctionName)) {
                frames.Add(new JavascriptStackFrame(evaluatedFunctionName, "eval", 0, 0));
            }

            foreach (var line in (stack ?? string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                var match = V8StackFrameRegex.Match(line);
                if (!match.Success) {
                    match = JavascriptCoreStackFrameRegex.Match(line.Trim());
                }
                if (match.Success) {
                    frames.Add(new JavascriptStackFrame(match.Groups["method"].Value, match.Groups["location"].Value, int.Parse(match.Groups["column"].Value), int.Parse(match.Groups["line"].Value)));
                }
            }

            return new JavascriptException(name, message, frames);
        }
    }
}
