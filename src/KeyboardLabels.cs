using System;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace RTLC.FallenAces
{
    /// <summary>US keyboard labels for the game's prompt formatter only.</summary>
    internal static class KeyboardLabels
    {
        private static PropertyInfo controlsProperty;
        private static PropertyInfo controlsCount;
        private static PropertyInfo controlsItem;
        private static Type keyControlType;
        private static PropertyInfo keyCodeProperty;

        internal static void Install(Harmony harmony, Assembly game)
        {
            // The game uses this one formatter for HUD prompts and rebinding
            // labels. KeyControl.displayName is supplied by the current OS
            // layout; keyCode is the stable physical US-QWERTY key identity.
            var input = Assembly.Load("Unity.InputSystem");
            var actionType = input.GetType("UnityEngine.InputSystem.InputAction", true);
            keyControlType = input.GetType("UnityEngine.InputSystem.Controls.KeyControl", true);
            controlsProperty = actionType.GetProperty("controls");
            controlsCount = controlsProperty.PropertyType.GetProperty("Count");
            controlsItem = controlsProperty.PropertyType.GetProperty("Item");
            keyCodeProperty = keyControlType.GetProperty("keyCode");
            var formatter = AccessTools.Method(game.GetType("FallenAces.SettingsManager", true),
                "GetOverriddenBindName", new[] { actionType, typeof(int) });
            harmony.Patch(formatter, prefix: new HarmonyMethod(typeof(KeyboardLabels), nameof(FormatPrefix)));
        }

        private static bool FormatPrefix(object __0, int __1, ref string __result)
        {
            if (__0 == null) return true;
            object controls = controlsProperty.GetValue(__0);
            int count = (int)controlsCount.GetValue(controls);
            // Match the original formatter's clamping and unbound-action path.
            // Mouse/gamepad controls retain all of the game's own overrides.
            if (count == 0) return true;
            int index = Math.Max(0, Math.Min(__1, count - 1));
            object control = controlsItem.GetValue(controls, new object[] { index });
            if (!keyControlType.IsInstanceOfType(control)) return true;
            string name = keyCodeProperty.GetValue(control).ToString();
            if (name == "None") return true;
            __result = EnglishKeyName(name);
            return false;
        }

        internal static string EnglishKeyName(string key)
        {
            if (key.Length == 1) return key.ToUpperInvariant();
            if (key.StartsWith("Digit", StringComparison.Ordinal) && key.Length == 6)
                return key.Substring(5);
            if (key.StartsWith("OEM", StringComparison.Ordinal) ||
                (key[0] == 'F' && key.Length <= 3 && char.IsDigit(key[1]))) return key;
            switch (key)
            {
                case "Backquote": return "`";
                case "Quote": return "'";
                case "Semicolon": return ";";
                case "Comma": return ",";
                case "Period": return ".";
                case "Slash": return "/";
                case "Backslash": return "\\";
                case "LeftBracket": return "[";
                case "RightBracket": return "]";
                case "Minus": return "-";
                case "Equals": return "=";
                case "NumpadDivide": return "Num /";
                case "NumpadMultiply": return "Num *";
                case "NumpadPlus": return "Num +";
                case "NumpadMinus": return "Num -";
                case "NumpadPeriod": return "Num .";
                case "NumpadEquals": return "Num =";
                case "NumpadEnter": return "Num Enter";
                case "LeftMeta": return "Left Win";
                case "RightMeta": return "Right Win";
                case "AltGr": return "Right Alt";
            }
            if (key.StartsWith("Numpad", StringComparison.Ordinal) && key.Length == 7)
                return "Num " + key.Substring(6);
            var label = new StringBuilder(key.Length + 4);
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]) && char.IsLower(key[i - 1])) label.Append(' ');
                label.Append(key[i]);
            }
            return label.ToString();
        }
    }
}
