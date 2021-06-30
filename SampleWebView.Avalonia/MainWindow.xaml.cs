using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WebViewControl;

namespace SampleWebView.Avalonia {

    internal class MainWindow : Window {
        private static bool ISChildrenVisible;
        private static IList<SecundaryWindow> ChildWindows = new List<SecundaryWindow>();

        public MainWindow() {
            WebView.Settings.OsrEnabled = false;
            WebView.Settings.LogFile = "ceflog.txt";
            AvaloniaXamlLoader.Load(this);

            DataContext = new MainWindowViewModel(this.FindControl<WebView>("webview"));

            for (var i = 0; i < 2; i++) {
                var w = new SecundaryWindow();
                w.Opened += MainWindow_Opened;
                ChildWindows.Add(w);
            }

            var btn = this.Find<Button>("toggleWindows");
            btn.Click += Btn_Click;

        }

        private void Btn_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e) {
            if (ISChildrenVisible) {
                foreach (var w in ChildWindows) {
                    w.Hide();
                }
            } else {
                foreach (var w in ChildWindows) {
                    w.Show(this);
                }
            }
            ISChildrenVisible = !ISChildrenVisible;
        }

        private void MainWindow_Opened(object sender, EventArgs e) {
            Dispatcher.UIThread.Post(() => ((SecundaryWindow)sender).FocusInner(), DispatcherPriority.Background);
        }
    }
}