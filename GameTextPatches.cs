using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NuclearOptionRussian
{
    // Перевод интерфейса самой игры (TMP и UGUI) без периодического просмотра всего текста:
    // Localization Patch каждые 0,3 с перебирал все текстовые компоненты, и игра подтормаживала.
    // Здесь текст переводится, только когда он появляется или меняется:
    //  * сеттер text и TMP_Text.SetText(string, ...) — текст, который игра задаёт из кода;
    //  * OnEnable — текст, заданный в префабе, переводится, когда объект становится видимым.
    // Поля ввода не трогаем: игрок должен видеть то, что напечатал.
    // Русскому тексту, который вылез за свой прямоугольник, включаем автоподбор размера —
    // на следующем кадре, когда панель уже разложена.
    internal static class GameTextPatches
    {
        const float MinScale = 0.55f;
        // Сильнее вылезает только текст, который и по-английски был «навылет» или прокручивается.
        const float MaxOverflow = 2.6f;
        const int FitsPerFrame = 30;

        static readonly Dictionary<int, bool> inputText = new Dictionary<int, bool>();
        static readonly List<(Component text, int frame)> fitQueue = new List<(Component, int)>();
        static readonly HashSet<int> queued = new HashSet<int>();
        // Последняя проверка размеров: длина текста и прямоугольник. Числа в "Позиция [x, y, z]" меняются
        // каждый кадр, но длина почти та же — пересчитывать незачем.
        static readonly Dictionary<int, (int length, Vector2 size)> fitted = new Dictionary<int, (int, Vector2)>();

        public static void Apply(Harmony harmony)
        {
            if (!Plugin.TranslateGame.Value) return;
            var self = typeof(GameTextPatches);
            // Priority.Last: если Localization Patch всё же стоит, он переведёт раньше, а мы поправим его перевод.
            HarmonyMethod Prefix(string name) => new HarmonyMethod(self, name) { priority = Priority.Last };

            harmony.Patch(AccessTools.PropertySetter(typeof(TMP_Text), "text"), prefix: Prefix(nameof(SetTmp)));
            harmony.Patch(AccessTools.PropertySetter(typeof(Text), "text"), prefix: Prefix(nameof(SetUgui)));
            foreach (var m in typeof(TMP_Text).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var ps = m.GetParameters();
                if (m.Name == "SetText" && m.DeclaringType == typeof(TMP_Text) && ps.Length > 0 && ps[0].ParameterType == typeof(string))
                    harmony.Patch(m, prefix: Prefix(nameof(SetTmpSource)));
            }

            var enabledTmp = new HarmonyMethod(self, nameof(EnabledTmp));
            harmony.Patch(AccessTools.Method(typeof(TextMeshProUGUI), "OnEnable"), postfix: enabledTmp);
            harmony.Patch(AccessTools.Method(typeof(TextMeshPro), "OnEnable"), postfix: enabledTmp);
            harmony.Patch(AccessTools.Method(typeof(Text), "OnEnable"), postfix: new HarmonyMethod(self, nameof(EnabledUgui)));
        }

        static void SetTmp(TMP_Text __instance, ref string value)
        {
            if (value == null || IsInput(__instance)) return;
            value = Translations.Game(value);
            QueueFit(__instance, value);
        }

        static void SetTmpSource(TMP_Text __instance, ref string sourceText)
        {
            if (sourceText == null || IsInput(__instance)) return;
            sourceText = Translations.Game(sourceText);
            QueueFit(__instance, sourceText);
        }

        static void SetUgui(Text __instance, ref string value)
        {
            if (value == null || IsInput(__instance)) return;
            value = Translations.Game(value);
            QueueFit(__instance, value);
        }

        static void EnabledTmp(TMP_Text __instance)
        {
            try
            {
                if (IsInput(__instance)) return;
                var s = __instance.text;
                if (string.IsNullOrEmpty(s)) return;
                var r = Translations.Game(s);
                if (r != s) __instance.text = r; // сеттер сам поставит проверку размеров
                else QueueFit(__instance, s);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("TMP text translation failed: " + e.Message);
            }
        }

        static void EnabledUgui(Text __instance)
        {
            try
            {
                if (IsInput(__instance)) return;
                var s = __instance.text;
                if (string.IsNullOrEmpty(s)) return;
                var r = Translations.Game(s);
                if (r != s) __instance.text = r;
                else QueueFit(__instance, s);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("UI text translation failed: " + e.Message);
            }
        }

        // Компонент, в котором поле ввода показывает набранный текст (подсказку-placeholder переводим).
        static bool IsInput(TMP_Text t)
        {
            int id = t.GetInstanceID();
            if (inputText.TryGetValue(id, out bool input)) return input;
            var field = t.GetComponentInParent<TMP_InputField>(true);
            input = field != null && field.textComponent == t;
            Remember(id, input);
            return input;
        }

        static bool IsInput(Text t)
        {
            int id = t.GetInstanceID();
            if (inputText.TryGetValue(id, out bool input)) return input;
            var field = t.GetComponentInParent<InputField>(true);
            input = field != null && field.textComponent == t;
            Remember(id, input);
            return input;
        }

        static void Remember(int id, bool input)
        {
            if (inputText.Count > 20000) inputText.Clear();
            inputText[id] = input;
        }

        static void QueueFit(Component text, string value)
        {
            if (!Plugin.FitText.Value || !Translations.HasCyrillic(value)) return;
            if (queued.Add(text.GetInstanceID())) fitQueue.Add((text, Time.frameCount));
        }

        // Каждый кадр: проверяем размеры текста, заданного в прошлых кадрах (раскладка к этому времени готова).
        public static void Tick()
        {
            if (fitQueue.Count == 0) return;
            int frame = Time.frameCount, i = 0;
            for (; i < fitQueue.Count && i < FitsPerFrame; i++)
            {
                var (text, queuedAt) = fitQueue[i];
                if (queuedAt >= frame) break; // очередь упорядочена по кадрам
                if (text == null) continue;   // объект уже уничтожен
                queued.Remove(text.GetInstanceID());
                try
                {
                    if (text is TMP_Text tmp) Fit(tmp);
                    else if (text is Text ugui) Fit(ugui);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Text fit failed: " + e.Message);
                }
            }
            fitQueue.RemoveRange(0, i);
            if (fitQueue.Count == 0) queued.Clear();
        }

        static bool Changed(Component t, string s, RectTransform rt)
        {
            if (fitted.Count > 20000) fitted.Clear();
            int id = t.GetInstanceID();
            var size = rt.rect.size;
            if (fitted.TryGetValue(id, out var last) && Math.Abs(last.length - s.Length) < 3 && last.size == size) return false;
            fitted[id] = (s.Length, size);
            return true;
        }

        static void Fit(TMP_Text t)
        {
            if (!t.isActiveAndEnabled || t.enableAutoSizing) return;
            var s = t.text;
            if (string.IsNullOrEmpty(s) || !Changed(t, s, t.rectTransform)) return;
            var rect = t.rectTransform.rect;
            var margin = t.margin;
            float w = rect.width - margin.x - margin.z;
            float h = rect.height - margin.y - margin.w;
            float size = t.fontSize;
            // Прямоугольник меньше одной строки — текст задуман «навылет» (подписи на карте, 3D-текст).
            if (size < 8f || w < size || h < size * 0.8f) return;

            // x — ширина в одну строку, y — высота с переносом по ширине w.
            var preferred = t.GetPreferredValues(w, 100000f);
            float ratio = t.enableWordWrapping ? preferred.y / h : preferred.x / w;
            if (ratio <= 1.02f || ratio > MaxOverflow) return;

            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(6f, size * MinScale);
            t.enableAutoSizing = true;
        }

        static void Fit(Text t)
        {
            if (!t.isActiveAndEnabled || t.resizeTextForBestFit || t.horizontalOverflow != HorizontalWrapMode.Wrap) return;
            var s = t.text;
            if (string.IsNullOrEmpty(s) || !Changed(t, s, t.rectTransform)) return;
            var rect = t.rectTransform.rect;
            int size = t.fontSize;
            if (size < 8 || rect.width < size || rect.height < size * 0.8f) return;

            float ratio = t.preferredHeight / rect.height;
            if (ratio <= 1.02f || ratio > MaxOverflow) return;

            t.resizeTextMaxSize = size;
            t.resizeTextMinSize = Mathf.Max(6, Mathf.RoundToInt(size * MinScale));
            t.resizeTextForBestFit = true;
        }
    }
}
