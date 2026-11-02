using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionRussian
{
    // Русский текст длиннее английского, а кнопки и подписи Ground Control рассчитаны на английский:
    // "НАЗЕМНЫЕ" рвётся на две строки, "РАЗМЕР ЗАКАЗА" обрезается. Вся отрисовка IMGUI проходит через
    // закрытый GUIStyle.Draw(Rect, GUIContent, int, bool, bool, bool, bool) — перед ним русская строка,
    // которая не помещается в свой прямоугольник, получает шрифт помельче, после — размер возвращается.
    internal static class FitPatches
    {
        const float MinScale = 0.6f;
        // Что вылезает сильнее, то, скорее всего, вылезало и по-английски (подпись в нулевом прямоугольнике).
        // Текст с переносом при уменьшении шрифта выигрывает и по ширине, и по высоте, поэтому ему можно больше.
        const float MaxWidthOverflow = 2.2f;
        const float MaxHeightOverflow = 3f;

        static readonly Dictionary<Key, int> sizes = new Dictionary<Key, int>();
        static readonly GUIContent probe = new GUIContent();
        // Отладка (вместе с DumpUntranslated): почему строка уменьшена или нет — fit_debug.txt рядом с плагином.
        static readonly List<string> debug = new List<string>();
        static bool debugDirty, firstShrinkLogged;

        public static void Apply(Harmony harmony)
        {
            if (!Plugin.FitText.Value) return;
            var draw = AccessTools.Method(typeof(GUIStyle), "Draw", new[]
            {
                typeof(Rect), typeof(GUIContent), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
            });
            if (draw == null)
            {
                Plugin.Log.LogWarning("GUIStyle.Draw not found: IMGUI text will not be shrunk to fit.");
                return;
            }
            harmony.Patch(draw,
                prefix: new HarmonyMethod(typeof(FitPatches), nameof(Shrink)),
                finalizer: new HarmonyMethod(typeof(FitPatches), nameof(Restore)));
            Plugin.Log.LogInfo("IMGUI text fitting enabled");
        }

        public static void SaveDebug()
        {
            if (!debugDirty) return;
            debugDirty = false;
            try
            {
                System.IO.File.WriteAllLines(System.IO.Path.Combine(Plugin.PluginDir, "fit_debug.txt"), debug.ToArray());
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save fit_debug.txt: " + e.Message);
            }
        }

        static void Shrink(GUIStyle __instance, Rect position, GUIContent content, out int __state)
        {
            __state = -1;
            var text = content?.text;
            if (string.IsNullOrEmpty(text) || ImguiPatches.InTextField || !Translations.HasCyrillic(text)) return;

            var font = __instance.font;
            var key = new Key(text, position, __instance.fontSize, font, __instance.wordWrap);
            if (!sizes.TryGetValue(key, out int size))
            {
                if (sizes.Count > 4000) sizes.Clear();
                size = FitSize(__instance, position, content, out string why);
                sizes[key] = size;
                // Однострочное, которое и так помещается, неинтересно — пишем вылезающее и многострочное.
                if (Plugin.DumpUntranslated.Value && debug.Count < 600 && (why != "fits" || text.IndexOf('\n') >= 0))
                {
                    debug.Add($"{why}\t{__instance.fontSize}->{size}\tw={position.width:0} h={position.height:0} wrap={__instance.wordWrap}\t{text.Replace("\n", "\\n")}");
                    debugDirty = true;
                }
                if (size > 0 && !firstShrinkLogged)
                {
                    firstShrinkLogged = true;
                    Plugin.Log.LogInfo($"IMGUI text fitting works: \"{text}\" {__instance.fontSize} -> {size}");
                }
            }
            if (size <= 0) return;
            __state = __instance.fontSize;
            __instance.fontSize = size;
        }

        static void Restore(GUIStyle __instance, int __state)
        {
            if (__state >= 0) __instance.fontSize = __state;
        }

        // 0 — оставить как есть.
        static int FitSize(GUIStyle style, Rect r, GUIContent content, out string why)
        {
            why = "small rect";
            if (r.width < 4 || r.height < 4) return 0;
            if (!Overflows(style, r, content, out float ratio, out bool byHeight))
            {
                why = "fits";
                return 0;
            }
            why = (byHeight ? "height x" : "width x") + ratio.ToString("0.00");
            if (ratio > (byHeight ? MaxHeightOverflow : MaxWidthOverflow)) return 0;

            int original = style.fontSize;
            int full = original > 0 ? original : DefaultSize(style);
            why += " full=" + full;
            if (full <= 0) return 0;
            int min = Mathf.Max(6, Mathf.CeilToInt(full * MinScale));
            int result = min;
            try
            {
                for (int s = full - 1; s >= min; s--)
                {
                    style.fontSize = s;
                    if (!Overflows(style, r, content, out _, out _))
                    {
                        result = s;
                        break;
                    }
                }
            }
            finally
            {
                style.fontSize = original;
            }
            return result;
        }

        // Размер 0 — «как у шрифта». У системного шрифта, созданного из ОС, он тоже бывает 0,
        // тогда выводим размер из высоты строки (у Arial и похожих она около 1,15 кегля).
        static int DefaultSize(GUIStyle style)
        {
            var font = style.font != null ? style.font : GUI.skin.font;
            if (font != null && font.dynamic && font.fontSize > 0) return font.fontSize;
            return Mathf.Clamp(Mathf.RoundToInt(style.lineHeight / 1.15f), 0, 48);
        }

        // Окна Ground Control масштабируются через GUI.matrix (настройка WindowZoom), и тогда текст рисуется
        // шрифтом округлённого размера и выходит на несколько процентов шире, чем считает CalcSize.
        // Поэтому строка «впритык» уже переносится — считаем, что ей нужно на 4 % больше места.
        const float Slack = 1.04f;

        static bool Overflows(GUIStyle style, Rect r, GUIContent content, out float ratio, out bool byHeight)
        {
            byHeight = false;
            float width = style.CalcSize(content).x * Slack + 2; // в одну строку, без переноса
            ratio = width / r.width;
            if (width <= r.width) return false;
            if (!style.wordWrap) return true;

            // Перенос задуман: плохо, только если строки не помещаются по высоте или слово рвётся посередине.
            float height = style.CalcHeight(content, (r.width - 2) / Slack);
            if (height > r.height + 1)
            {
                ratio = height / r.height;
                byHeight = true;
                return true;
            }
            float word = LongestWord(style, content.text) * Slack + 2;
            ratio = word / r.width;
            return word > r.width;
        }

        static float LongestWord(GUIStyle style, string text)
        {
            float max = 0;
            foreach (var word in text.Split(' ', '\n'))
            {
                if (word.Length == 0) continue;
                probe.text = word;
                max = Mathf.Max(max, style.CalcSize(probe).x);
            }
            return max;
        }

        // Стили Ground Control часто создаются заново каждый кадр, поэтому ключ — не сам стиль, а то, что влияет на размер.
        readonly struct Key : IEquatable<Key>
        {
            readonly string text;
            readonly int width, height, fontSize, font;
            readonly bool wrap;

            public Key(string text, Rect r, int fontSize, Font font, bool wrap)
            {
                this.text = text;
                width = Mathf.RoundToInt(r.width);
                height = Mathf.RoundToInt(r.height);
                this.fontSize = fontSize;
                this.font = font != null ? font.GetInstanceID() : 0;
                this.wrap = wrap;
            }

            public bool Equals(Key o) =>
                width == o.width && height == o.height && fontSize == o.fontSize && font == o.font && wrap == o.wrap &&
                string.Equals(text, o.text);

            public override bool Equals(object obj) => obj is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = text.GetHashCode();
                    h = h * 31 + width;
                    h = h * 31 + height;
                    h = h * 31 + fontSize;
                    h = h * 31 + font;
                    return wrap ? ~h : h;
                }
            }
        }
    }
}
