using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace RTLC.FallenAces
{
    internal sealed class RussianAssets
    {
        private const string Prefix = "assets/rtlc/";
        private readonly AssetBundle bundle;

        private RussianAssets(AssetBundle bundle) { this.bundle = bundle; }

        public static RussianAssets Open(string pluginDirectory)
        {
            string path = Path.Combine(pluginDirectory, "rtlc.bundle");
            if (!File.Exists(path)) throw new FileNotFoundException("Russian asset bundle is missing", path);
            var bundle = AssetBundle.LoadFromFile(path);
            if (!bundle) throw new InvalidDataException("Unity could not load rtlc.bundle: " + path);
            return new RussianAssets(bundle);
        }

        public TextAsset Text(string relativePath)
        {
            string name = Prefix + relativePath.ToLowerInvariant().Replace('\\', '/');
            var asset = bundle.LoadAsset<TextAsset>(name);
            if (!asset) throw new InvalidDataException("Asset is missing from rtlc.bundle: " + name);
            return asset;
        }

        public Font Font(string filename)
        {
            string name = Prefix + "fonts/" + filename.ToLowerInvariant();
            var asset = bundle.LoadAsset<Font>(name);
            if (!asset) throw new InvalidDataException("Font is missing from rtlc.bundle: " + name);
            return asset;
        }

        public IEnumerable<(string name, string contents)> Catalogs()
        {
            foreach (string name in new[] { "data.ru.json", "subtitles.ru.json", "ui.ru.json" })
                yield return (name, Text("translations/" + name).text);
        }
    }
}
