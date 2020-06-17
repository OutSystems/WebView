using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Script.Serialization;

namespace ReactViewControl {
    public class HotReloadDependenciesProvider : IDependenciesProvider {

        private Dictionary<string, List<string>> dependencies;
        private readonly Uri uri;
        private readonly string basePath;
        private const string ManifestPath = "manifest.json";

        public HotReloadDependenciesProvider(Uri uri) {
            this.uri = uri;
            basePath = getBaseSegmentFromUri();
            GetDependenciesFromUri();
        }

        public string getBaseSegmentFromUri() {
            return "/" + uri.Segments.Last();
        }

        string[] IDependenciesProvider.GetCssDependencies(string moduleName) {
            if (!dependencies.ContainsKey(moduleName)) {
                return new string[0];
            }

            return dependencies[moduleName]
                .Where(dependency => dependency.Contains(".css"))
                .Select(dependency => basePath + dependency)
                .ToArray();
        }

        string[] IDependenciesProvider.GetJsDependencies(string moduleName) {
            if (!dependencies.ContainsKey(moduleName)) {
                return new string[0];
            }

            return dependencies[moduleName]
                .FindAll(dependency => dependency.Contains(".js"))
                .Select(dependency =>  basePath + dependency)
                .Reverse().Skip(1).Reverse().ToArray() // remove self reference
                .ToArray();
        }

        private void GetDependenciesFromUri() {
            using (WebClient wc = new WebClient()) {
                var json = wc.DownloadString(new Uri(uri, ManifestPath));
                var serializer = new JavaScriptSerializer();
                dependencies = serializer.Deserialize<Dictionary<string, List<string>>>(json);
            }
        }
    }
}
