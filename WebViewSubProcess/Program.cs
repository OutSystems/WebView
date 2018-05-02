using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace WebViewSubProcess {

    static class Program {

        static void Main(string[] args) {
            Task.Factory.StartNew(() => AwaitParentProcessExit(), TaskCreationOptions.LongRunning);
            var basePath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), Environment.Is64BitProcess ? "x64" : "x86");
            AppDomain.CurrentDomain.ExecuteAssembly(basePath + "\\CefSharp.BrowserSubprocess.exe", args);
        }

        private static void AwaitParentProcessExit() {
            var parentProcessId = Process.GetCurrentProcess().GetParentId();
            if (parentProcessId.HasValue) {
                try {
                    var parentProcess = Process.GetProcessById(parentProcessId.Value);
                    parentProcess.WaitForExit();
                } catch (Exception) {
                    //main process probably died already
                }

                Task.Delay(1000); //wait a bit before exiting
            }

            Environment.Exit(0);
        }

        internal static int? GetParentId(this Process process) {
            string queryText = "select parentprocessid from win32_process where processid = " + process.Id;
            using (var searcher = new ManagementObjectSearcher(queryText)) {
                foreach (var obj in searcher.Get()) {
                    object data = obj.GetPropertyValue("parentprocessid");
                    if (data != null) {
                        return Convert.ToInt32(data);
                    }
                }
            }
            return null;
        }
    }
}
