using System;
using System.Collections.Generic;

namespace WebViewControl {

    /// <summary>
    /// Mime types of the resources served through the custom schemes, for engines that don't provide a lookup of their own.
    /// </summary>
    internal static class MimeTypes {

        private static readonly Dictionary<string, string> MimeTypesByExtension = new(StringComparer.OrdinalIgnoreCase) {
            ["html"] = "text/html",
            ["htm"] = "text/html",
            ["js"] = "text/javascript",
            ["mjs"] = "text/javascript",
            ["css"] = "text/css",
            ["json"] = "application/json",
            ["map"] = "application/json",
            ["txt"] = "text/plain",
            ["xml"] = "text/xml",
            ["svg"] = "image/svg+xml",
            ["png"] = "image/png",
            ["jpg"] = "image/jpeg",
            ["jpeg"] = "image/jpeg",
            ["gif"] = "image/gif",
            ["bmp"] = "image/bmp",
            ["ico"] = "image/x-icon",
            ["webp"] = "image/webp",
            ["woff"] = "font/woff",
            ["woff2"] = "font/woff2",
            ["ttf"] = "font/ttf",
            ["otf"] = "font/otf",
            ["eot"] = "application/vnd.ms-fontobject",
            ["wasm"] = "application/wasm",
            ["mp3"] = "audio/mpeg",
            ["wav"] = "audio/wav",
            ["mp4"] = "video/mp4",
            ["webm"] = "video/webm",
            ["pdf"] = "application/pdf",
        };

        /// <summary>
        /// Returns the mime type of the extension (without the dot), or an empty string when unknown.
        /// </summary>
        public static string GetMimeType(string extension) {
            return MimeTypesByExtension.TryGetValue(extension ?? "", out var mimeType) ? mimeType : string.Empty;
        }
    }
}
