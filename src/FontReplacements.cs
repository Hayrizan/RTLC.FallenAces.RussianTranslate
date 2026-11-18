using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

namespace RTLC.FallenAces
{
    internal sealed class FontReplacements
    {
        private readonly RussianAssets assets;
        private readonly string diagnosticsDirectory;
        private readonly ManualLogSource log;
        private readonly Dictionary<string, string> mappings;
        private readonly Dictionary<string, TMP_FontAsset> replacements = new Dictionary<string, TMP_FontAsset>();
        private readonly Dictionary<(Material, Material), Material> presets = new Dictionary<(Material, Material), Material>();
        private readonly HashSet<int> reported = new HashSet<int>();
        private readonly List<object> report = new List<object>();
        private int writtenCount = -1;
        public FontReplacements(RussianAssets assets, string diagnosticsDirectory, ManualLogSource log)
        {
            this.assets = assets; this.diagnosticsDirectory = diagnosticsDirectory; this.log = log;
            mappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(assets.Text("fonts/fonts.json").text)
                ?? throw new InvalidDataException("Font mappings are empty in rtlc.bundle.");
            log.LogInfo("Loaded " + (mappings == null ? 0 : mappings.Count) + " font replacement mappings.");
        }
        public void Apply(TMP_Text text)
        {
            var original = text.font;
            if (!original || original.name.StartsWith("RTLC_", StringComparison.Ordinal)) return;
            Inspect(original);
            string path;
            if (!mappings.TryGetValue(original.name, out path) && !mappings.TryGetValue(original.faceInfo.familyName, out path)) return;
            if (!replacements.TryGetValue(path, out var replacement))
            {
                replacements[path] = null; // Report a bad mapping once, not on every frame.
                try
                {
                    if (Path.GetFileName(path) != path) throw new IOException("Invalid bundled font name: " + path);
                    Font font = assets.Font(path);
                    replacement = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                    if (!replacement) throw new IOException("Could not create font asset: " + path);
                    replacement.name = "RTLC_" + Path.GetFileNameWithoutExtension(path);
                    UnityEngine.Object.DontDestroyOnLoad(replacement);
                    replacements[path] = replacement;
                    log.LogInfo("Font replacement: " + original.name + " -> " + path);
                }
                catch (Exception e) { log.LogError(e.Message); }
            }
            if (!replacement) return;
            var source = text.fontSharedMaterial;
            var atlas = replacement.material;
            text.font = replacement;
            if (!source || !atlas) return;
            var key = (source, atlas);
            if (!presets.TryGetValue(key, out var preset))
            {
                // Keep the game's face, outline and shadow settings. Only the
                // atlas texture and its SDF sampling parameters belong to the new font.
                preset = new Material(source);
                preset.mainTexture = atlas.mainTexture;
                float sourceRange = source.GetFloat("_GradientScale") / original.faceInfo.pointSize;
                float targetRange = atlas.GetFloat("_GradientScale") / replacement.faceInfo.pointSize;
                float effectScale = sourceRange / targetRange;
                foreach (var property in new[] { "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_UnderlayDilate", "_UnderlaySoftness", "_UnderlayOffsetX", "_UnderlayOffsetY" })
                    if (preset.HasProperty(property))
                        preset.SetFloat(property, source.GetFloat(property) * effectScale);
                foreach (var property in new[] { "_TextureWidth", "_TextureHeight", "_GradientScale", "_WeightNormal", "_WeightBold" })
                    if (preset.HasProperty(property) && atlas.HasProperty(property))
                        preset.SetFloat(property, atlas.GetFloat(property));
                presets.Add(key, preset);
            }
            text.fontSharedMaterial = preset;
            text.UpdateMeshPadding();
        }
        private void Inspect(TMP_FontAsset font)
        {
            if (!reported.Add(font.GetInstanceID())) return;
            string alphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            string missingAtlas = new string(alphabet.Where(c => !font.characterLookupTable.ContainsKey(c)).ToArray());
            var source = font.sourceFontFile;
            string missingSource = source ? new string(alphabet.Where(c => !source.HasCharacter(c)).ToArray()) : null;
            report.Add(new { asset = font.name, family = font.faceInfo.familyName, style = font.faceInfo.styleName, population = font.atlasPopulationMode.ToString(), source = source ? source.name : null, missingAtlas, missingSource });
            if (missingAtlas.Length > 0) log.LogWarning("Cyrillic absent from font atlas: " + font.name + " (" + font.faceInfo.familyName + "), " + missingAtlas.Length + " letters. See Diagnostics/fonts.json.");
        }
        public void WriteReport()
        {
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>()) if (font && !font.name.StartsWith("RTLC_", StringComparison.Ordinal)) Inspect(font);
            if (report.Count != writtenCount)
            {
                Directory.CreateDirectory(diagnosticsDirectory);
                File.WriteAllText(Path.Combine(diagnosticsDirectory, "fonts.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
                writtenCount = report.Count;
            }
        }
    }
}
