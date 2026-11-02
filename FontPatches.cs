using System;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionRussian
{
    // Ground Control рисует IMGUI шрифтом Barlow-Medium из игры, в нём нет кириллицы.
    // Подменяем такие шрифты на системный шрифт с кириллицей: первый установленный из списка.
    // Вариативные шрифты (Bahnschrift) не годятся — Unity берёт из них неверную ширину букв.
    internal static class FontPatches
    {
        static Regex pattern;
        static Font replacement;
        static bool resolved;

        public static void Apply(Harmony harmony)
        {
            var p = Plugin.FontReplacePattern.Value;
            if (string.IsNullOrEmpty(p) || string.IsNullOrEmpty(Plugin.FontNames.Value)) return;
            pattern = new Regex(p, RegexOptions.IgnoreCase);

            var prefix = new HarmonyMethod(typeof(FontPatches), nameof(SetFont));
            harmony.Patch(AccessTools.PropertySetter(typeof(GUIStyle), "font"), prefix: prefix);
            harmony.Patch(AccessTools.PropertySetter(typeof(GUISkin), "font"), prefix: prefix);
        }

        static void SetFont(ref Font value)
        {
            if (value == null || !pattern.IsMatch(value.name)) return;
            var r = Replacement();
            if (r != null) value = r;
        }

        static Font Replacement()
        {
            if (resolved) return replacement;
            resolved = true;
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                foreach (var raw in Plugin.FontNames.Value.Split(','))
                {
                    var name = raw.Trim();
                    if (name.Length == 0 || !installed.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) continue;
                    replacement = Font.CreateDynamicFontFromOSFont(name, 16);
                    Plugin.Log.LogInfo($"IMGUI font replaced with {name}");
                    return replacement;
                }
                Plugin.Log.LogWarning($"None of the fonts '{Plugin.FontNames.Value}' is installed; IMGUI keeps its font (Cyrillic falls back to the system font).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Font replacement failed: " + e.Message);
            }
            return replacement;
        }
    }
}
