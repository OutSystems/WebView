using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows;
using System.Windows.Media;
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

            public void ExecuteOnItem(IJsObj item, string newValue, string[] items) {
                ((JsObj)item).Execute(newValue);
            }
        }

        //private ReactView reactView;
        //private WebView webview;

        public MainWindow() {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
            InitializeComponent();

            webview.Focus();

            var scrollListener = new Listener();
            scrollListener.NotificationReceived = Hello;
            webview.RegisterJavascriptObject(ScrollListenerObj, scrollListener);

            webview.ShowDeveloperTools();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            webview.BeforeResourceLoad += Webview_BeforeResourceLoad;

            webview.MouseRightButtonUp += Webview_MouseRightButtonUp;
            webview.MouseLeftButtonUp += Webview_MouseLeftButtonUp;
            webview.RegisterJavascriptObject("NativeApi", new NativeApi());
            webview.JavascriptContextCreated += Webview_JavascriptContextCreated;
            //webview.AllowDrop = true;
            //webview.PreviewDragEnter += Webview_PreviewDragLeave;
            //webview.PreviewDragOver += Webview_PreviewDragLeave;
            //webview.PreviewDragLeave += Webview_PreviewDragLeave;
            //webtView.BeforeResourceLoad += WebtView_BeforeResourceLoad;
            //webtView.BeforeNavigate += WebtView_BeforeNavigate;
            //webtView.ShowDeveloperTools();
            //reactView = new ReactView("Resources", "dist", "example.js");
            //reactView.NativeApi = new NativeApi();
            //panel.Children.Add(reactView);

            //webview = new WebView();
            //panel.Children.Add(webview);
            //webview.LoadFrom("Resources");
            //webview.Address = "https://www.google.pt/";
            /*webview.LoadHtml(
            @"
            <html>
                <body>hi</body>
            </html>");*/

            //webview.AllowDeveloperTools = true;
            //webview.BeforeNavigate += WebView_BeforeNavigate;
            //webview.BeforeResourceLoad += WebView_BeforeResourceLoad;
            //webview.Navigated += WebView_Navigated;
            //webview.ResourceLoadFailed += WebView_ResourceLoadFailed;
            //webview.JavascriptContextCreated += Webview_JavascriptContextReady;
        }

        private void Webview_ErrorOcurred(Exception obj) {
            
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            
        }

        private class Listener {

            public Action NotificationReceived;

            public void Notify() {
                // BeginInvoke otherwise if we try to execute some script  on the browser as a result of this notification, it will block forever
                Application.Current.Dispatcher.BeginInvoke(
                    (Action)(() => {
                        if (NotificationReceived != null) {
                            NotificationReceived();
                        }
                    }));
            }
        }

        private const string ScrollListenerObj = "__ssScrollChangedListener__";

        private void Webview_JavascriptContextCreated() {
            
            //webview.ExecuteScript("window.addEventListener('scroll', function() { " + ScrollListenerObj + ".notify(); })");

            
        }

        private void Hello() {
            Console.WriteLine("scroll");
        }

        private void Webview_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            
        }

        private void Webview_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            
        }

        private void Webview_PreviewDragLeave(object sender, DragEventArgs e) {
            e.Handled = true;
        }

        private void WebtView_BeforeNavigate1(WebView.Request obj) {
            throw new NotImplementedException();
        }

        private void WebtView_BeforeResourceLoad(WebView.ResourceHandler obj) {
            if (obj.Url.StartsWith("https://www.google.pt/images/")) {
                obj.Redirect("https://media.licdn.com/mpr/mpr/shrink_200_200/AAEAAQAAAAAAAAd2AAAAJGQ5NTBhY2U2LWM2NDEtNGJkMC1iOTdiLWU4ODE2MDdmZThjNg.png");
            }
        }

        private void WebtView_BeforeNavigate(WebView.Request obj) {
            
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

        private static Assembly Resolver(object sender, ResolveEventArgs args) {
            return null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static object GetValue() {
            return "hello";
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string GetValueStr() {
            return "hello";
        }


        class MyObj {
            public string Value;
            
        }

        private void Button_Click(object sender, RoutedEventArgs e) {

            //new Window().Show();
            //Close();
            //webview.ExecuteScript("console.log('1'); var start = new Date().getTime(); var end = start; while (end < start + 5000) { end = new Date().getTime(); } ");
            //webview.ExecuteScript("console.log('2')");

            //webview.Dispose();
            var rx = webview.EvaluateScript<object>("console.log('1'); var start = new Date().getTime(); var end = start; while (end < start + 5000) { end = new Date().getTime(); } ", TimeSpan.FromSeconds(1));
            
            return;

            webview.IsHistoryDisabled = true;
            webview.GoBack();
            return;
            //var innerview = (FrameworkElement)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(webview, 0), 0);
            //innerview.MouseLeftButtonUp += Innerview_MouseLeftButtonUp;
            //webview.Dispose();
            try {
                webview.EvaluateScript<object>("test1()");
                //webview.ExecuteScript("(function () { var j = 0; for(var i= 0; i < 100000000000; i++) j++; return j; })()");
                //webview.EvaluateScript<object>("1");
            } catch (Exception ex) {
                var r = ex.ToString();
            }
            //var result0 = webtView.EvaluateScriptFunction<int[][]>("test1");
            //var result1 = reactView.EvaluateScriptFunction<MyObj[]>("test");
            //var stopwatch = new System.Diagnostics.Stopwatch();

            //var funcs = new Dictionary<string, Func<dynamic, string>>();
            //for(var i = 0; i < 1000; i++) {
            //    funcs["hello" + i] = null;
            //}
            //funcs["hello"] = (dynamic val) => JavascriptSerializer.Serialize(val);

            //result = "";
            //stopwatch.Start();
            //for (int i = 0, j = 0; i < 10000; i++) {
            //    var str = GetValueStr();
            //    result += JavascriptSerializer.Serialize(str);
            //}
            //stopwatch.Stop();
            //Console.WriteLine(result.Length);
            //Console.WriteLine(stopwatch.ElapsedMilliseconds);

            //result = "";
            //stopwatch.Start();
            //for (int i = 0, j = 0; i < 10000; i++) {
            //    var str = GetValueStr();
            //    result += funcs["hello"](str);
            //}
            //stopwatch.Stop();
            //Console.WriteLine(result.Length);
            //Console.WriteLine(stopwatch.ElapsedMilliseconds);

            //result = "";
            //stopwatch.Start();
            //for (int i = 0, j = 0; i < 10000; i++) {
            //    var str = GetValue();
            //    if (str is string) {
            //        result += JavascriptSerializer.Serialize((string)str);
            //    }
            //}
            //stopwatch.Stop();
            //Console.WriteLine(result.Length);
            //Console.WriteLine(stopwatch.ElapsedMilliseconds);


            //for (int i = 0, j = 0; i < 100; i++) {
            //    webview.ExecuteScript("i = " + ++j);
            //    webview.ExecuteScript("i = " + ++j);
            //    webview.ExecuteScript("i = " + ++j);
            //    webview.ExecuteScript("i = " + ++j);
            //    var result = webview.EvaluateScript<int>("i");
            //    if (result != j) {
            //        throw new InvalidOperationException();
            //    }
            //    webview.ExecuteScript("i = " + ++j);
            //    //Title = result + "";
            //}
            //Console.WriteLine("Done");
        }

        [DebuggerNonUserCode]
        private void Webview_BeforeResourceLoad(WebView.ResourceHandler obj) {
            //throw new NotImplementedException();
        }

        private void Innerview_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            
        }

        private void Label_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            DataObject dragData = new DataObject(typeof(ModelObject), new ModelObject());
            DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link | DragDropEffects.Scroll);
        }
    }
}
