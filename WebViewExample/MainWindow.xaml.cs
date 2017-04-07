using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows;
using WebViewControl;

namespace WebViewExample {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        internal class ModelObject {
            public int Id;
        }

        //[TypeConverter(typeof(JsObjectConverter<JsObj>))]
        public class JsObj : ReactView.ViewObject, IJsObj {

            public string Value;

            internal ModelObject ModelObj {
                get;
                set;
            }

            public void Execute(string newValue) {
                Console.WriteLine("JS says hello: " + newValue + " (" + Value + ")");
            }
        }

        class NativeApi : INativeApi {


            public IJsObj[] GetItems() {
                var result = new List<JsObj>();
                for (var i = 0; i < 1000; i++) {
                    result.Add(new JsObj() { Value = "" + i, ModelObj = new ModelObject() { Id = i } });
                }
                return result.ToArray();
            }

            public void ExecuteOnItem(IJsObj item, string newValue) {
                ((JsObj)item).Execute(newValue);
            }
        }

        private ReactView reactView;
        private WebView webview;

        public MainWindow() {
            InitializeComponent();
            //reactView = new ReactView("Resources", "dist", "example.js");
            //reactView.NativeApi = new NativeApi();
            //panel.Children.Add(reactView);
            
            //webview = new WebView();
            //panel.Children.Add(webview);
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
            for (int i = 0, j = 0; i < 100; i++) {
                webview.ExecuteScript("i = " + ++j);
                webview.ExecuteScript("i = " + ++j);
                webview.ExecuteScript("i = " + ++j);
                webview.ExecuteScript("i = " + ++j);
                var result = webview.EvaluateScript<int>("i");
                if (result != j) {
                    throw new InvalidOperationException();
                }
                webview.ExecuteScript("i = " + ++j);
                //Title = result + "";
            }
            Console.WriteLine("Done");
        }
    }
}
