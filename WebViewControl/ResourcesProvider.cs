using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WebViewControl {

    internal class ResourcesProvider {

        public void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
            var stream = ResourcesManager.TryGetResource(url, true, out string extension);

            if (stream != null) {
                resourceHandler.RespondWith(stream, extension);
            }
        }
    }
}
