using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace WebViewControl {
    
    partial class WebView {

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
    }
}
