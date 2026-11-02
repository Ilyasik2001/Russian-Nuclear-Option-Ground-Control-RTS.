using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace NuclearOptionRussian
{
    // Переводит строковые литералы прямо в коде Ground Control: транспайлер меняет
    // операнд ldstr "English" на перевод. Так переводятся и шаблоны String.Format,
    // и куски String.Concat, и текст, который уходит в TMP.
    internal static class LiteralPatcher
    {
        // Классы со служебными строками: настройки, сеть, тесты, профилировщики, файлы.
        static readonly HashSet<string> skipClasses = new HashSet<string>
        {
            "CommanderSettings", "CommanderNetwork", "CommanderFixTest", "CommanderSoakTest",
            "CommanderSupplySoakTest", "CommanderNavalTest", "CommanderTakeoffTest", "CommanderUiShot",
            "CommanderFrameProfiler", "CommanderGameProfilerPatches", "CommanderServiceRegistryCheck",
            "CommanderServiceRegistry", "CommanderMissionInstaller", "CommanderPriceProfile",
            "CommanderSessionLog", "CommanderAirCrashRecorder", "CommanderTakeoffRecorderPatches",
            "CommanderLocalHeightMapBaker", "CommanderStrategicHeightMap", "CommanderScheduler",
        };

        // Если литерал уходит в такой вызов — это ключ/идентификатор, его не трогаем.
        static readonly HashSet<string> keyMethods = new HashSet<string>
        {
            "op_Equality", "op_Inequality", "Equals", "StartsWith", "EndsWith", "IndexOf", "LastIndexOf",
            "Contains", "Replace", "Split", "get_Item", "set_Item", "TryGetValue", "ContainsKey", "Remove",
            "get_Key", "Find", "set_name", "Parse", "TryParse", "LogOnce", "TraceEatenPress",
            "ContainsWeaponToken", "ContainsAll", "ContainsToken", "FindInCatalog", "OverrideInt",
            "OverrideBool", "Override", "HasTag", "IsHiddenTag", "SetTag", "SetValue", "GetValue",
            "FollowInMatch", "Expect", "Slot", "Stagger", "GetNestedType", "GetField", "GetMethod",
            "GetProperty", "TypeByName",
        };

        static readonly HashSet<string> keyTypes = new HashSet<string>
        {
            "AccessTools", "Traverse", "Harmony", "HarmonyMethod", "Path", "Directory", "File", "Type",
            "Shader", "Transform", "Enum", "Material", "MaterialPropertyBlock", "CommandBuffer", "Regex",
            "Input", "Player", "PlayerPrefs", "Animator", "Debug", "ManualLogSource", "ConfigFile",
            "ConfigDefinition", "CultureInfo", "CommanderNetwork", "CommanderSettings", "CommanderGameInput",
            "CommanderFrameProfiler", "CommanderScheduler", "CommanderMapSettings", "Resources", "GameObject",
        };

        static int replaced;

        public static void Apply(Harmony harmony, Assembly assembly)
        {
            if (Translations.Literals.Count == 0) return;

            var started = DateTime.Now;
            var transpiler = new HarmonyMethod(typeof(LiteralPatcher), nameof(Transpiler));
            int patched = 0, failed = 0, skippedGeneric = 0;

            foreach (var type in GetTypes(assembly))
            {
                if (skipClasses.Contains(OuterName(type))) continue;

                const BindingFlags all = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static;
                var methods = type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all));
                foreach (var method in methods)
                {
                    if (!HasTranslatableLiteral(method)) continue;
                    if (type.ContainsGenericParameters || method.ContainsGenericParameters)
                    {
                        skippedGeneric++;
                        continue;
                    }
                    try
                    {
                        harmony.Patch(method, transpiler: transpiler);
                        patched++;
                    }
                    catch (Exception e)
                    {
                        failed++;
                        Plugin.Log.LogWarning($"Could not patch {Scope(method)}: {e.Message}");
                    }
                }
            }

            Plugin.Log.LogInfo($"Ground Control: {patched} methods patched, {replaced} literals translated, " +
                               $"{failed} failed, {skippedGeneric} generic skipped ({(DateTime.Now - started).TotalSeconds:0.0}s)");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var list = instructions.ToList();
            var scope = Scope(original);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode != OpCodes.Ldstr || !(list[i].operand is string text)) continue;
                if (!Translations.TryLiteral(scope, text, out var ru)) continue;
                if (UsedAsKey(list, i)) continue;
                list[i].operand = ru;
                replaced++;
            }
            return list;
        }

        // Смотрим, куда уходит строка: первый вызов в пределах нескольких инструкций.
        static bool UsedAsKey(List<CodeInstruction> list, int index)
        {
            for (int k = index + 1; k < list.Count && k <= index + 8; k++)
            {
                var op = list[k].opcode;
                if (op == OpCodes.Stfld || op == OpCodes.Stsfld || op == OpCodes.Ret) return false;
                if (op != OpCodes.Call && op != OpCodes.Callvirt && op != OpCodes.Newobj) continue;
                if (!(list[k].operand is MethodBase target)) return false;
                // Вызов без параметров строку не потребляет (кроме "строка".Метод() сразу за ldstr).
                if (op != OpCodes.Newobj && target.GetParameters().Length == 0 && (target.IsStatic || k != index + 1)) continue;
                if (keyMethods.Contains(target.Name)) return true;
                var owner = target.DeclaringType;
                return owner != null && keyTypes.Contains(owner.IsGenericType ? owner.Name.Split('`')[0] : owner.Name);
            }
            return false;
        }

        // Быстрый поиск ldstr в сыром IL: 0x72 + токен строки (старший байт 0x70).
        static bool HasTranslatableLiteral(MethodBase method)
        {
            byte[] il;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch
            {
                return false;
            }
            if (il == null) return false;

            var scope = Scope(method);
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != 0x72 || il[i + 4] != 0x70) continue;
                int token = BitConverter.ToInt32(il, i + 1);
                string s;
                try
                {
                    s = method.Module.ResolveString(token);
                }
                catch
                {
                    continue;
                }
                if (Translations.TryLiteral(scope, s, out _)) return true;
            }
            return false;
        }

        static IEnumerable<Type> GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        // Формат "Внешний/Вложенный::Метод", как в выгрузке tools/DllStrings.
        internal static string Scope(MethodBase method) => TypePath(method.DeclaringType) + "::" + method.Name;

        static string TypePath(Type type) => type.DeclaringType == null ? type.Name : TypePath(type.DeclaringType) + "/" + type.Name;

        static string OuterName(Type type) => type.DeclaringType == null ? type.Name : OuterName(type.DeclaringType);
    }
}
