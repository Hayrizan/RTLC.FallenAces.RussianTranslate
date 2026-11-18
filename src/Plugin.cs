using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTLC.FallenAces
{
    [BepInPlugin(Id, "Fallen Aces — Русский перевод", "0.1.0")]
    [BepInProcess("Fallen Aces.exe")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Id = "rtlc.fallenaces.russiantranslate";
        internal static Plugin Instance;
        internal readonly TranslationCatalog Catalog = new TranslationCatalog();
        internal FontReplacements Fonts;
        internal TextureReplacements Textures;
        private RussianAssets assets;
        private Harmony harmony;
        private ConfigEntry<bool> exportTextures;
        private Type menuButtonType;
        private Type loadingScreenType, skipPromptType, hiddenIndicatorType;
        private FieldInfo loadingContinueField, loadingStatusField, skipTextField, hiddenTextField;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object> fittedLabels = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, OriginalFontSize> originalFontSizes = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, OriginalFontSize>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Transform, object> expandedSettingsPanels = new System.Runtime.CompilerServices.ConditionalWeakTable<Transform, object>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Dropdown, object> fittedDropdowns = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Dropdown, object>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object> measuredMenuLabels = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, object>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, SourceText> fittedSkipSources = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, SourceText>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, SourceText> directTextSources = new System.Runtime.CompilerServices.ConditionalWeakTable<TMP_Text, SourceText>();
        private Type profileNameType;
        private Type profileSlotType;
        private Type journalListEntryType;
        private FieldInfo journalEntryNameField;
        private FieldInfo profileDetailsField;
        private FieldInfo profileStateField;
        private Image largeSettingsPanelImage;
        private RectTransform largeSettingsPanelRect;
        internal string Root;
        private sealed class SourceText { public string Value; public SourceText(string value) { Value = value; } }
        private sealed class OriginalFontSize { public float Value; public OriginalFontSize(float value) { Value = value; } }

        private void Awake()
        {
            Instance = this;
            Root = Path.GetDirectoryName(Info.Location);
            exportTextures = Config.Bind("Textures", "ExportLoadedTextures", false, "Export loaded textures as PNG (may use much disk space). Restart after changing. Originals are never modified on disk.");
            if (!Config.Bind("General", "Enabled", true, "Enable Russian translation.").Value) return;
            try
            {
                assets = RussianAssets.Open(Root);
                Catalog.LoadTexts(assets.Catalogs());
                Fonts = new FontReplacements(assets, Path.Combine(Paths.BepInExRootPath, "cache", Id), Logger);
                Textures = new TextureReplacements(Root, Logger, exportTextures.Value);
                harmony = new Harmony(Id);
                PatchText();
                StartCoroutine(ScanResources());
                Logger.LogInfo("Russian translation loaded from rtlc.bundle: " + Catalog.Count + " entries.");
            }
            catch (Exception e) { Logger.LogError(e); harmony?.UnpatchSelf(); }
        }

        private void PatchText()
        {
            var prefix = new HarmonyMethod(typeof(Plugin), nameof(TextPrefix));
            harmony.Patch(AccessTools.PropertySetter(typeof(TMP_Text), "text"), new HarmonyMethod(typeof(Plugin), nameof(AttachPrefix)));
            harmony.Patch(AccessTools.PropertySetter(typeof(TMP_Text), "text"), postfix: new HarmonyMethod(typeof(Plugin), nameof(ClearSourceText)));
            harmony.Patch(AccessTools.PropertyGetter(typeof(TMP_Text), "text"), postfix: new HarmonyMethod(typeof(Plugin), nameof(TextGetterPostfix)));
            foreach (var method in typeof(TMP_Text).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "SetText" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string)))
                harmony.Patch(method, prefix, postfix: new HarmonyMethod(typeof(Plugin), nameof(SetTextPostfix)));
            harmony.Patch(AccessTools.Method(typeof(TMP_Text), "ParseInputText"), new HarmonyMethod(typeof(Plugin), nameof(AttachPrefix)));
            harmony.Patch(AccessTools.Method(typeof(TMP_Text), "SetText", new[] { typeof(System.Text.StringBuilder) }), new HarmonyMethod(typeof(Plugin), nameof(BuilderPrefix)), postfix: new HarmonyMethod(typeof(Plugin), nameof(BuilderPostfix)));
            harmony.Patch(AccessTools.Method(typeof(TextMeshProUGUI), "OnEnable"), postfix: new HarmonyMethod(typeof(Plugin), nameof(EnablePostfix)));
            harmony.Patch(AccessTools.Method(typeof(TextMeshPro), "OnEnable"), postfix: new HarmonyMethod(typeof(Plugin), nameof(EnablePostfix)));
            var game = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "FallenAces.CSharp") ?? Assembly.Load("FallenAces.CSharp");
            KeyboardLabels.Install(harmony, game);
            loadingScreenType = game.GetType("FallenAces.LoadingScreen");
            skipPromptType = game.GetType("FallenAces.SkipPrompt");
            hiddenIndicatorType = game.GetType("FallenAces.HUD.HudBoxNS.HiddenIndicator");
            loadingContinueField = AccessTools.Field(loadingScreenType, "_continuePrompt");
            loadingStatusField = AccessTools.Field(loadingScreenType, "_underText");
            skipTextField = AccessTools.Field(skipPromptType, "_text");
            hiddenTextField = AccessTools.Field(hiddenIndicatorType, "_textMesh");
            InstallGameplayLayout(game);
            menuButtonType = game.GetType("FallenAces.MenuButton");
            profileNameType = game.GetType("FallenAces.UI.ProfileNameDisplayer");
            profileSlotType = game.GetType("FallenAces.UI.ProfileSlotButton");
            profileDetailsField = AccessTools.Field(profileSlotType, "_detailsTextMesh");
            profileStateField = AccessTools.Field(profileSlotType, "_state");
            journalListEntryType = game.GetType("FallenAces.HUD.JournalListEntry");
            journalEntryNameField = AccessTools.Field(journalListEntryType, "_nameLabel");
            harmony.Patch(AccessTools.Method(journalListEntryType, "SetText"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(JournalListEntrySetTextPostfix)));
            // Translate subtitle segments before parsing, leaving timing, indices and facial expressions intact.
            var subtitle = game.GetType("FallenAces.HUD.SubtitleController");
            harmony.Patch(AccessTools.Method(subtitle, "AddSubtitle"), new HarmonyMethod(typeof(Plugin), nameof(SubtitlePrefix)));
            harmony.Patch(AccessTools.Method(subtitle.GetNestedType("Subtitle", BindingFlags.NonPublic), "LexText"), postfix: new HarmonyMethod(typeof(Plugin), nameof(SubtitleSegmentsPostfix)));
            var tutorial = game.GetType("FallenAces.HUD.TutorialPopupData");
            harmony.Patch(AccessTools.Method(tutorial, "GetText"), new HarmonyMethod(typeof(Plugin), nameof(TutorialPrefix)));
            var page = game.GetType("FallenAces.NoteData+Page");
            harmony.Patch(AccessTools.Method(page, "GetText"), new HarmonyMethod(typeof(Plugin), nameof(NotePrefix)));
        }

        private static bool IsEditable(TMP_Text text)
        {
            var input = text.GetComponentInParent<TMP_InputField>();
            if (input != null && input.textComponent == text) return true;
            if (Instance.profileNameType != null && text.GetComponentInParent(Instance.profileNameType)) return true;
            if (Instance.profileSlotType != null)
            {
                var slot = text.GetComponentInParent(Instance.profileSlotType);
                // This guard also runs inside TMP's text getter. Reading
                // text.text here would recursively enter the patched getter.
                if (slot && Instance.profileDetailsField.GetValue(slot) as TMP_Text == text &&
                    Instance.profileStateField.GetValue(slot)?.ToString() == "ExistingProfile") return true;
            }
            return false;
        }
        private static void TextPrefix(TMP_Text __instance, ref string __0, ref string __state)
        {
            AttachPrefix(__instance);
            __state = IsEditable(__instance) ? null : __0;
            if (__state != null) __0 = Instance.Catalog.Translate(__0);
        }
        private static void SetTextPostfix(TMP_Text __instance, string __state)
        {
            if (__state != null && !IsEditable(__instance)) Instance.StoreSourceText(__instance, __state);
        }
        private static void ClearSourceText(TMP_Text __instance) => Instance.directTextSources.Remove(__instance);
        private static void TextGetterPostfix(TMP_Text __instance, ref string __result)
        {
            if (!IsEditable(__instance) && Instance.directTextSources.TryGetValue(__instance, out var source)) __result = source.Value;
        }
        private void StoreSourceText(TMP_Text text, string source)
        {
            directTextSources.Remove(text);
            directTextSources.Add(text, new SourceText(source));
        }
        private void FitMenuLabel(TMP_Text text)
        {
            if (text.text == "PRESS ANY KEY")
            {
                var rect = text.rectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = false;
                text.fontSize = OriginalSize(text);
                return;
            }
            if (FitGameplayLabel(text)) return;
            if (FitLoadingAndHudLabel(text)) return;
            Transform settingsPanel = FindSettingsPanel(text.transform);
            if (settingsPanel != null) ApplySettingsPanelLayout(settingsPanel);
            Transform menuPanel = settingsPanel ?? FindMenuPanel(text.transform);
            if (menuPanel != null && menuPanel.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (Catalog.Translate(text.text).Trim() == "В меню" || Catalog.Translate(text.text).Trim() == "Выйти"))
            {
                text.enableAutoSizing = false;
                text.fontSize = 32f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                return;
            }
            bool customDifficulty = menuPanel != null && menuPanel.name.StartsWith("Menu_CustomDifficulty", StringComparison.Ordinal);
            if (customDifficulty && !expandedSettingsPanels.TryGetValue(menuPanel, out _))
            {
                expandedSettingsPanels.Add(menuPanel, new object());
                LayoutCustomDifficultyFooter(menuPanel);
            }
            bool title = (text.name == "TitleText" || text.name == "Title Text") &&
                text.transform.parent && text.transform.parent.name.StartsWith("Menu_", StringComparison.Ordinal);
            if (title)
            {
                FitMenuTitle(text);
                return;
            }
            if (customDifficulty && text.transform.parent != null &&
                (text.transform.parent.name == "MenuButton_Back" || text.transform.parent.name == "MenuButton_Confirm"))
            {
                text.fontSizeMax = text.fontSizeMin = 38f;
                text.enableAutoSizing = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.margin = Vector4.zero;
                return;
            }
            var inputField = text.GetComponentInParent<TMP_InputField>();
            if (inputField != null && inputField.placeholder == text)
            {
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.55f;
                text.enableAutoSizing = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                return;
            }
            if (menuPanel != null && menuPanel.name.StartsWith("Menu_Save And Load", StringComparison.Ordinal) && text.name == "Details")
            {
                // The stock save label uses a negative right margin to paint
                // outside its RectTransform. Give those lines a real area.
                ExpandNegativeMargins(text);
                var row = text.transform.parent as RectTransform;
                float size = OriginalSize(text);
                if (row != null) text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Min(size * 4f, row.rect.height - 6f));
                text.fontSizeMax = size;
                text.fontSizeMin = size * 0.8f;
                text.enableAutoSizing = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                return;
            }
            if (text.name == "Message")
            {
                Transform dialog = FindMenuPanel(text.transform);
                if (dialog != null && dialog.name.StartsWith("Menu_Old Save Prompt", StringComparison.Ordinal))
                {
                    text.rectTransform.anchoredPosition = new Vector2(text.rectTransform.anchoredPosition.x, -110f);
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 180f);
                    text.alignment = TextAlignmentOptions.TopLeft;
                    text.textWrappingMode = TextWrappingModes.Normal;
                    return;
                }
            }
            var dropdown = text.GetComponentInParent<TMP_Dropdown>();
            if (dropdown != null && (dropdown.captionText == text || dropdown.itemText == text))
            {
                Transform menu = FindMenuPanel(dropdown.transform);
                if (menu != null)
                {
                    FitMenuDropdown(dropdown, menu);
                    float size = OriginalSize(text);
                    text.fontSizeMax = size;
                    text.fontSizeMin = size * 0.85f;
                    text.enableAutoSizing = true;
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    return;
                }
            }
            if (settingsPanel != null)
            {
                float size = OriginalSize(text);
                if (settingsPanel.name.StartsWith("Menu_Keybinds", StringComparison.Ordinal))
                {
                    LayoutKeybindLabel(text, settingsPanel);
                    text.fontSize = size;
                    text.enableAutoSizing = false;
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    return;
                }
                var settingsButton = menuButtonType == null ? null : text.GetComponentInParent(menuButtonType);
                bool fitButton = settingsButton != null && settingsButton.GetType() == menuButtonType;
                text.fontSize = size;
                bool crosshair = settingsPanel.name.StartsWith("Menu_Crosshair", StringComparison.Ordinal);
                text.textWrappingMode = crosshair && !fitButton ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                text.enableAutoSizing = fitButton;
                if (fitButton)
                {
                    text.fontSizeMax = settingsButton.name == "MenuButton_Back" ? 60f : size;
                    text.fontSizeMin = text.fontSizeMax * 0.65f;
                }
                if (text.name == "Current Label" && text.GetComponentInParent<Slider>() != null)
                {
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Max(text.rectTransform.rect.height, size * 1.4f));
                    text.alignment = TextAlignmentOptions.Midline;
                }
                if (settingsPanel.name.StartsWith("Menu_KeyboardAndMouse", StringComparison.Ordinal) &&
                    text.GetComponentInParent<Toggle>() != null && !measuredMenuLabels.TryGetValue(text, out _))
                {
                    measuredMenuLabels.Add(text, new object());
                    ExpandNegativeMargins(text);
                    float extra = text.GetPreferredValues(Catalog.Translate(text.text)).x + 8f - text.rectTransform.rect.width;
                    if (extra > 0f) text.rectTransform.offsetMax += new Vector2(extra, 0f);
                }
                if (crosshair && text.name == "ColumnLabel")
                {
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                    text.margin = Vector4.zero;
                    text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                }
                if (settingsPanel.name.StartsWith("Menu_Video", StringComparison.Ordinal) && text.name == "Max FPS Label" &&
                    !measuredMenuLabels.TryGetValue(text, out _))
                {
                    measuredMenuLabels.Add(text, new object());
                    text.margin = Vector4.zero;
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Max(text.rectTransform.rect.width, text.GetPreferredValues(Catalog.Translate(text.text)).x + 8f));
                }
                return;
            }
            if (fittedLabels.TryGetValue(text, out _)) return;
            var parentButton = menuButtonType == null ? null : text.GetComponentInParent(menuButtonType);
            bool button = parentButton != null && parentButton.GetType() == menuButtonType;
            if (!button) return;
            fittedLabels.Add(text, new object());
            float originalSize = text.fontSize;
            text.fontSizeMax = originalSize;
            text.fontSizeMin = originalSize * 0.55f;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        private float OriginalSize(TMP_Text text)
        {
            if (!originalFontSizes.TryGetValue(text, out var state))
            {
                state = new OriginalFontSize(text.fontSize);
                originalFontSizes.Add(text, state);
            }
            return state.Value;
        }
        private static void ExpandNegativeMargins(TMP_Text text)
        {
            Vector4 margin = text.margin;
            if (margin.x >= 0f && margin.y >= 0f && margin.z >= 0f && margin.w >= 0f) return;
            RectTransform rect = text.rectTransform;
            rect.offsetMin += new Vector2(Mathf.Min(0f, margin.x), Mathf.Min(0f, margin.w));
            rect.offsetMax -= new Vector2(Mathf.Min(0f, margin.z), Mathf.Min(0f, margin.y));
            text.margin = new Vector4(Mathf.Max(0f, margin.x), Mathf.Max(0f, margin.y), Mathf.Max(0f, margin.z), Mathf.Max(0f, margin.w));
        }
        private bool FitLoadingAndHudLabel(TMP_Text text)
        {
            var loading = loadingScreenType == null ? null : text.GetComponentInParent(loadingScreenType);
            if (loading != null && (loadingContinueField.GetValue(loading) as TMP_Text) == text)
            {
                var rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.03f, 0f);
                rect.anchorMax = new Vector2(0.97f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 110f);
                rect.sizeDelta = new Vector2(0f, 80f);
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.8f;
                text.enableAutoSizing = true;
                return true;
            }
            if (loading != null && (loadingStatusField.GetValue(loading) as TMP_Text) == text)
            {
                text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360f);
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.MidlineRight;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.7f;
                text.enableAutoSizing = true;
                return true;
            }
            var skip = skipPromptType == null ? null : text.GetComponentInParent(skipPromptType);
            if (skip != null && (skipTextField.GetValue(skip) as TMP_Text) == text)
            {
                var root = skip.transform as RectTransform;
                var rect = text.rectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70f);
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = false;
                text.fontSize = OriginalSize(text);
                if (!fittedSkipSources.TryGetValue(text, out var sourceState))
                {
                    sourceState = new SourceText(null);
                    fittedSkipSources.Add(text, sourceState);
                }
                string source = text.text;
                if (sourceState.Value != source)
                {
                    // Mark before measuring: GetPreferredValues can re-enter
                    // ParseInputText and this layout guard.
                    sourceState.Value = source;
                    float width = Mathf.Clamp(text.GetPreferredValues(Catalog.Translate(source)).x + 16f, 300f, 1000f);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    if (root != null) root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + 90f);
                    var circle = skip.transform.Find("Circle") as RectTransform;
                    if (circle != null) circle.anchoredPosition = new Vector2(width + 20f, circle.anchoredPosition.y);
                }
                return true;
            }
            var hidden = hiddenIndicatorType == null ? null : text.GetComponentInParent(hiddenIndicatorType);
            if (hidden != null && (hiddenTextField.GetValue(hidden) as TMP_Text) == text)
            {
                text.margin = new Vector4(12f, 0f, 12f, 0f);
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.fontSizeMax = OriginalSize(text);
                text.fontSizeMin = text.fontSizeMax * 0.6f;
                text.enableAutoSizing = true;
                return true;
            }
            return false;
        }

        private static void LayoutCustomDifficultyFooter(Transform panel)
        {
            foreach (string name in new[] { "MenuButton_Back", "MenuButton_Confirm" })
            {
                var button = panel.Find(name) as RectTransform;
                if (button == null) continue;
                bool back = name == "MenuButton_Back";
                button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
                button.pivot = new Vector2(0.5f, 0.5f);
                button.localScale = Vector3.one;
                button.anchoredPosition = new Vector2(back ? -85f : 85f, -440f);
                button.sizeDelta = new Vector2(170f, 95f);
                var label = button.Find("Text") as RectTransform;
                if (label == null) continue;
                label.anchorMin = new Vector2(0.04f, 0.1f);
                label.anchorMax = new Vector2(0.96f, 0.9f);
                label.offsetMin = label.offsetMax = Vector2.zero;
            }
        }
        private void FitMenuTitle(TMP_Text text)
        {
            var panel = text.transform.parent as RectTransform;
            if (panel == null) return;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(0.5f, rect.anchorMax.y);
            rect.pivot = new Vector2(0.5f, rect.pivot.y);
            rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
            float drawingWidth = panel.rect.width;
            var image = panel.GetComponent<Image>();
            if (image != null && image.type == Image.Type.Simple && image.preserveAspect && image.sprite != null)
                drawingWidth = Mathf.Min(drawingWidth, panel.rect.height * image.sprite.rect.width / image.sprite.rect.height);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(panel.rect.width * 0.72f, drawingWidth * 0.84f));
            text.margin = Vector4.zero;
            // Center the visible glyph bounds. Font ascender metrics and the
            // original negative margins otherwise move replacement titles.
            text.alignment = TextAlignmentOptions.MidlineGeoAligned;
            text.fontSizeMax = OriginalSize(text);
            text.fontSizeMin = text.fontSizeMax * 0.55f;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            ApplyLoadingOutline(text);
        }

        private static void JournalListEntrySetTextPostfix(object __instance)
        {
            var self = Instance;
            var label = self.journalEntryNameField.GetValue(__instance) as TMP_Text;
            if (label == null) return;
            float size = self.OriginalSize(label);
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.MidlineGeoAligned;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontSizeMax = size;
            label.fontSizeMin = size * 0.55f;
            label.enableAutoSizing = true;
        }
        private static Transform FindMenuPanel(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
                if (current.name.StartsWith("Menu_", StringComparison.Ordinal) && current.GetComponent<Image>()) return current;
            return null;
        }
        private void FitMenuDropdown(TMP_Dropdown dropdown, Transform panel)
        {
            if (fittedDropdowns.TryGetValue(dropdown, out _) || dropdown.captionText == null) return;
            fittedDropdowns.Add(dropdown, new object());
            var rect = dropdown.transform as RectTransform;
            if (rect == null) return;
            TMP_Text caption = dropdown.captionText;
            Fonts?.Apply(caption);
            caption.enableAutoSizing = false;
            caption.fontSize = OriginalSize(caption);
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            float inset = Mathf.Max(48f, rect.rect.width - caption.rectTransform.rect.width);
            float preferred = 0f;
            foreach (var option in dropdown.options)
                preferred = Mathf.Max(preferred, caption.GetPreferredValues(Catalog.Translate(option.text)).x);
            float minimum = panel.name.StartsWith("Menu_Accessibility", StringComparison.Ordinal) ? 280f :
                panel.name.StartsWith("Menu_Gameplay", StringComparison.Ordinal) ? 340f :
                panel.name.StartsWith("Menu_Video", StringComparison.Ordinal) ? 380f :
                panel.name.StartsWith("Menu_Crosshair", StringComparison.Ordinal) ? 280f : 240f;
            float width = Mathf.Max(rect.rect.width, Mathf.Min(420f, Mathf.Max(minimum, preferred + inset + 16f)));
            bool compact = rect.rect.width < 240f;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var layout = dropdown.GetComponent<LayoutElement>();
            if (layout != null && layout.preferredWidth >= 0f) layout.preferredWidth = width;
            if (panel.name.StartsWith("Menu_CustomDifficulty", StringComparison.Ordinal))
            {
                var row = dropdown.transform.parent.GetComponent<HorizontalLayoutGroup>();
                var label = dropdown.transform.parent.Find("Label") as RectTransform;
                var rowRect = dropdown.transform.parent as RectTransform;
                if (row != null && label != null && rowRect != null)
                {
                    row.spacing = 20f;
                    row.childForceExpandWidth = false;
                    if (!compact)
                    {
                        width = Mathf.Min(width, 340f);
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                    }
                    label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowRect.rect.width - width - 20f);
                }
            }
            caption.margin = Vector4.zero;
            caption.alignment = TextAlignmentOptions.MidlineGeoAligned;
            caption.overflowMode = TextOverflowModes.Ellipsis;
        }
        private static Transform FindSettingsPanel(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                string name = current.name;
                if (name.StartsWith("Menu_Options", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Accessibility", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Audio", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Gameplay", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Crosshair", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_KeyboardAndMouse", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Keybinds", StringComparison.Ordinal) ||
                    name.StartsWith("Menu_Video", StringComparison.Ordinal)) return current;
            }
            return null;
        }
        private void ApplySettingsPanelLayout(Transform panel)
        {
            if (expandedSettingsPanels.TryGetValue(panel, out _)) return;
            if (panel.name.StartsWith("Menu_Options", StringComparison.Ordinal))
            {
                expandedSettingsPanels.Add(panel, new object());
                DistributeSettingsCategories(panel);
                return;
            }
            var rect = panel as RectTransform;
            var image = panel.GetComponent<Image>();
            if (rect == null || image == null || !FindLargeSettingsPanel()) return;
            expandedSettingsPanels.Add(panel, new object());

            // Reuse the game's own large Video panel, including its frame and
            // matching title/Back placement. Only the live menu instance changes.
            image.sprite = largeSettingsPanelImage.sprite;
            image.type = largeSettingsPanelImage.type;
            image.preserveAspect = largeSettingsPanelImage.preserveAspect;
            image.fillCenter = largeSettingsPanelImage.fillCenter;
            image.pixelsPerUnitMultiplier = largeSettingsPanelImage.pixelsPerUnitMultiplier;
            float verticalDelta = Mathf.Max(0f, largeSettingsPanelRect.sizeDelta.y - rect.sizeDelta.y) * 0.5f;
            rect.sizeDelta = largeSettingsPanelRect.sizeDelta;
            // The authored Video panel is intentionally offset upward in the
            // original menu. The enlarged frame is shared by every settings
            // page, so center its root instead of inheriting that offset.
            // Lift framed settings pages slightly so bottom anchored tooltips
            // have room below the Back button at common 16:9 resolutions.
            rect.anchoredPosition = new Vector2(0f, 35f);
            image.SetAllDirty();

            // Some compact pages pin their contents to the top edge. Keep the
            // rows below the enlarged title bar when the taller frame is reused.
            if (verticalDelta > 0f)
            {
                for (int i = 0; i < panel.childCount; i++)
                {
                    Transform child = panel.GetChild(i);
                    if (child.name == "TitleText" || child.name == "Title Text" || child.name == "MenuButton_Back") continue;
                    var childRect = child as RectTransform;
                    if (childRect != null && childRect.anchorMin.y > 0.99f && childRect.anchorMax.y > 0.99f)
                        childRect.anchoredPosition -= new Vector2(0f, verticalDelta);
                }
            }

            CopyPanelChildRect(panel, "TitleText");
            CopyPanelChildRect(panel, "MenuButton_Back");
            if (panel.name.StartsWith("Menu_Video", StringComparison.Ordinal))
            {
                var content = panel.Find("Scroll View/Viewport/Content/Panel") as RectTransform;
                if (content != null) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 840f);
            }
            if (panel.name.StartsWith("Menu_KeyboardAndMouse", StringComparison.Ordinal))
                LayoutKeyboardAndMouse(panel);
            if (panel.name.StartsWith("Menu_Keybinds", StringComparison.Ordinal))
                LayoutKeybindsPanel(panel);
            if (panel.name.StartsWith("Menu_Audio", StringComparison.Ordinal))
                LayoutAudioToggles(panel);
            // Gameplay was authored as a compact, left-aligned panel. Widen
            // its two columns and center the content inside the shared frame.
            if (panel.name.StartsWith("Menu_Gameplay", StringComparison.Ordinal))
            {
                MoveGameplayIndicators(panel);
                CenterGameplayColumns(panel, rect);
                LayoutGameplayBottomOptions(panel);
            }
            if (panel.name.StartsWith("Menu_Accessibility", StringComparison.Ordinal))
                CenterAccessibilityControls(panel);
            if (panel.name.StartsWith("Menu_Crosshair", StringComparison.Ordinal))
            {
                var content = panel.Find("All Panels") as RectTransform;
                var top = content?.Find("Top Panel") as RectTransform;
                if (content != null) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 840f);
                if (top != null)
                {
                    top.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 840f);
                    var group = top.GetComponent<HorizontalLayoutGroup>();
                    if (group != null) { group.spacing = 20f; group.childForceExpandWidth = false; }
                    foreach (string columnName in new[] { "Ranged Panel", "Melee Panel" })
                    {
                        var column = top.Find(columnName) as RectTransform;
                        if (column != null) column.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
                    }
                }
            }
            if (panel.name.StartsWith("Menu_Video", StringComparison.Ordinal))
                LayoutVideoControls(panel);
        }

        private static void DistributeSettingsCategories(Transform panel)
        {
            var content = panel.Find("Contents") as RectTransform;
            if (content == null) return;
            // Match the main menu's content area. Let the existing layout
            // distribute the five active rows across that entire area.
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = new Vector2(0f, -54.37f);
            content.sizeDelta = new Vector2(435.33f, 472.01f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) return;
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
            layout.padding = new RectOffset();
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        }

        private void LayoutKeyboardAndMouse(Transform panel)
        {
            var content = panel.Find("Main Panel");
            if (content == null) return;
            var paths = new[] { "Panel 2/Toggle Crouch", "Panel 2/Invert Mouse Y",
                "Panel 3/Toggle Sprint", "Panel 4/Toggle Controller" };
            for (int i = 0; i < paths.Length; i++)
            {
                var toggle = content.Find(paths[i]);
                if (toggle != null) AlignToggle(toggle, panel, i % 2 == 0 ? -330f : 65f,
                    35f - (i / 2) * 80f, 340f);
            }
            var sensitivity = content.Find("Panel 1/Mouse Sensitivity");
            if (sensitivity != null)
            {
                sensitivity.SetParent(panel, false);
                PlaceMenuRect(sensitivity as RectTransform, -320f, 140f, 640f, 65f);
            }
            var button = content.Find("Panel 3/Keybinds");
            if (button != null)
            {
                button.SetParent(panel, false);
                CenterMenuAction(button, -155f, 640f);
            }
            content.gameObject.SetActive(false);
        }

        private static void CenterMenuAction(Transform button, float y, float width)
        {
            var rect = button as RectTransform;
            if (rect == null) return;
            PlaceMenuRect(rect, 0f, y, width, 70f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                var label = text.rectTransform;
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = label.offsetMax = Vector2.zero;
                text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.MidlineGeoAligned;
            }
        }

        // Fix row geometry in panel coordinates. Text width must not change
        // slider lengths or the distance between a checkbox and its label.
        private static void PlaceMenuRect(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;
            var element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void StopRowLayout(Transform row)
        {
            foreach (var layout in row.GetComponents<LayoutGroup>()) layout.enabled = false;
            var fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        private void AlignToggle(Transform toggleTransform, Transform panel, float x, float y, float width)
        {
            var toggle = toggleTransform.GetComponent<Toggle>();
            if (toggle == null) return;
            toggleTransform.SetParent(panel, false);
            StopRowLayout(toggleTransform);
            PlaceMenuRect(toggleTransform as RectTransform, x, y, width, 60f);
            var label = toggleTransform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                StopRowLayout(label.transform);
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(62f, 0f);
                rect.sizeDelta = new Vector2((width - 62f) / Mathf.Max(0.01f, rect.localScale.x),
                    60f / Mathf.Max(0.01f, rect.localScale.y));
                label.margin = Vector4.zero;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontSize = OriginalSize(label);
                label.enableAutoSizing = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var background = toggle.targetGraphic != null ? toggle.targetGraphic.rectTransform : null;
            if (background != null && background.parent == toggleTransform)
            {
                background.anchorMin = background.anchorMax = new Vector2(0f, 0.5f);
                background.pivot = new Vector2(0f, 0.5f);
                background.anchoredPosition = Vector2.zero;
            }
        }

        private void LayoutAudioToggles(Transform panel)
        {
            var sliders = panel.Find("Panel 1");
            if (sliders != null)
            {
                // Preserve the actual Slider and its event bindings. Only move
                // its authored row, label and complete Bar subtree.
                int rowIndex = 0;
                foreach (Transform row in sliders.Cast<Transform>().ToArray())
                {
                    var label = row.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                    var bar = row.Find("Bar") as RectTransform;
                    if (label == null || bar == null) continue;
                    row.SetParent(panel, false);
                    StopRowLayout(row);
                    PlaceMenuRect(row as RectTransform, -380f, 210f - rowIndex++ * 66f, 760f, 60f);
                    var labelRect = label.rectTransform;
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
                    labelRect.pivot = new Vector2(0f, 0.5f);
                    labelRect.anchoredPosition = Vector2.zero;
                    labelRect.sizeDelta = new Vector2(360f / Mathf.Max(0.01f, labelRect.localScale.x), 60f);
                    label.margin = Vector4.zero;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.enableAutoSizing = false;
                    label.fontSize = OriginalSize(label);
                    bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
                    bar.pivot = new Vector2(0f, 0.5f);
                    bar.anchoredPosition = new Vector2(400f, 0f);
                    bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360f);
                }
                sliders.gameObject.SetActive(false);
            }
            var toggles = new[] {
                panel.Find("Panel 2/Row 1/Finisher Stingers"),
                panel.Find("Panel 2/Row 1/Alert Stinger"),
                panel.Find("Panel 2/Row 2/Pause Menu Music")
            };
            for (int i = 0; i < toggles.Length; i++)
                if (toggles[i] != null) AlignToggle(toggles[i], panel, -300f, -165f - i * 72f, 720f);
            var oldToggles = panel.Find("Panel 2");
            if (oldToggles != null) oldToggles.gameObject.SetActive(false);
        }

        private static void LayoutGameplayBottomOptions(Transform panel)
        {
            Transform content = panel.Find("Panel");
            Transform left = content != null ? content.Find("Left") : null;
            if (content == null || left == null) return;
            Transform pickupLabel = left.Find("Label");
            Transform pickupDropdown = left.Find("Pickup Flash");
            Transform videoLabel = left.Find("Label 2");
            Transform videoDropdown = left.Find("Disable Videos");
            if (pickupLabel == null || pickupDropdown == null || videoLabel == null || videoDropdown == null) return;

            Transform existing = panel.Find("Bottom Options Row");
            if (existing == null)
            {
                var rowObject = new GameObject("Bottom Options Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                var row = rowObject.GetComponent<RectTransform>();
                row.SetParent(panel, false);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
                row.pivot = new Vector2(0.5f, 0.5f);
                row.anchoredPosition = new Vector2(0f, -225f);
                row.sizeDelta = new Vector2(800f, 112f);
                var rowElement = rowObject.GetComponent<LayoutElement>();
                rowElement.ignoreLayout = true;
                var horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
                horizontal.spacing = 36f;
                horizontal.childAlignment = TextAnchor.MiddleCenter;
                horizontal.childControlWidth = true;
                horizontal.childControlHeight = true;
                horizontal.childForceExpandWidth = false;
                horizontal.childForceExpandHeight = false;

                AddGameplayOptionColumn(row, "Pickup Flash Option", pickupLabel, pickupDropdown);
                AddGameplayOptionColumn(row, "Disable Videos Option", videoLabel, videoDropdown);
            }

            // The Crosshair page is a distinct action; keep it centered below
            // the paired selectors and above the shared Back button.
            Transform crosshairButton = panel.Find("Panel/Right/MenuButton_CrosshairOptions");
            if (crosshairButton != null)
            {
                crosshairButton.SetParent(panel, false);
                var buttonRect = crosshairButton as RectTransform;
                if (buttonRect != null)
                {
                    buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                    buttonRect.pivot = new Vector2(0.5f, 0.5f);
                    buttonRect.anchoredPosition = new Vector2(0f, -325f);
                    buttonRect.sizeDelta = new Vector2(440f, 70f);
                }
                var layout = crosshairButton.GetComponent<LayoutElement>();
                if (layout == null) layout = crosshairButton.gameObject.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
            }
        }

        private static void AddGameplayOptionColumn(Transform row, string name, Transform label, Transform dropdown)
        {
            var columnObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var column = columnObject.GetComponent<RectTransform>();
            column.SetParent(row, false);
            column.sizeDelta = new Vector2(380f, 112f);
            var element = columnObject.GetComponent<LayoutElement>();
            element.preferredWidth = 380f;
            element.preferredHeight = 112f;
            var layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            label.SetParent(column, false);
            var labelRect = label as RectTransform;
            if (labelRect != null) labelRect.sizeDelta = new Vector2(380f, 38f);
            var tmp = label.GetComponent<TMP_Text>();
            if (tmp != null) { tmp.alignment = TextAlignmentOptions.MidlineGeoAligned; tmp.margin = Vector4.zero; }
            dropdown.SetParent(column, false);
            var dropdownRect = dropdown as RectTransform;
            if (dropdownRect != null)
            {
                dropdownRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 360f);
                dropdownRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 64f);
            }
        }

        private void LayoutVideoControls(Transform panel)
        {
            var content = panel.Find("Scroll View/Viewport/Content/Panel");
            var left = content?.Find("Left");
            var right = content?.Find("Right");
            if (left == null || right == null) return;
            const float width = 380f;
            foreach (var column in new[] { left, right })
            {
                var layout = column.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childControlWidth = false;
                    layout.childForceExpandWidth = false;
                }
                foreach (Transform child in column)
                {
                    if (child.GetComponent<TMP_Dropdown>() != null || child.name.EndsWith(" Label", StringComparison.Ordinal))
                        (child as RectTransform)?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                }
            }
            // Only the first two selector pairs need the stock zero-spacing
            // stack used on the right. Keep the later slider-value spacing.
            var selectors = new GameObject("Aligned Top Selectors", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var selectorsRect = selectors.GetComponent<RectTransform>();
            selectorsRect.SetParent(left, false);
            selectorsRect.SetAsFirstSibling();
            selectorsRect.sizeDelta = new Vector2(width, 208f);
            var selectorsLayout = selectors.GetComponent<VerticalLayoutGroup>();
            selectorsLayout.spacing = 0f;
            selectorsLayout.childAlignment = TextAnchor.UpperCenter;
            selectorsLayout.childControlWidth = selectorsLayout.childControlHeight = false;
            selectorsLayout.childForceExpandWidth = selectorsLayout.childForceExpandHeight = false;
            var selectorsSize = selectors.GetComponent<LayoutElement>();
            selectorsSize.preferredWidth = width;
            selectorsSize.preferredHeight = 208f;
            foreach (string name in new[] { "Resolution Label", "Resolution", "Anti Aliasing Label", "Anti Aliasing" })
            {
                var item = left.Find(name);
                if (item != null) item.SetParent(selectorsRect, false);
            }
            var leftLayout = left.GetComponent<VerticalLayoutGroup>();
            if (leftLayout != null) leftLayout.spacing = Mathf.Max(leftLayout.spacing, 10f);
            var fps = left.Find("Max FPS Panel");
            if (fps != null)
            {
                StopRowLayout(fps);
                (fps as RectTransform)?.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                var field = fps.Find("Max FPS") as RectTransform;
                var label = fps.Find("Max FPS Label") as RectTransform;
                if (field != null)
                {
                    field.anchorMin = field.anchorMax = new Vector2(1f, 0.5f);
                    field.pivot = new Vector2(1f, 0.5f);
                    field.anchoredPosition = Vector2.zero;
                }
                if (label != null)
                {
                    label.anchorMin = label.anchorMax = new Vector2(0f, 0.5f);
                    label.pivot = new Vector2(0f, 0.5f);
                    label.anchoredPosition = Vector2.zero;
                    label.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - 150f);
                }
            }
            foreach (var toggle in right.Cast<Transform>().Select(t => t.GetComponent<Toggle>()).Where(t => t != null).ToArray())
            {
                // Keep vertical layout ownership; normalize the internals only.
                AlignToggle(toggle.transform, right, 0f, 0f, width);
                toggle.GetComponent<LayoutElement>().ignoreLayout = false;
            }
        }

        private static void LayoutKeybindsPanel(Transform panel)
        {
            var scroll = panel.Find("Panel") as RectTransform;
            var background = panel.Find("Key Binds Background") as RectTransform;
            if (scroll == null) return;
            scroll.anchoredPosition = new Vector2(0f, -65f);
            scroll.sizeDelta = new Vector2(880f, 610f);
            if (background != null)
            {
                background.anchoredPosition = scroll.anchoredPosition;
                background.sizeDelta = new Vector2(890f, 630f);
            }
            var content = scroll.Find("Viewport/Content");
            var layout = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.padding.left = layout.padding.right = 16;
            }
        }

        private static void LayoutKeybindLabel(TMP_Text text, Transform panel)
        {
            var column = text.transform.parent;
            if (column == null || (column.name != "Bind Name" && column.name != "Bind Control")) return;
            var row = column.parent;
            if (row == null) return;
            StopRowLayout(row);
            var rowElement = row.GetComponent<LayoutElement>();
            if (rowElement == null) rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 65f;
            foreach (string name in new[] { "Bind Name", "Bind Control" })
            {
                var rect = row.Find(name) as RectTransform;
                if (rect == null) continue;
                PlaceMenuRect(rect, name == "Bind Name" ? 0f : 580f, 0f, name == "Bind Name" ? 560f : 240f, 65f);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            }
            text.margin = Vector4.zero;
            text.alignment = column.name == "Bind Name" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineGeoAligned;
        }

        private static void MoveGameplayIndicators(Transform panel)
        {
            Transform content = panel.Find("Panel");
            Transform left = content != null ? content.Find("Left") : null;
            Transform right = content != null ? content.Find("Right") : null;
            Transform indicators = right != null ? right.Find("Detection Meters") : null;
            if (left == null || right == null || indicators == null) return;
            indicators.SetParent(left, false);
            indicators.SetAsLastSibling();
            foreach (Transform column in new[] { left, right })
            {
                var layout = column.GetComponent<VerticalLayoutGroup>();
                if (layout == null) continue;
                layout.spacing = 16f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandHeight = false;
            }
        }

        private void CenterAccessibilityControls(Transform panel)
        {
            var content = panel.Find("Panel");
            var left = content?.Find("Left");
            var right = content?.Find("Right");
            if (content == null || left == null || right == null) return;
            var toggles = left.Cast<Transform>().Concat(right.Cast<Transform>())
                .Select(child => child.GetComponent<Toggle>()).Where(toggle => toggle != null).ToArray();
            for (int i = 0; i < toggles.Length; i++)
                AlignToggle(toggles[i].transform, panel, -380f, 165f - i * 78f, 760f);
            int rowIndex = 0;
            foreach (Transform row in left.Cast<Transform>().ToArray())
            {
                var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
                var label = row.Find("Label")?.GetComponent<TMP_Text>();
                if (dropdown == null || label == null) continue;
                row.SetParent(panel, false);
                StopRowLayout(row);
                PlaceMenuRect(row as RectTransform, -380f, -25f - rowIndex++ * 120f, 760f, 80f);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    465f / Mathf.Max(0.01f, labelRect.localScale.x));
                label.margin = Vector4.zero;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                var dropRect = dropdown.transform as RectTransform;
                dropRect.anchorMin = dropRect.anchorMax = new Vector2(0f, 0.5f);
                dropRect.pivot = new Vector2(0f, 0.5f);
                dropRect.anchoredPosition = new Vector2(480f, 0f);
                dropRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 280f);
            }
            content.gameObject.SetActive(false);
        }

        private void CenterGameplayColumns(Transform panel, RectTransform panelRect)
        {
            var content = panel.Find("Panel");
            if (content == null) return;
            foreach (string name in new[] { "Left", "Right" })
            {
                var column = content.Find(name);
                if (column == null) continue;
                int rowIndex = 0;
                foreach (var toggle in column.Cast<Transform>().Select(child => child.GetComponent<Toggle>())
                    .Where(toggle => toggle != null).ToArray())
                    AlignToggle(toggle.transform, panel, name == "Left" ? -380f : 30f,
                        205f - rowIndex++ * 70f, name == "Left" ? 370f : 390f);
            }
        }
        private bool FindLargeSettingsPanel()
        {
            if (largeSettingsPanelImage && largeSettingsPanelRect) return true;
            foreach (var image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (!image || !image.gameObject.name.StartsWith("Menu_Video", StringComparison.Ordinal) || !image.sprite) continue;
                var rect = image.transform as RectTransform;
                if (rect == null) continue;
                largeSettingsPanelImage = image;
                largeSettingsPanelRect = rect;
                return true;
            }
            return false;
        }
        private void CopyPanelChildRect(Transform targetPanel, string childName)
        {
            if (!largeSettingsPanelRect) return;
            Transform source = largeSettingsPanelRect.Find(childName);
            Transform target = targetPanel.Find(childName) ?? (childName == "TitleText" ? targetPanel.Find("Title Text") : null);
            var sourceRect = source as RectTransform;
            var targetRect = target as RectTransform;
            if (sourceRect == null || targetRect == null) return;
            targetRect.anchoredPosition = sourceRect.anchoredPosition;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.localScale = sourceRect.localScale;
            if (childName == "MenuButton_Back")
            {
                var sourceLabel = source.Find("Text") as RectTransform;
                var targetLabel = target.Find("Text") as RectTransform;
                if (sourceLabel == null || targetLabel == null) return;
                targetLabel.anchorMin = sourceLabel.anchorMin;
                targetLabel.anchorMax = sourceLabel.anchorMax;
                targetLabel.pivot = sourceLabel.pivot;
                targetLabel.anchoredPosition = sourceLabel.anchoredPosition;
                targetLabel.sizeDelta = sourceLabel.sizeDelta;
                targetLabel.localScale = sourceLabel.localScale;
                var label = targetLabel.GetComponent<TMP_Text>();
                if (label != null)
                {
                    label.margin = Vector4.zero;
                    label.alignment = TextAlignmentOptions.MidlineGeoAligned;
                }
            }
        }
        private sealed class RussianPreprocessor : ITextPreprocessor
        {
            private readonly ITextPreprocessor previous;
            private readonly TMP_Text owner;
            public RussianPreprocessor(TMP_Text owner, ITextPreprocessor previous) { this.owner = owner; this.previous = previous; }
            public string PreprocessText(string text)
            {
                string prepared = previous == null ? text : previous.PreprocessText(text);
                if (Instance == null || IsEditable(owner)) return prepared;
                var menu = FindMenuPanel(owner.transform);
                if (menu != null && menu.name.StartsWith("Menu_CustomDifficulty", StringComparison.Ordinal) &&
                    owner.transform.parent != null && owner.transform.parent.name == "MenuButton_Confirm")
                    return Instance.Catalog.Translate("Accept");
                return Instance.Catalog.Translate(prepared);
            }
        }
        private static void AttachPrefix(TMP_Text __instance)
        {
            // Keep .text in the original language: the game compares it with identifiers
            // in ControlPrompt.MatchesInfo and LoadingScreen. Translate only the render input.
            if (!IsEditable(__instance) && !(__instance.textPreprocessor is RussianPreprocessor))
            {
                __instance.textPreprocessor = new RussianPreprocessor(__instance, __instance.textPreprocessor);
                __instance.havePropertiesChanged = true;
            }
            Instance.Fonts?.Apply(__instance);
            Instance.FitMenuLabel(__instance);
        }
        private static void EnablePostfix(TMP_Text __instance) => AttachPrefix(__instance);
        private static void BuilderPrefix(TMP_Text __instance, ref System.Text.StringBuilder __0, ref string __state)
        {
            AttachPrefix(__instance);
            if (__0 != null && !IsEditable(__instance))
            {
                __state = __0.ToString();
                __0 = new System.Text.StringBuilder(Instance.Catalog.Translate(__state));
            }
        }
        private static void BuilderPostfix(TMP_Text __instance, string __state)
        {
            if (__state != null && !IsEditable(__instance)) Instance.StoreSourceText(__instance, __state);
        }
        private static void SubtitlePrefix(ref string characterName, ref string text)
        {
            characterName = Instance.Catalog.Translate(characterName);
            text = RepairSubtitleSyntax(text);
        }
        internal static string RepairSubtitleSyntax(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("lip? \"Cause", "lip? Cause");
            return System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^(\s*T\s*[-.\d]+[^\r\n]*)(\r?)$", m =>
            {
                string line = m.Groups[1].Value;
                return line.Count(c => c == '"') % 2 == 1 ? line + "\"" + m.Groups[2].Value : m.Value;
            });
        }
        private static void SubtitleSegmentsPostfix(object ___Segments)
        {
            // Let the game's lexer parse I/T/E, expression names and timing first.
            // Only replace Text in the already parsed segment structs.
            var segments = (IList)___Segments;
            for (int i = 0; i < segments.Count; i++)
            {
                object segment = segments[i];
                var field = AccessTools.Field(segment.GetType(), "Text");
                field.SetValue(segment, Instance.Catalog.Translate((string)field.GetValue(segment)));
                segments[i] = segment;
            }
        }
        private static void TutorialPrefix(ref string ____text) { ____text = Instance.Catalog.Translate(____text); }
        private static void NotePrefix(object __instance)
        {
            var field = AccessTools.Field(__instance.GetType(), "Text");
            field.SetValue(__instance, Instance.Catalog.Translate((string)field.GetValue(__instance)));
        }
        private IEnumerator ScanResources()
        {
            yield return null;
            while (true)
            {
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (text && text.gameObject.scene.IsValid()) EnablePostfix(text);
                Fonts.WriteReport();
                Textures.ProtectFontAtlases();
                int batch = 0;
                foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                {
                    Textures.Apply(texture);
                    if (exportTextures.Value || ++batch % 32 == 0) yield return null;
                }
                yield return new WaitForSecondsRealtime(3f);
            }
        }
        private void OnDestroy() { harmony?.UnpatchSelf(); Instance = null; }
    }
}
