using System;
using System.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace Example.Avalonia {

    internal class MainWindow : Window {

        private TabControl tabs;

        public MainWindow() {
            WebView.OsrEnabled = false;
            InitializeComponent();

            CreateTab();
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);

            tabs = this.FindControl<TabControl>("tabs");
        }

        public void CreateTab() {
            ((IList)tabs.Items).Add(new TabItem() {
                Header = "View " + tabs.ItemCount,
                Content = new View()
            });
        }

        private View SelectedView => (View) tabs.SelectedContent;

        private void OnNewTabClick(object sender, RoutedEventArgs e) {
            CreateTab();
        }

        private void OnCallMainViewMenuItemClick(object sender, RoutedEventArgs e) {
            SelectedView.CallMainViewMenuItemClick();
        }

        private void OnCallInnerViewMenuItemClick(object sender, RoutedEventArgs e) {
            SelectedView.CallInnerViewMenuItemClick();
        }

        private void OnCallInnerViewPluginMenuItemClick(object sender, RoutedEventArgs e) {
            SelectedView.CallInnerViewPluginMenuItemClick();
        }

        private void OnShowDevTools(object sender, RoutedEventArgs e) {
            SelectedView.ShowDevTools();
        }

        private void OnToggleIsEnabled(object sender, RoutedEventArgs e) {
            SelectedView.ToggleIsEnabled();
        }
    }
}