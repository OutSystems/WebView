using System;

namespace WebViewControl {

    internal class ResourcesProvider {

        public void LoadEmbeddedResource(ResourceHandler resourceHandler, Uri url) {
            var stream = ResourcesManager.TryGetResource(url, true, out string extension);

            if (stream != null) {
                resourceHandler.RespondWith(stream, extension, preventStreamConcurrentAccess: true);
            }
        }
    }
}
