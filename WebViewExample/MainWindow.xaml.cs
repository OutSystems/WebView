using System;
using System.IO;
using System.Windows;
using WebViewControl;

namespace WebViewExample {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        class NativeApi {
            public string[] GetItems() {
                return new[] {
                    "a",
                    "b",
                    "c"
                };
            }
        }

        private ReactView reactView;

        public MainWindow() {
            InitializeComponent();
            reactView = new ReactView("Resources", "dist", "example.js");
            reactView.NativeApi = new NativeApi();
            panel.Children.Add(reactView);

            //webview.LoadFrom("Resources");
            //webview.Load("https://www.google.pt/");
            //webview.LoadHtml(
            //@"
            //<html>
            //    <script>
            //        var result = '';
            //        function setValue(value) {
            //            result += value + ',';
            //            document.body.innerText = result; 
            //        }

            //        function getResult() {
            //            return result;
            //        }
            //    </script>
            //    <link href='style.css' rel='stylesheet' type='text/css'>
            //    <body>hi</body>
            //</html>");

            //webview.AllowDeveloperTools = true;
            //webview.BeforeNavigate += WebView_BeforeNavigate;
            //webview.BeforeResourceLoad += WebView_BeforeResourceLoad;
            //webview.Navigated += WebView_Navigated;
            //webview.ResourceLoadFailed += WebView_ResourceLoadFailed;
            //webview.JavascriptContextCreated += Webview_JavascriptContextReady;
        }

        //private void Webview_JavascriptContextReady() {
            
        //}

        //private void WebView_ResourceLoadFailed(string url) {
        //    Console.WriteLine(url);
        //}

        //private void WebView_Navigated(string url) {
        //    this.Title = url;
        //}

        //private void WebView_BeforeResourceLoad(WebView.ResourceHandler obj) {
        //    if (obj.Url == "https://www.google.pt/images/branding/googlelogo/2x/googlelogo_color_272x92dp.png") {
        //        obj.RespondWith(new StreamReader(@"C:\Users\jmn\Downloads\googlelogo_color_272x92dp.png").BaseStream, "png");
        //    }
        //}

        //private void WebView_BeforeNavigate(WebView.Request request) {
        //    //if (request.Url != "https://www.google.pt/") {
        //    //    request.Cancel();
        //    //}
        //}

        private void Button_Click(object sender, RoutedEventArgs e) {
        //    webview.ExecuteScriptFunction("setValue", "1");
        //    webview.ExecuteScriptFunction("setValue", "2");
        //    webview.ExecuteScriptFunction("setValue", "3");
        //    Title = webview.EvaluateScriptFunction<string>("getResult");
        }
    }
}
