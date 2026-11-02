using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Newtonsoft.Json;

namespace NuclearOptionRussian
{
    // Словари перевода.
    //  * Literals — строковые литералы Ground Control: "English" -> "Русский".
    //    Ключ вида "Тип::Метод|English" действует только внутри этого метода.
    //  * Display — точные совпадения для IMGUI: наши литералы + базовый перевод игры.
    //    Файлы display_*.json попадают только сюда: это строки, которые мод сравнивает в коде,
    //    поэтому их можно переводить лишь при выводе на экран.
    //  * Base — base_*.json: базовый перевод интерфейса самой игры.
    //  * Game — game_*.json: текст игры, которого нет в базовом переводе,
    //    и замены его слишком длинных переводов (ключ — русский текст).
    // Ключ с {0}, {1}... в display_*.json и game_*.json — шаблон: "Air: {0}" -> "Авиация: {0}".
    // Подставленная часть тоже переводится, если она есть в словаре.
    internal static class Translations
    {
        public static readonly Dictionary<string, string> Literals = new Dictionary<string, string>();
        static readonly Dictionary<string, string> display = new Dictionary<string, string>();
        static readonly Dictionary<string, string> game = new Dictionary<string, string>();
        static readonly Dictionary<string, string> baseGame = new Dictionary<string, string>();
        // "MISSION SETTINGS", когда в словаре есть только "Mission Settings".
        static readonly Dictionary<string, string> upper = new Dictionary<string, string>();
        static readonly Dictionary<string, string> gameCache = new Dictionary<string, string>();
        static readonly List<Pattern> displayPatterns = new List<Pattern>();
        static readonly List<Pattern> gamePatterns = new List<Pattern>();
        static readonly Dictionary<string, string> names = new Dictionary<string, string>();
        static Regex nameRegex;

        // Длинные описания (миссии) сравниваем без учёта переносов: игра разделяет абзацы то \n, то \v.
        const int LooseMinLength = 60;
        static readonly Dictionary<string, string> gameLoose = new Dictionary<string, string>();
        static readonly Regex spaces = new Regex(@"\s+");

        // Номер в списке миссий: "15. Breaking the Atoll".
        static readonly Regex numbered = new Regex(@"^(\d{1,3}\.\s+)(.+)$", RegexOptions.Singleline);

        sealed class Pattern
        {
            public Regex Regex;
            public string Value;
            public int Length;
        }

        // Иконки и теги по краям: "<size=85%><font=...></font></size> Refresh" -> ядро "Refresh".
        static readonly Regex edges = new Regex(@"^((?:<[^>]*>|\\u[0-9a-fA-F]{4}|[-]|\s)*)(.*?)((?:<[^>]*>|\s|:)*)$",
            RegexOptions.Singleline);

        // Кэш результатов Display(): IMGUI спрашивает одни и те же строки каждый кадр.
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly HashSet<string> untranslated = new HashSet<string>();
        static readonly HashSet<string> untranslatedGame = new HashSet<string>();
        static readonly Regex latin = new Regex("[A-Za-z]{2,}");
        static readonly Regex word = new Regex("[A-Za-z]{3,}");
        static readonly Regex digits = new Regex("[0-9]+");
        static bool untranslatedDirty;

        public static void Load(string folder)
        {
            if (!Directory.Exists(folder))
            {
                Plugin.Log.LogWarning("Translation folder not found: " + folder);
                return;
            }

            var files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                int n = 0;
                var name = Path.GetFileName(file);
                var kind = KindOf(name);
                foreach (var kv in Read(file))
                {
                    Add(kind, kv.Key, kv.Value);
                    n++;
                }
                Plugin.Log.LogInfo($"Loaded {n} {kind.ToString().ToLowerInvariant()} entries from {name}");
            }

            // Сначала длинные шаблоны: "{0}\nRank: {1}\nOwned: {2}\nNot available here" раньше, чем без последней строки.
            displayPatterns.Sort((a, b) => b.Length.CompareTo(a.Length));
            gamePatterns.Sort((a, b) => b.Length.CompareTo(a.Length));
            BuildNames();
            AfterBaseLoaded();
        }

        public static bool HasBase => baseGame.Count > 0;

