using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.FallenAces
{
    public sealed partial class Plugin
    {
        private static void WantedAwakePostfix(object __instance)
        {
            var image = AccessTools.Field(__instance.GetType(), "_image").GetValue(__instance) as Image;
            if (image == null || image.sprite == null || image.GetComponent<WantedRasterResources>() != null) return;
            // A hand-translated texture supplied by the user takes precedence.
            string textureFile = TextureReplacements.Key(image.sprite.texture) + ".png";
            if (Directory.GetFiles(Path.Combine(Instance.Root, "Textures"), textureFile, SearchOption.AllDirectories).Length > 0) return;
            try
            {
                var texture = Instance.RenderWantedTexture();
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), image.sprite.pixelsPerUnit);
                sprite.name = "RTLC_WantedRussian";
                var owned = image.gameObject.AddComponent<WantedRasterResources>();
                owned.Texture = texture;
                owned.Sprite = sprite;
                image.sprite = sprite;
                // Retain the game's material, fill texture, visibility, positioning
                // and _FillupFactor animation. Only the letter mask changes.
                image.material.SetTexture("_MainTex", texture);
                image.SetMaterialDirty();
            }
            catch (Exception e) { Instance.Logger.LogError("Wanted label: " + e); }
        }

        private Texture2D RenderWantedTexture()
        {
            const int width = 1024, height = 256;
            var root = new GameObject("RTLC_WantedRasterCamera");
            root.transform.position = new Vector3(100000f, 100000f, 100000f);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D result = null;
            try
            {
                var camera = root.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.aspect = (float)width / height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 20f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                var go = new GameObject("RTLC_WantedRasterText", typeof(RectTransform), typeof(TextMeshPro));
                go.layer = 31;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.forward * 10f;
                var label = go.GetComponent<TextMeshPro>();
                label.font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "Broadway CG Regular SDF") ?? TMP_Settings.defaultFontAsset;
                Fonts.Apply(label);
                label.rectTransform.sizeDelta = new Vector2(width, height);
                label.isOrthographic = true;
                label.margin = new Vector4(20f, 10f, 20f, 10f);
                label.text = Catalog.Translate("WANTED");
                label.fontSize = 220f;
                label.fontSizeMax = 220f;
                label.fontSizeMin = 96f;
                label.enableAutoSizing = true;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.alignment = TextAlignmentOptions.MidlineGeoAligned;
                label.color = Color.white;
                label.outlineColor = Color.black;
                label.outlineWidth = 0.1f;
                label.ForceMeshUpdate(true, true);
                camera.Render();
                RenderTexture.active = target;
                result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.name = "RTLC_WantedRussian";
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                result.Apply();
                result.filterMode = FilterMode.Bilinear;
                result.wrapMode = TextureWrapMode.Clamp;
                return result;
            }
            catch { if (result != null) Destroy(result); throw; }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(root);
            }
        }
    }

    internal sealed class WantedRasterResources : MonoBehaviour
    {
        internal Texture2D Texture;
        internal Sprite Sprite;
        private void OnDestroy()
        {
            if (Sprite != null) Destroy(Sprite);
            if (Texture != null) Destroy(Texture);
        }
    }
}
