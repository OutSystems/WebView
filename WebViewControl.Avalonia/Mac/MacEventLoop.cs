using System;
using static WebViewControl.Native.Mac.Objc;

namespace WebViewControl.Native.Mac {

    internal static class MacEventLoop {

        /// <summary>
        /// Processes (at most) one pending application event, waiting up to the specified time for it to arrive.
        /// Used to wait synchronously for results that are delivered through the UI thread (a nested event loop).
        /// </summary>
        public static void ProcessEvents(int milliseconds) {
            var application = Send(Cls("NSApplication"), Sel("sharedApplication"));
            var untilDate = Send(Cls("NSDate"), Sel("dateWithTimeIntervalSinceNow:"), milliseconds / 1000.0);
            // NSEventMaskAny, NSDefaultRunLoopMode
            var nextEvent = Send(application, Sel("nextEventMatchingMask:untilDate:inMode:dequeue:"), -1L, untilDate, NSString("kCFRunLoopDefaultMode"), (byte)1);
            if (nextEvent != IntPtr.Zero) {
                Send(application, Sel("sendEvent:"), nextEvent);
            }
        }
    }
}
