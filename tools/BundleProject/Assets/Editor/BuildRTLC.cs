using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildRTLC
{
    private static readonly string[] Catalogs = { "data.ru.json", "subtitles.ru.json", "ui.ru.json" };
    private static readonly string[] Fonts =
    {
        "Broadway Cyrillic Extended.ttf", "brushscriptstd.ttf",
        "JosefinSansExt-Regular.ttf", "Troubleside.ttf"
    };

    public static void Build()
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (string catalog in Catalogs) names.Add("Assets/RTLC/translations/" + catalog);
        names.Add("Assets/RTLC/fonts/fonts.json");
        foreach (string font in Fonts) names.Add("Assets/RTLC/fonts/" + font);
        foreach (string name in names)
            if (!File.Exists(name) || AssetDatabase.LoadMainAssetAtPath(name) == null)
                throw new FileNotFoundException("Unity could not import " + name);

        const string output = "BundleOutput";
        Directory.CreateDirectory(output);
        var map = new[] { new AssetBundleBuild { assetBundleName = "rtlc.bundle", assetNames = names.ToArray() } };
        var options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle;
        if (BuildPipeline.BuildAssetBundles(output, map, options, BuildTarget.StandaloneWindows64) == null)
            throw new Exception("AssetBundle build failed");

        var bundle = AssetBundle.LoadFromFile(Path.GetFullPath(output + "/rtlc.bundle"));
        if (bundle == null) throw new Exception("Unity could not reopen rtlc.bundle");
        try
        {
            foreach (string catalog in Catalogs)
                Require<TextAsset>(bundle, "assets/rtlc/translations/" + catalog);
            Require<TextAsset>(bundle, "assets/rtlc/fonts/fonts.json");
            foreach (string font in Fonts)
                Require<Font>(bundle, "assets/rtlc/fonts/" + font);
        }
        finally { bundle.Unload(true); }
        Debug.Log("RTLC_BUNDLE_OK: " + names.Count + " assets");
    }

    private static void Require<T>(AssetBundle bundle, string path) where T : UnityEngine.Object
    {
        if (bundle.LoadAsset<T>(path.ToLowerInvariant()) == null)
            throw new Exception("Bundled asset missing: " + path);
    }
}
