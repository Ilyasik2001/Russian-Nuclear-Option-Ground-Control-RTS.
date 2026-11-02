using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace NuclearOptionRussian
{
    // GUID специально начинается на "com.gc...": BepInEx грузит плагины без зависимостей
    // по алфавиту GUID, и так мы стартуем раньше Ground Control ("com.groundcontrol.rts").
    // Это нужно, чтобы пропатчить его сборку до того, как выполнится её код.
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.gcru.russian";
        public const string Name = "Nuclear Option Russian";
        public const string Version = "1.0.0";

        internal const string GroundControlAssembly = "GroundControlRts";
        internal const string LocalizationPatchGuid = "com.noms.localizationpatch";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static string PluginDir;

        internal static ConfigEntry<bool> TranslateGroundControl;
        internal static ConfigEntry<bool> TranslateImgui;
        internal static ConfigEntry<bool> TranslateGame;
        internal static ConfigEntry<bool> FitText;
        internal static ConfigEntry<string> GameFont;
        internal static ConfigEntry<string> FontReplacePattern;
        internal static ConfigEntry<string> FontNames;
        internal static ConfigEntry<bool> DumpUntranslated;

        bool groundControlPatched;

        void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            Harmony = new Harmony(Guid);

            TranslateGroundControl = Config.Bind("General", "TranslateGroundControl", true,
                "Переводить интерфейс мода Ground Control (RTS).");
            TranslateImgui = Config.Bind("General", "TranslateImgui", true,
                "Переводить IMGUI-текст окон Ground Control (названия из игры берутся из base_game.json и names_*.json).");
            TranslateGame = Config.Bind("General", "TranslateGame", true,
                "Переводить интерфейс самой игры (TMP/UGUI): base_game.json и game_*.json. Localization Patch для этого не нужен.");
            FitText = Config.Bind("General", "FitText", true,
                "Уменьшать шрифт русского текста, который не помещается в свою кнопку или подпись (окна Ground Control и интерфейс игры).");
            GameFont = Config.Bind("Font", "GameFont", "Tektur-Reg.ttf",
                "Файл шрифта с кириллицей для текста игры (TextMeshPro): имя в папке fonts плагина или полный путь. Пусто — не добавлять.");
            FontReplacePattern = Config.Bind("Font", "ReplaceFonts", "Barlow",
                "Шрифты IMGUI без кириллицы, которые заменяются (регулярное выражение по имени). Пусто — не заменять.");
            FontNames = Config.Bind("Font", "FontNames", "Arial Narrow, Arial",
                "Системные шрифты с кириллицей через запятую; берётся первый установленный. Вариативные шрифты (Bahnschrift) не подходят.");
            DumpUntranslated = Config.Bind("Debug", "DumpUntranslated", false,
                "Для переводчиков: собирать непереведённый текст в untranslated.txt (окна Ground Control) и untranslated_game.txt (игра), " +
                "а решения о размере шрифта — в fit_debug.txt рядом с плагином.");

            Translations.Load(Path.Combine(PluginDir, "translations"));

            try
            {
                ImguiPatches.Apply(Harmony);
                FontPatches.Apply(Harmony);
                FitPatches.Apply(Harmony);
            }
            catch (Exception e)
            {
                Log.LogError("IMGUI patches failed: " + e);
            }

            try
            {
                GameTextPatches.Apply(Harmony);
            }
            catch (Exception e)
            {
                Log.LogError("Game text patches failed: " + e);
            }

            try
            {
                CyrillicFont.Apply(Harmony);
            }
            catch (Exception e)
            {
                Log.LogError("Cyrillic font setup failed: " + e);
            }

            if (TranslateGroundControl.Value)
            {
                var loaded = FindLoaded(GroundControlAssembly);
                if (loaded != null)
                {
                    Log.LogWarning("Ground Control was loaded before this plugin; static texts may stay in English.");
                    PatchGroundControl(loaded);
                }
                else
                {
                    AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
                }
            }
        }

        void Start()
        {
            // К Start() все плагины уже загружены — можно проверить соседей.
            if (Chainloader.PluginInfos.ContainsKey(LocalizationPatchGuid))
                Log.LogWarning("Localization Patch is also installed. This plugin translates the game by itself; Localization Patch only adds work every 0.3 s and can be disabled.");
            else if (!Translations.HasBase)
                Log.LogWarning("translations/base_game.json not found: the base game stays in English.");
            if (TranslateGroundControl.Value && !groundControlPatched)
                Log.LogInfo("Ground Control is not installed — nothing to patch.");
        }

        void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GetName().Name != GroundControlAssembly) return;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            PatchGroundControl(args.LoadedAssembly);
        }

        void PatchGroundControl(Assembly assembly)
        {
            groundControlPatched = true;
            try
            {
                LiteralPatcher.Apply(Harmony, assembly);
            }
            catch (Exception e)
            {
                Log.LogError("Ground Control patching failed: " + e);
            }
        }

        float nextSave;

        void Update()
        {
            GameTextPatches.Tick();

            // Сохраняем непереведённое периодически: игра может закрыться без OnApplicationQuit.
            if (UnityEngine.Time.unscaledTime < nextSave) return;
            nextSave = UnityEngine.Time.unscaledTime + 30f;
            ImguiPatches.SaveUntranslated();
            FitPatches.SaveDebug();
        }

        void OnApplicationQuit()
        {
            ImguiPatches.SaveUntranslated();
            FitPatches.SaveDebug();
        }

        static Assembly FindLoaded(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name == name) return a;
            return null;
        }
    }
}
