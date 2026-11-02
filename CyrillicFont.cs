using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace NuclearOptionRussian
{
    // Шрифты TextMeshPro в игре без кириллицы. Добавляем запасной (fallback) шрифт из файла .ttf.
    // TMP 3.0 умеет строить динамический шрифт только из объекта Font, а Font из файла в Unity не создать.
    // Поэтому берём любой системный Font как «заглушку», а FontEngine.LoadFontFace(заглушка) подменяем
    // загрузкой нашего файла: TMP думает, что работает с заглушкой, а глифы рисуются из файла.
    internal static class CyrillicFont
    {
        const int SamplingSize = 36;

        static Font proxy;
        static string path;
        static TMP_FontAsset asset;

        public static void Apply(Harmony harmony)
        {
            var configured = Plugin.GameFont.Value;
            if (string.IsNullOrEmpty(configured)) return;

            path = FindFile(configured);
            if (path == null)
            {
                Plugin.Log.LogWarning("Cyrillic font file not found: " + configured);
                return;
            }

            foreach (var m in typeof(FontEngine).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var ps = m.GetParameters();
                if (m.Name == "LoadFontFace" && ps.Length > 0 && ps[0].ParameterType == typeof(Font))
                    harmony.Patch(m, prefix: new HarmonyMethod(typeof(CyrillicFont), nameof(LoadFontFace)));
            }

            proxy = Font.CreateDynamicFontFromOSFont("Arial", SamplingSize);
            proxy.name = "Russian fallback (" + Path.GetFileNameWithoutExtension(path) + ")";
            asset = TMP_FontAsset.CreateFontAsset(proxy, SamplingSize, 4, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                Plugin.Log.LogWarning("Could not create the Cyrillic TMP font from " + path);
                return;
            }
            asset.name = proxy.name;

            var global = TMP_Settings.fallbackFontAssets;
            if (global != null && !global.Contains(asset)) global.Add(asset);
            AttachToFonts();
            SceneManager.sceneLoaded += (scene, mode) => AttachToFonts();
            Plugin.Log.LogInfo("Cyrillic TMP font ready: " + Path.GetFileName(path));
        }

        static string FindFile(string configured)
        {
            var candidates = new List<string>();
            if (Path.IsPathRooted(configured)) candidates.Add(configured);
            else
            {
                candidates.Add(Path.Combine(Path.Combine(Plugin.PluginDir, "fonts"), configured));
                candidates.Add(Path.Combine(Plugin.PluginDir, configured));
            }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"));
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        static bool LoadFontFace(Font font, object[] __args, ref FontEngineError __result)
        {
            if (proxy == null || !ReferenceEquals(font, proxy)) return true;
            int size = __args.Length > 1 ? (int)__args[1] : SamplingSize;
            int face = __args.Length > 2 ? (int)__args[2] : 0;
            __result = FontEngine.LoadFontFace(path, size, face);
            return false;
        }

        // Глобальный список запасных шрифтов TMP смотрит не всегда — добавляем наш и каждому шрифту игры.
        // Шрифтов всего несколько, поэтому достаточно делать это при загрузке сцены.
        static void AttachToFonts()
        {
            try
            {
                foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                {
                    if (font == asset) continue;
                    var table = font.fallbackFontAssetTable;
                    if (table == null) font.fallbackFontAssetTable = new List<TMP_FontAsset> { asset };
                    else if (!table.Contains(asset)) table.Add(asset);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not attach the Cyrillic font: " + e.Message);
            }
        }
    }
}
