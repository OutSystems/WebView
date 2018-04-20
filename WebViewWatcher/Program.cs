using System;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace WebViewWatcher {

    static class Program {

        static void Main(string[] args) {
            if (args.Length < 2) {
                return;
            }

            var cefSubProcessName = args[0];
            var showLog = args[1] == "1";

            try {
                var parentProcessId = Process.GetCurrentProcess().GetParentId();
                if (parentProcessId == null) {
                    return;
                }

                var parentProcess = Process.GetProcessById(parentProcessId.Value);
                parentProcess.WaitForExit();

                Thread.Sleep(1000); // wait a bit before scanning cef processes ... they might close at their own will

                var killedProcesses = 0;
                var cefSubProcesses = Process.GetProcessesByName(cefSubProcessName);
                Parallel.ForEach(cefSubProcesses, (cefSubProcess) => {
                    var parentId = cefSubProcess.GetParentId();
                    if (parentId == null || parentId == parentProcessId) {
                        if (!cefSubProcess.HasExited) {
                            // kill orphan process
                            cefSubProcess.Kill();
                            killedProcesses++;
                        }
                    }
                });

                if (showLog && killedProcesses > 0) {
                    Process.Start("cmd.exe", $"/K echo CefSharpWatcher: Killed {killedProcesses} zombie cefsharp processes!");
                }
            } catch (Exception e) {
                if (showLog) {
                    Process.Start("cmd.exe", "/K echo CefSharpWatcher: " + e.ToString().Replace("\r\n", " "));
                }
            }
        }

        internal static int? GetParentId(this Process process) {
            string queryText = "select parentprocessid from win32_process where processid = " + process.Id;
            using (var searcher = new ManagementObjectSearcher(queryText)) {
                foreach (var obj in searcher.Get()) {
                    object data = obj.Properties["parentprocessid"].Value;
                    if (data != null) {
                        return Convert.ToInt32(data);
                    }
                }
            }
            return null;
        }
    }
}