        //  * Base — base_*.json: базовый перевод игры (и экран IMGUI — для названий из игры);
        //  * Literal — gc_*.json: литералы Ground Control (и экран, если ключ не привязан к методу);
        //  * Display — display_*.json: только экран IMGUI;
        //  * Game — game_*.json: только текст игры;
        //  * Shared — shared_*.json: и экран IMGUI, и текст игры (описания техники видны в обоих);
        //  * Name — names_*.json: названия зданий, баз, техники и пилонов. Кроме точного совпадения,
        //    подставляются внутрь IMGUI-строк Ground Control ("Мешает: Vehicle Depot.") и в ВЕРХНЕМ регистре.
        enum Kind { Base, Literal, Display, Game, Shared, Name }

        static Kind KindOf(string file)
        {
            if (file.StartsWith("base", StringComparison.OrdinalIgnoreCase)) return Kind.Base;
            if (file.StartsWith("display", StringComparison.OrdinalIgnoreCase)) return Kind.Display;
            if (file.StartsWith("game", StringComparison.OrdinalIgnoreCase)) return Kind.Game;
            if (file.StartsWith("shared", StringComparison.OrdinalIgnoreCase)) return Kind.Shared;
            if (file.StartsWith("names", StringComparison.OrdinalIgnoreCase)) return Kind.Name;
            return Kind.Literal;
        }

        static void Add(Kind kind, string key, string value)
        {
            switch (kind)
            {
                case Kind.Base:
                    AddBase(key, value);
                    return;
                case Kind.Literal:
                    Literals[key] = value;
                    // Ключ "Тип::Метод|текст" действует только в своём методе — на экранный словарь не влияет.
                    int bar = key.IndexOf('|');
                    if (bar <= 0 || key.IndexOf("::", 0, bar, StringComparison.Ordinal) < 0) display[key] = value;
                    return;
                case Kind.Name:
                    AddName(key, value);
                    AddName(key.ToUpperInvariant(), value.ToUpperInvariant());
                    return;
            }

            bool pattern = IsPattern(key);
            if (kind != Kind.Game)
            {
                if (pattern) displayPatterns.Add(Compile(key, value));
                else display[key] = value;
            }
            if (kind != Kind.Display)
            {
                if (pattern)
                {
                    gamePatterns.Add(Compile(key, value));
                }
                else
                {
                    game[key] = value;
                    if (key.Length >= LooseMinLength) gameLoose[Loose(key)] = value;
                }
            }
        }

        static void AddName(string key, string value)
        {
            names[key] = value;
            display[key] = value;
            game[key] = value;
        }

        // Одно регулярное выражение на все названия, длинные раньше коротких:
        // "Aircraft Revetment @ K92 Highway Strip" — это два названия, а не одно неизвестное.
        static void BuildNames()
        {
            if (names.Count == 0) return;
            var keys = new List<string>(names.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            var sb = new StringBuilder(@"(?<![A-Za-z0-9])(?:");
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(Regex.Escape(keys[i]));
            }
            sb.Append(@")(?![A-Za-z0-9])");
            nameRegex = new Regex(sb.ToString(), RegexOptions.CultureInvariant);
        }

        static string ReplaceNames(string text) =>
            nameRegex == null || !latin.IsMatch(text) ? text : nameRegex.Replace(text, m => names[m.Value]);

        static bool IsPattern(string key) => key.IndexOf("{0}", StringComparison.Ordinal) >= 0;

        static string Loose(string text) => spaces.Replace(text, " ").Trim();

