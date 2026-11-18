using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using TMPro;
using System.Runtime.CompilerServices;

namespace RTLC.FallenAces
{
    internal sealed class TextureReplacements
    {
        private readonly string folder;
        private readonly string exportFolder;
        private readonly ManualLogSource log;
        private readonly bool export;
        private readonly ConditionalWeakTable<Texture2D, object> processed = new ConditionalWeakTable<Texture2D, object>();
        private readonly HashSet<string> exported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<Texture2D> fontAtlases = new HashSet<Texture2D>();
        private readonly Dictionary<string, byte[]> images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        public TextureReplacements(string root, ManualLogSource log, bool export)
        {
            folder = Path.Combine(root, "Textures"); exportFolder = Path.Combine(root, "TextureDump"); this.log = log; this.export = export;
            if (export) Directory.CreateDirectory(exportFolder);
            foreach (string path in Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories) : Array.Empty<string>())
            {
                string key = Path.GetFileNameWithoutExtension(path);
                if (images.ContainsKey(key)) throw new InvalidDataException("Duplicate texture replacement filename: " + key);
                images.Add(key, File.ReadAllBytes(path));
            }
        }
        internal static string Key(Texture2D texture)
        {
            string name = texture.name;
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name + "__" + texture.width + "x" + texture.height;
        }
        public void ProtectFontAtlases()
        {
            fontAtlases.Clear();
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font && font.atlasTextures != null) foreach (var atlas in font.atlasTextures) if (atlas) fontAtlases.Add(atlas);
        }
        public void Apply(Texture2D texture)
        {
            if (!texture || processed.TryGetValue(texture, out _)) return;
            // Never modify font atlases or transient render targets.
            if (fontAtlases.Contains(texture)) return;
            processed.Add(texture, new object());
            string key = Key(texture);
            try
            {
                if (export && exported.Add(key)) Export(texture, Path.Combine(exportFolder, key + ".png"));
                if (!images.TryGetValue(key, out var png)) return;
                // Validate before mutating the original object (sprites keep their rects and pivots).
                var test = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(test, png, false) || test.width != texture.width || test.height != texture.height)
                        throw new InvalidDataException("PNG dimensions must match " + key);
                }
                finally { UnityEngine.Object.Destroy(test); }
                var filter = texture.filterMode; var wrap = texture.wrapMode; int aniso = texture.anisoLevel;
                if (!ImageConversion.LoadImage(texture, png, false)) throw new IOException("Unity LoadImage rejected original texture format for " + key);
                texture.filterMode = filter; texture.wrapMode = wrap; texture.anisoLevel = aniso;
                log.LogInfo("Replaced texture " + key);
            }
            catch (Exception e) { log.LogError("Texture " + key + ": " + e.Message); }
        }
        private static void Export(Texture2D source, string path)
        {
            var previous = RenderTexture.active;
            var render = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, render); RenderTexture.active = render;
                copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); copy.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(copy));
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); if (copy) UnityEngine.Object.Destroy(copy); }
        }
    }
}
