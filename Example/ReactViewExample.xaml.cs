using Example.ViewAdapters;
using System;
using System.IO;
using System.Windows;
using WebViewControl;

namespace Example {
    /// <summary>
    /// Interaction logic for ReactViewExample.xaml
    /// </summary>
    public partial class ReactViewExample : Window {
        private const string dragSourceViewId = "drag-source-view";
        private const string dropTargetViewId = "drop-target-view";
        private DragSourceViewModule dragSourceView;
        private DropTargetViewModule dropTargetView;

        private readonly ViewAdapter viewAdapter;
        private readonly ViewAdapter dragSourceViewAdapter;
        private readonly ViewAdapter dropTargetViewAdapter;

        public ReactViewExample() {
            InitializeComponent();

            dragSourceView = new DragSourceViewModule();
            dropTargetView = new DropTargetViewModule();
            exampleView.AttachInnerView(dragSourceView, dragSourceViewId);
            exampleView.AttachInnerView(dropTargetView, dropTargetViewId);

            viewAdapter = new ViewAdapter(reactView: exampleView, frameName: string.Empty);
            dragSourceViewAdapter = new ViewAdapter(reactView: exampleView, frameName: dragSourceViewId);
            dropTargetViewAdapter = new ViewAdapter(reactView: exampleView, frameName: dropTargetViewId);
        }

        private void OnExampleViewClick(SomeType arg) {

        }

        private void OnCallInnerViewPluginMenuItemClick(object sender, RoutedEventArgs e) {
            exampleView.WithPlugin<ViewPlugin>(dragSourceViewId).Test();
        }

        private void OnShowDevTools(object sender, RoutedEventArgs e) {
            exampleView.ShowDeveloperTools();
        }
    }
}