        // Пробел в шаблоне совпадает с любым пробельным промежутком (в том числе с переносом строки),
        // два пробела и больше — с промежутком хотя бы из двух символов: так "{0}   {1} / {2}"
        // отделяет название из нескольких слов от чисел.
        static Pattern Compile(string key, string value)
        {
            var sb = new StringBuilder("^");
            for (int i = 0; i < key.Length;)
            {
                char c = key[i];
                if (char.IsWhiteSpace(c))
                {
                    int j = i;
                    while (j < key.Length && char.IsWhiteSpace(key[j])) j++;
                    sb.Append(j - i > 1 ? @"\s{2,}" : @"\s+");
                    i = j;
                }
                else if (c == '{' && i + 2 < key.Length && char.IsDigit(key[i + 1]) && key[i + 2] == '}')
                {
                    // Подстановка не переходит на другую строку: многострочный текст переводится построчно.
                    sb.Append("(?<g").Append(key[i + 1]).Append(@">[^\n]+?)");
                    i += 3;
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                    i++;
                }
            }
            sb.Append('$');
            return new Pattern
            {
                Regex = new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant),
                Value = value,
                Length = key.Length
            };
        }

        static bool TryPattern(List<Pattern> patterns, string text, Func<string, string> part, out string result)
        {
            foreach (var p in patterns)
            {
                var m = p.Regex.Match(text);
                if (!m.Success) continue;
                var sb = new StringBuilder(p.Value);
                for (int g = 0; g < 10; g++)
                {
                    var group = m.Groups["g" + g];
                    if (group.Success) sb.Replace("{" + g + "}", part(group.Value));
                }
                // Шаблон-«пустышка» вроде "{0}: {1} / {2}" нужен только ради перевода частей.
                var s = sb.ToString();
                if (s == text) continue;
                result = s;
                return true;
            }
            result = null;
            return false;
        }

        static string DisplayPart(string s) => display.TryGetValue(s, out var ru) ? ru : ReplaceNames(s);

        // Часть шаблона: точное совпадение или названия внутри ("Tatarin087 [T/A-30 Compass]").
        static string GamePart(string s) => Lookup(s, out var ru) || display.TryGetValue(s, out ru) ? ru : ReplaceNames(s);

        // Точное совпадение в тексте игры: наш словарь, затем базовый перевод (с нашей заменой, если она есть),
        // затем то же в ВЕРХНЕМ регистре.
        static bool Lookup(string s, out string ru)
        {
            if (game.TryGetValue(s, out ru)) return true;
            if (baseGame.TryGetValue(s, out ru))
            {
                if (game.TryGetValue(ru, out var better)) ru = better;
                return true;
            }
            return upper.Count > 0 && IsUpper(s) && upper.TryGetValue(s, out ru);
        }

        static bool IsUpper(string s)
        {
            bool letter = false;
            foreach (char c in s)
            {
                if (char.IsLower(c)) return false;
                letter |= char.IsLetter(c);
            }
            return letter;
        }

        static void AddBase(string key, string value)
        {
            baseGame[key] = value;
            display[key] = value; // display_*.json и остальные файлы грузятся позже и перекрывают
            if (key.Length >= LooseMinLength && !gameLoose.ContainsKey(Loose(key))) gameLoose[Loose(key)] = value;
        }

        // После загрузки базового перевода:
        //  * свой вариант названия ставим и по его переводу: игра показывает перевод, а не английское название
        //    ("Облицовка воздушных средств" -> "Капонир");
        //  * строим словарь ВЕРХНЕГО регистра для кнопок вроде "MISSION SETTINGS".
        static void AfterBaseLoaded()
        {
            foreach (var kv in names)
                if (baseGame.TryGetValue(kv.Key, out var lp) && lp != kv.Value && !game.ContainsKey(lp))
                    game[lp] = kv.Value;

            upper.Clear();
            foreach (var source in new[] { baseGame, game })
            foreach (var kv in source)
            {
                var key = kv.Key.ToUpperInvariant();
                if (key == kv.Key || baseGame.ContainsKey(key) || game.ContainsKey(key) || upper.ContainsKey(key)) continue;
                var value = kv.Value;
                if (game.TryGetValue(value, out var better)) value = better;
                upper[key] = value.ToUpperInvariant();
            }
            cache.Clear();
            gameCache.Clear();
        }

        public static bool HasCyrillic(string s)
        {
            foreach (char c in s)
                if (c >= 'Ѐ' && c <= 'ӿ') return true;
            return false;
        }

        static IEnumerable<KeyValuePair<string, string>> Read(string file)
        {
            Dictionary<string, string> map;
            try
            {
                map = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Bad JSON in {Path.GetFileName(file)}: {e.Message}");
                yield break;
            }
            if (map == null) yield break;
            foreach (var kv in map)
                if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                    yield return kv;
        }

        public static bool TryLiteral(string scope, string text, out string result)
        {
            if (Literals.Count == 0)
            {
                result = null;
                return false;
            }
            return Literals.TryGetValue(scope + "|" + text, out result) || Literals.TryGetValue(text, out result);
        }

        public static string Display(string text)
        {
            if (cache.TryGetValue(text, out var hit)) return hit;

            string result = text;
            if (display.TryGetValue(text, out var ru))
            {
                result = ru;
            }
            else
            {
                var trimmed = text.Trim();
                if (trimmed.Length != text.Length && display.TryGetValue(trimmed, out ru))
                    result = text.Replace(trimmed, ru);
                else if (displayPatterns.Count > 0 && TryPattern(displayPatterns, text, DisplayPart, out ru))
                    result = ru;
                // Кнопки Ground Control из нескольких строк: "Internal Cannon\n20mm Rotary Cannon (750 rounds)   v".
                else if (text.IndexOf('\n') >= 0 && TranslateLines(text, Display, out ru))
                    result = ru;
                else if ((ru = ReplaceNames(text)) != text)
                    result = ru;
                else if (Plugin.DumpUntranslated.Value && latin.IsMatch(text) && untranslated.Count < 5000)
                    untranslatedDirty |= untranslated.Add(digits.Replace(text, "#"));
            }

            if (cache.Count > 20000) cache.Clear();
            cache[text] = result;
            return result;
        }

        // Текст самой игры (TMP/UGUI). Если Localization Patch всё же установлен, он срабатывает раньше,
        // и сюда приходит уже его перевод — тогда действуют только наши замены по русскому ключу.
        public static string Game(string text)
        {
            if (text.Length == 0 || (game.Count == 0 && baseGame.Count == 0)) return text;
            if (gameCache.TryGetValue(text, out var hit)) return hit;

            string result = text;
            if (Lookup(text, out var ru))
            {
                result = ru;
            }
            else if (latin.IsMatch(text))
            {
                var m = edges.Match(text);
                var core = m.Groups[2].Value;
                if (core.Length > 0 && GameCore(core, core.Length < text.Length, out ru))
                    result = m.Groups[1].Value + ru + m.Groups[3].Value;
                else if (text.IndexOf('\n') >= 0 && TranslateLines(text, Game, out ru))
                    result = ru;
                else if (core.Length > 0 && TryNames(core, out ru))
                    result = m.Groups[1].Value + ru + m.Groups[3].Value;
                else if (Plugin.DumpUntranslated.Value && untranslatedGame.Count < 5000)
                    // Короткие строки с числами меняются каждый кадр (скорость, время) — их числа заменяем на #,
                    // а длинные подсказки сохраняем как есть: по ним пишется перевод.
                    untranslatedDirty |= untranslatedGame.Add(text.Length >= LooseMinLength ? text : digits.Replace(text, "#"));
            }

            if (gameCache.Count > 20000) gameCache.Clear();
            gameCache[text] = result;
            return result;
        }

        // Многострочный текст, который целиком не нашёлся, переводим построчно:
        // "Stability Assist <b>Enabled</b>\nNav Lights <b>On</b>\n..." в игре, кнопки пилонов в Ground Control.
        static bool TranslateLines(string text, Func<string, string> translate, out string result)
        {
            var lines = text.Split('\n');
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var line = translate(lines[i]);
                if (line == lines[i]) continue;
                lines[i] = line;
                changed = true;
            }
            result = changed ? string.Join("\n", lines) : null;
            return changed;
        }

        // Названия внутри текста игры ("Maris Airport 1,2km"). Строку из одних английских слов
        // (название сервера, миссии из мастерской) не трогаем, если в ней останется английское слово.
        static bool TryNames(string core, out string result)
        {
            result = ReplaceNames(core);
            if (result == core) return false;
            return HasCyrillic(core) || !word.IsMatch(result);
        }

        // exact == false, когда ядро совпадает со всем текстом: его уже искали.
        static bool GameCore(string core, bool exact, out string result)
        {
            if (exact && Lookup(core, out result))
                return true;

            var n = numbered.Match(core);
            if (n.Success && Lookup(n.Groups[2].Value, out result))
            {
                result = n.Groups[1].Value + result;
                return true;
            }

            if (core.Length >= LooseMinLength && gameLoose.TryGetValue(Loose(core), out result))
                return true;

            result = null;
            return gamePatterns.Count > 0 && TryPattern(gamePatterns, core, GamePart, out result);
        }

        public static void SaveUntranslated()
        {
            if (!untranslatedDirty) return;
            untranslatedDirty = false;
            Save(untranslated, "untranslated.txt");
            Save(untranslatedGame, "untranslated_game.txt");
        }

        static void Save(HashSet<string> set, string file)
        {
            try
            {
                var list = new List<string>(set);
                list.Sort(StringComparer.Ordinal);
                File.WriteAllText(Path.Combine(Plugin.PluginDir, file), JsonConvert.SerializeObject(list, Formatting.Indented));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not save {file}: {e.Message}");
            }
        }
    }
}
