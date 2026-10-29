using System.Collections.Generic;

namespace WebViewControl {

    public static partial class ResourcesManager {

        // the native web views do not expose a mime type database, keep the types we expect to serve from embedded resources
        private static readonly IReadOnlyDictionary<string, string> MimeTypes = new Dictionary<string, string> {
            ["html"] = "text/html", ["htm"] = "text/html", ["xhtml"] = "application/xhtml+xml",
            ["js"] = "text/javascript", ["mjs"] = "text/javascript", ["css"] = "text/css",
            ["json"] = "application/json", ["map"] = "application/json", ["xml"] = "text/xml", ["txt"] = "text/plain",
            ["csv"] = "text/csv", ["md"] = "text/markdown", ["wasm"] = "application/wasm", ["pdf"] = "application/pdf",
            ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg", ["gif"] = "image/gif",
            ["svg"] = "image/svg+xml", ["webp"] = "image/webp", ["ico"] = "image/x-icon", ["bmp"] = "image/bmp", ["avif"] = "image/avif",
            ["woff"] = "font/woff", ["woff2"] = "font/woff2", ["ttf"] = "font/ttf", ["otf"] = "font/otf", ["eot"] = "application/vnd.ms-fontobject",
            ["mp3"] = "audio/mpeg", ["wav"] = "audio/wav", ["ogg"] = "audio/ogg", ["mp4"] = "video/mp4", ["webm"] = "video/webm",
        };

        private static string GetNativeMimeType(string extension) {
            return MimeTypes.TryGetValue(extension.ToLowerInvariant(), out var mimeType) ? mimeType : "application/octet-stream";
        }
    }
}
