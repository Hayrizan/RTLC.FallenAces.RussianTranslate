using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.FallenAces
{
    public sealed partial class Plugin
    {
        private Type tutorialPopupType, controlPromptType, mashIndicatorType, noteHandlerType, radioOnHudType;
        private FieldInfo tutorialLabelField, controlKeyField, controlVerbField, controlImageField, controlFillField, mashKeyField;
        private FieldInfo lowerNoteTextField, radioNameField, loadingWorldTitleField;
        private FieldInfo loadingOverTextField, loadingUnderTextMeshField, loadingTipField;
        private Type shootingScoreType;
        private FieldInfo shootingScoreField;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object> fittedRadioNames = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object>();

        private void InstallGameplayLayout(Assembly game)
        {
            tutorialPopupType = game.GetType("FallenAces.HUD.TutorialPopupHandler", true);
            controlPromptType = game.GetType("FallenAces.HUD.ControlPrompt", true);
            mashIndicatorType = game.GetType("FallenAces.MashKeyIndicator", true);
            noteHandlerType = game.GetType("FallenAces.HUD.NoteHandler", true);
            radioOnHudType = game.GetType("FallenAces.RadioOnHud", true);
            lowerNoteTextField = AccessTools.Field(noteHandlerType, "_lowerNoteText");
            radioNameField = AccessTools.Field(radioOnHudType, "_characterNameTextMesh");
            loadingWorldTitleField = AccessTools.Field(loadingScreenType, "_worldTitleTextMesh");
            loadingOverTextField = AccessTools.Field(loadingScreenType, "_overTextMesh");
            loadingUnderTextMeshField = AccessTools.Field(loadingScreenType, "_underTextMesh");
            loadingTipField = AccessTools.Field(loadingScreenType, "_tipText");
            shootingScoreType = game.GetType("FallenAces.ShootingRangeScoreText", true);
            shootingScoreField = AccessTools.Field(shootingScoreType, "_textMesh");
            harmony.Patch(AccessTools.Method(shootingScoreType, "SetText"), postfix: new HarmonyMethod(typeof(Plugin), nameof(ShootingScorePostfix)));
            var wantedType = game.GetType("FallenAces.HUD.WantedText", true);
            harmony.Patch(AccessTools.Method(wantedType, "Awake"), postfix: new HarmonyMethod(typeof(Plugin), nameof(WantedAwakePostfix)));
            tutorialLabelField = AccessTools.Field(tutorialPopupType, "_text");
            controlKeyField = AccessTools.Field(controlPromptType, "_textMeshForButton");
            controlVerbField = AccessTools.Field(controlPromptType, "_textMeshForVerb");
            controlImageField = AccessTools.Field(controlPromptType, "_buttonImage");
            controlFillField = AccessTools.Field(controlPromptType, "_buttonFillupImage");
            mashKeyField = AccessTools.Field(mashIndicatorType, "_promptKeyTextMesh");
            harmony.Patch(AccessTools.Method(tutorialPopupType, "Show"), postfix: new HarmonyMethod(typeof(Plugin), nameof(TutorialShownPostfix)));
            harmony.Patch(AccessTools.Method(controlPromptType, "Setup"), postfix: new HarmonyMethod(typeof(Plugin), nameof(ControlPromptSetupPostfix)));
            var chapterEnd = game.GetType("FallenAces.ChapterEndScreen", true);
            harmony.Patch(AccessTools.Method(chapterEnd, "ShowTitle"), prefix: new HarmonyMethod(typeof(Plugin), nameof(ChapterEndTitlePrefix)));
        }

        private void ApplyLoadingOutline(TMP_Text text, float opacity = 1f)
        {
            var material = text.fontMaterial;
            if (!material.HasProperty("_UnderlayColor")) return;
            if (text.outlineWidth == 0f && material.IsKeywordEnabled("UNDERLAY_ON") &&
                Mathf.Approximately(material.GetColor("_UnderlayColor").a, opacity) &&
                Mathf.Approximately(material.GetFloat("_UnderlayDilate"), 0.18f)) return;
            // TMP's normal SDF outline straddles the glyph edge and consumes its
            // thin face. A zero-offset dilated underlay draws behind the unchanged
            // glyph, exposing only the exterior black stroke.
            text.outlineWidth = 0f;
            material = text.fontMaterial;
            material.DisableKeyword("OUTLINE_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, opacity));
            material.SetFloat("_UnderlayOffsetX", 0f);
            material.SetFloat("_UnderlayOffsetY", 0f);
            material.SetFloat("_UnderlayDilate", 0.18f);
            material.SetFloat("_UnderlaySoftness", 0f);
            text.extraPadding = true;
            text.UpdateMeshPadding();
        }

        private bool FitGameplayLabel(TMP_Text text)
        {
            var score = shootingScoreType == null ? null : text.GetComponentInParent(shootingScoreType);
            if (score != null && shootingScoreField.GetValue(score) as TMP_Text == text)
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.margin = new Vector4(0.1f, 0.05f, 0.1f, 0.05f);
                text.fontSizeMax = OriginalSize(text) * 0.85f;
                text.fontSizeMin = OriginalSize(text) * 0.4f;
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Overflow;
                return true;
            }
            var note = noteHandlerType == null ? null : text.GetComponentInParent(noteHandlerType);
            if (note != null && lowerNoteTextField.GetValue(note) as TMP_Text == text)
            {
                ApplyLoadingOutline(text);
                return true;
            }
            var radio = radioOnHudType == null ? null : text.GetComponentInParent(radioOnHudType);
            if (radio != null && radioNameField.GetValue(radio) as TMP_Text == text)
            {
                var rect = text.rectTransform;
                float size = OriginalSize(text);
                if (!fittedRadioNames.TryGetValue(text, out _))
                {
                    fittedRadioNames.Add(text, new object());
                    var corners = new Vector3[4];
                    rect.GetWorldCorners(corners);
                    Vector3 top = (corners[1] + corners[2]) * 0.5f;
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(rect.rect.height, size * 2.7f));
                    rect.position = top;
                    var canvas = text.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.rootCanvas.transform is RectTransform canvasRect)
                    {
                        rect.GetWorldCorners(corners);
                        float bottom = canvasRect.InverseTransformPoint(corners[0]).y;
                        float safeBottom = canvasRect.rect.yMin + 12f;
                        if (bottom < safeBottom) rect.position += canvasRect.TransformVector(Vector3.up * (safeBottom - bottom));
                    }
                }
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Top;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.lineSpacing = 0f;
                text.fontSizeMax = size;
                text.fontSizeMin = size * 0.8f;
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Overflow;
                return true;
            }
            var loading = loadingScreenType == null ? null : text.GetComponentInParent(loadingScreenType);
            if (loading != null && loadingWorldTitleField.GetValue(loading) as TMP_Text == text)
            {
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.04f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(0.96f, rect.anchorMax.y);
                rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
                rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Left;
                text.fontSizeMax = OriginalSize(text) * 0.9f;
                text.fontSizeMin = text.fontSizeMax * 0.6f;
                text.enableAutoSizing = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                return true;
            }
            if (loading != null &&
                (loadingOverTextField.GetValue(loading) as TMP_Text == text ||
                 loadingUnderTextMeshField.GetValue(loading) as TMP_Text == text))
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                return true;
            }
            if (loading != null && loadingTipField.GetValue(loading) as TMP_Text == text)
            {
                var rect = text.rectTransform;
                var parent = rect.parent as RectTransform;
                if (parent != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.zero;
                    rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(32f, 55f);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parent.rect.width * 0.58f);
                }
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.BottomLeft;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.68f;
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Overflow;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(rect.rect.height, OriginalSize(text) * 2.7f));
                return true;
            }
            var popup = tutorialPopupType == null ? null : text.GetComponentInParent(tutorialPopupType);
            if (popup != null && tutorialLabelField.GetValue(popup) as TMP_Text == text)
            {
                text.margin = new Vector4(8f, 8f, 8f, 8f);
                text.textWrappingMode = TextWrappingModes.Normal;
                text.alignment = TextAlignmentOptions.Midline;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = OriginalSize(text) * 0.42f;
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Overflow;
                return true;
            }
            var prompt = controlPromptType == null ? null : text.GetComponentInParent(controlPromptType);
            if (prompt != null && controlKeyField.GetValue(prompt) as TMP_Text == text)
            {
                text.margin = Vector4.zero;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.8f;
                text.enableAutoSizing = true;
                return true;
            }
            if (prompt != null && controlVerbField.GetValue(prompt) as TMP_Text == text)
            {
                text.margin = Vector4.zero;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.7f;
                text.enableAutoSizing = true;
                return true;
            }
            var mash = mashIndicatorType == null ? null : text.GetComponentInParent(mashIndicatorType);
            if (mash != null && mashKeyField.GetValue(mash) as TMP_Text == text)
            {
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.margin = Vector4.zero;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.65f;
                text.enableAutoSizing = true;
                return true;
            }
            return false;
        }

        private static void TutorialShownPostfix(object __instance)
        {
            var text = Instance.tutorialLabelField.GetValue(__instance) as TMP_Text;
            if (text != null) Instance.FitGameplayLabel(text);
        }

        private static void ShootingScorePostfix(object __instance)
        {
            if (Instance.shootingScoreField.GetValue(__instance) is TMP_Text text)
                Instance.FitGameplayLabel(text);
        }

        private static void ControlPromptSetupPostfix(object __instance)
        {
            var self = Instance;
            var key = self.controlKeyField.GetValue(__instance) as TMP_Text;
            var verb = self.controlVerbField.GetValue(__instance) as TMP_Text;
            var image = self.controlImageField.GetValue(__instance) as Image;
            var fill = self.controlFillField.GetValue(__instance) as Image;
            if (key == null || verb == null || image == null) return;
            // Pooled prompts may retain a previously fitted size. Measure at the
            // original size so repeated uses do not progressively shrink them.
            key.enableAutoSizing = false;
            key.fontSize = self.OriginalSize(key);
            float width = Mathf.Max(40f, key.GetPreferredValues(self.Catalog.Translate(key.text)).x + 20f);
            image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (fill != null) fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            key.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 12f);
            self.FitGameplayLabel(key);
            var corners = new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            Vector3 right = verb.rectTransform.parent.InverseTransformPoint(corners[2]);
            Vector3 position = verb.rectTransform.localPosition;
            verb.rectTransform.pivot = new Vector2(0f, verb.rectTransform.pivot.y);
            verb.rectTransform.localPosition = new Vector3(right.x + 10f, position.y, position.z);
            verb.enableAutoSizing = false;
            verb.fontSize = self.OriginalSize(verb);
            float verbWidth = verb.GetPreferredValues(self.Catalog.Translate(verb.text)).x + 8f;
            var canvas = verb.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas.transform is RectTransform canvasRect)
            {
                float x = canvasRect.InverseTransformPoint(verb.rectTransform.position).x;
                float scale = Mathf.Abs(canvasRect.InverseTransformVector(verb.rectTransform.TransformVector(Vector3.right)).x);
                if (scale > 0.0001f)
                    verbWidth = Mathf.Min(verbWidth, Mathf.Max(80f, (canvasRect.rect.xMax - x - 24f) / scale));
            }
            verb.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, verbWidth);
            self.FitGameplayLabel(verb);
        }

        private static void ChapterEndTitlePrefix(GameObject ____title, TextMeshProUGUI ____chapterTitle)
        {
            if (____title == null || ____title.GetComponentsInChildren<TMP_Text>(true).Any(t => t.name == "RTLC_ChapterEndTitle")) return;
            // The stock heading is raster artwork. Replace only that image, keeping
            // its animated parent and the rest of the end-of-chapter screen intact.
            var images = ____title.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null).ToArray();
            var image = images.FirstOrDefault(i => i.sprite.name.Replace("_", "").Replace(" ", "").IndexOf("endofchapter", StringComparison.OrdinalIgnoreCase) >= 0);
            if (image == null && images.Length == 1) image = images[0];
            if (image == null) return;
            var go = new GameObject("RTLC_ChapterEndTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(image.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (____chapterTitle != null) label.font = ____chapterTitle.font;
            Instance.Fonts.Apply(label);
            label.text = Instance.Catalog.Translate("END OF CHAPTER");
            label.alignment = TextAlignmentOptions.MidlineGeoAligned;
            float originalSize = ____chapterTitle != null ? ____chapterTitle.fontSize : label.fontSize;
            label.fontSize = originalSize;
            label.fontSizeMax = originalSize;
            label.fontSizeMin = originalSize * 0.55f;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.raycastTarget = false;
            Instance.ApplyLoadingOutline(label, 1f);
            image.enabled = false;
        }
    }
}
