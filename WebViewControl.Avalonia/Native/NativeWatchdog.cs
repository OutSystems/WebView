using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;

namespace WebViewControl.Native {

    /// <summary>
    /// Trace only (WEBVIEW_NATIVE_TRACE=1): reports the operations that are waiting for too long, and whether the UI thread is responsive.
    /// It runs on its own thread, so it keeps reporting when the UI thread is the one that is stuck.
    /// </summary>
    internal static class NativeWatchdog {

        private const int CheckIntervalMs = 1000;
        private const int ReportAfterMs = 3000;
        private const int RepeatEveryMs = 5000;

        private sealed class Operation {
            public string Description;
            public long StartedAt;
            public long LastReportedAt;
        }

        private static readonly ConcurrentDictionary<long, Operation> Pending = new ConcurrentDictionary<long, Operation>();
        private static long nextId;
        private static Timer timer;
        private static long lastUiResponseAt = Stopwatch.GetTimestamp();
        private static int uiProbePending;

        private sealed class Token : IDisposable {
            private readonly long id;
            public Token(long id) { this.id = id; }
            public void Dispose() {
                if (Pending.TryRemove(id, out var operation)) {
                    var elapsed = ElapsedMs(operation.StartedAt);
                    if (operation.LastReportedAt != 0) {
                        // it was reported as stuck, say that it finished
                        NativeTrace.Write($"WATCHDOG: finished after {elapsed} ms: {operation.Description}");
                    }
                }
            }
        }

        private sealed class NoToken : IDisposable {
            public void Dispose() { }
        }

        private static readonly IDisposable None = new NoToken();

        public static IDisposable Track(string description) {
            if (!NativeTrace.Enabled) {
                return None;
            }

            EnsureStarted();
            var id = Interlocked.Increment(ref nextId);
            Pending[id] = new Operation { Description = description, StartedAt = Stopwatch.GetTimestamp() };
            return new Token(id);
        }

        private static void EnsureStarted() {
            if (timer == null) {
                lock (Pending) {
                    if (timer == null) {
                        timer = new Timer(_ => Check(), null, CheckIntervalMs, CheckIntervalMs);
                    }
                }
            }
        }

        private static long ElapsedMs(long since) => (Stopwatch.GetTimestamp() - since) * 1000 / Stopwatch.Frequency;

        private static void Check() {
            try {
                // probe the UI thread: the probe only runs (and refreshes the timestamp) when the UI thread is processing its queue
                if (Interlocked.Exchange(ref uiProbePending, 1) == 0) {
                    Dispatcher.UIThread.Post(() => {
                        lastUiResponseAt = Stopwatch.GetTimestamp();
                        Interlocked.Exchange(ref uiProbePending, 0);
                    }, DispatcherPriority.Send);
                }

                var uiUnresponsiveMs = ElapsedMs(lastUiResponseAt);
                var uiBlocked = uiUnresponsiveMs > ReportAfterMs;

                foreach (var operation in Pending.Values) {
                    var elapsed = ElapsedMs(operation.StartedAt);
                    if (elapsed < ReportAfterMs) {
                        continue;
                    }
                    if (operation.LastReportedAt != 0 && ElapsedMs(operation.LastReportedAt) < RepeatEveryMs) {
                        continue;
                    }
                    operation.LastReportedAt = Stopwatch.GetTimestamp();
                    NativeTrace.Write($"WATCHDOG: still pending after {elapsed} ms: {operation.Description}  (UI thread {(uiBlocked ? $"NOT responding for {uiUnresponsiveMs} ms" : "responsive")})");
                }

                if (uiBlocked && Pending.IsEmpty) {
                    NativeTrace.Write($"WATCHDOG: UI thread NOT responding for {uiUnresponsiveMs} ms");
                }
            } catch {
                // diagnostics must never break the application
            }
        }
    }
}
