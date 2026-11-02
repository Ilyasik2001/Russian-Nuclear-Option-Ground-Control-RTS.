using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionRussian
{
    // Перевод IMGUI по точному совпадению: всё, что рисуется через GUI.Label / Button / Window /
    // GUILayout..., проходит через GUIContent.Temp(string) или new GUIContent(string).
    // Поля ввода не трогаем: GUI.TextField возвращает content.text, и перевод подменил бы ввод игрока.
    internal static class ImguiPatches
    {
        static int textFieldDepth;

        internal static bool InTextField => textFieldDepth > 0;

        public static void Apply(Harmony harmony)
        {
            if (!Plugin.TranslateImgui.Value) return;

            var self = typeof(ImguiPatches);
            var content = typeof(GUIContent);
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var m in content.GetMethods(statics))
            {
                if (m.Name != "Temp") continue;
                var ps = m.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
                var prefix = ps.Length == 2 && ps[1].ParameterType == typeof(string) ? nameof(Text01) : nameof(Text0);
                harmony.Patch(m, prefix: new HarmonyMethod(self, prefix));
            }

            foreach (var c in content.GetConstructors())
            {
                var ps = c.GetParameters();
                if (ps.Length == 0 || ps[0].ParameterType != typeof(string)) continue;
                string prefix = nameof(Text0);
                if (ps.Length == 2 && ps[1].ParameterType == typeof(string)) prefix = nameof(Text01);
                if (ps.Length == 3 && ps[2].ParameterType == typeof(string)) prefix = nameof(Text02);
                harmony.Patch(c, prefix: new HarmonyMethod(self, prefix));
            }

            var enter = new HarmonyMethod(self, nameof(EnterTextField));
            var exit = new HarmonyMethod(self, nameof(ExitTextField));
            foreach (var type in new[] { typeof(GUI), typeof(GUILayout) })
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                if (m.Name == "TextField" || m.Name == "TextArea" || m.Name == "PasswordField")
                    harmony.Patch(m, prefix: enter, finalizer: exit);
        }

        static string Tr(string s) => s == null || textFieldDepth > 0 ? s : Translations.Display(s);

        static void Text0(ref string __0) => __0 = Tr(__0);

        static void Text01(ref string __0, ref string __1)
        {
            __0 = Tr(__0);
            __1 = Tr(__1);
        }

        static void Text02(ref string __0, ref string __2)
        {
            __0 = Tr(__0);
            __2 = Tr(__2);
        }

        static void EnterTextField() => textFieldDepth++;

        static Exception ExitTextField(Exception __exception)
        {
            textFieldDepth--;
            return __exception;
        }

        public static void SaveUntranslated() => Translations.SaveUntranslated();
    }
}
