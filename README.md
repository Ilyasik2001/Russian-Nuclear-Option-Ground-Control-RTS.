# Русификатор Nuclear Option + Ground Control

Русский перевод **Nuclear Option** и мода **Ground Control (RTS)** — плагин для BepInEx 5.

- Интерфейс игры: меню, HUD, настройки, ангар, энциклопедия, описания миссий, обучение (все 5 уроков), подсказки.
- Мод Ground Control: все окна командования, постройки, депо, снабжение, авиация, обучение, сообщения.
- Шрифт с кириллицей для игры (Tektur) и для окон Ground Control (системный Arial).
- Русский текст, который не влезает в кнопку, автоматически рисуется шрифтом чуть мельче.
- Текст переводится в момент появления, без постоянного перебора экрана, — игра не подтормаживает.

Обозначения оружия (AGM-48, IRM-S2), позывные, названия серверов и миссий из Мастерской остаются как есть.

## Требования

- Nuclear Option (проверено на Early Access 0.34.6).
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (проверено на 5.4.23.4) — его ставит NOMM.
- [Ground Control (RTS)](https://github.com/simonsimme/Ground-Controll-RTS) — мод по желанию (Рекомендую) (проверено на 1.0.0.0). Без него переводится только игра.
- [NOMM](https://github.com/Combat787/NOMM) — по желанию

## Установка

**Через менеджер NOMM** — найдите «Rusifikator Nuclear Option + Ground Control» в списке модов и установите.

**Вручную** — скачайте архив со страницы [релизов](https://github.com/Ilyasik2001/Rusifikator-NuclearOption-GroundControl/releases)
и распакуйте в D:\steamapps\common\Nuclear Option\BepInEx\plugins\(Распаковка мода)

Нашли непереведённое — пришлите скриншот в [Issues](https://github.com/Ilyasik2001/Rusifikator-NuclearOption-GroundControl/issues).

## Настройки

Файл `<папка игры>\BepInEx\config\com.gcru.russian.cfg` появляется после первого запуска.

| Параметр | По умолчанию | Что делает |
|---|---|---|
| `TranslateGame` | `true` | Перевод интерфейса самой игры |
| `TranslateGroundControl` | `true` | Перевод мода Ground Control |
| `TranslateImgui` | `true` | Перевод окон Ground Control |
| `FitText` | `true` | Уменьшать шрифт текста, который не помещается |
| `GameFont` | `Tektur-Reg.ttf` | Шрифт с кириллицей для игры (файл в `fonts` или полный путь) |
| `FontNames` | `Arial Narrow, Arial` | Системный шрифт для окон Ground Control |
| `DumpUntranslated` | `false` | Для переводчиков: собирать непереведённый текст в файлы рядом с плагином |

## Авторы

- Плагин, перевод игры и мода Ground Control — Tatarok.
- Шрифт Tektur — The Tektur Project Authors, SIL Open Font License 1.1.
- Ground Control (RTS) — его авторы; мод в этот репозиторий не входит.

Подробнее о лицензиях — `LICENSE` и `THIRD_PARTY_NOTICES.md`.

---

## English

Rusifikator Nuclear Option + Ground Control is a Russian translation of Nuclear Option and the
Ground Control (RTS) mod, as a BepInEx 5 plugin. It translates text only when it appears, so there is no periodic full-scene scan. Install with NOMM
or extract the release archive into `BepInEx\plugins\Rusifikator-NuclearOption-GroundControl\`.
Ground Control is optional.

---

## Для разработчиков

### Сборка

Нужны .NET SDK и установленная игра (ссылки на её библиотеки берутся из папки игры).

```
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Nuclear Option"
```

Без `-p:GameDir` используется путь из `Rusifikator-NuclearOption-GroundControl.csproj`. После сборки DLL,
переводы и шрифт копируются в `BepInEx\plugins\Rusifikator-NuclearOption-GroundControl` игры.

Архив для релиза и его SHA-256:

```
powershell -File tools\package.ps1
```

Версия берётся из `<Version>` в `Rusifikator-NuclearOption-GroundControl.csproj` и должна совпадать
с `Plugin.Version`.

### Как работает

- `LiteralPatcher.cs` — до запуска Ground Control меняет строковые литералы в его коде
  (Harmony-транспайлер по `ldstr`). Переводятся и шаблоны `String.Format`, и куски склеек.
  Литералы, которые уходят в сравнения, ключи словарей, конфиг, сеть и рефлексию, не трогаются.
  GUID `com.gcru.russian` по алфавиту раньше `com.groundcontrol.rts` — поэтому плагин
  загружается первым и ловит загрузку сборки мода.
- `ImguiPatches.cs` — перевод IMGUI по точному совпадению (`GUIContent.Temp` / `new GUIContent`):
  строки из `display_*.json`, названия из `base_ru.json` и `names_*.json`. Поля ввода не трогаются.
- `FontPatches.cs` — заменяет шрифт Barlow (без кириллицы) в IMGUI на системный (`FontNames`).
- `GameTextPatches.cs` — переводит текст самой игры (TMP/UGUI) в момент, когда он задаётся
  (сеттер `text`, `SetText`) или появляется на экране (`OnEnable`), без периодического перебора.
  Поля ввода не трогаются.
- `CyrillicFont.cs` — добавляет к шрифтам TextMeshPro запасной шрифт с кириллицей (`GameFont`).
- `FitPatches.cs` и автоподбор в `GameTextPatches.cs` — русский текст, который не влезает в кнопку
  или подпись, рисуется шрифтом помельче (не меньше 55–60 % исходного).

### Переводы

`translations/*.json` — плоские словари `"English": "Русский"`.

- `base_*.json` — основной перевод интерфейса игры: меню, настройки, HUD, энциклопедия, миссии, советы.
  Попадает и в окна Ground Control (названия из игры), в ВЕРХНЕМ регистре находится по обычному ключу.
- `gc_*.json` — литералы Ground Control. Ключ `"Тип::Метод|English"` действует только в этом методе.
- `display_*.json` — только на экране: строки, которые мод сравнивает в коде
  (категории техники, метки, названия грузов), переводить в исходнике нельзя.
- `game_*.json` — остальной текст игры: обучение, описания миссий, шаблоны. Если установлен
  Localization Patch, ключом может быть и его русский текст — он срабатывает раньше нас.
- `shared_*.json` — и то и другое: описания техники видны и в окнах Ground Control, и в энциклопедии игры.
- `names_*.json` — названия техники, зданий, баз, пилонов, оружия, пресетов и клавиш. Ground Control
  вклеивает их в свои строки (`"Мешает: Vehicle Depot."`, `"GOLD MINE   УР. 2/3"`), поэтому они
  заменяются и внутри строки, в том числе в ВЕРХНЕМ регистре.

В `display_*.json` и `game_*.json` ключ с `{0}`, `{1}`… — шаблон: `"Air: {0}": "Авиация: {0}"`.
Пробел в шаблоне совпадает с любым промежутком (и с переносом строки), два пробела — с двумя и больше.
Подставленная часть тоже переводится, если она есть в словаре. Многострочный текст, который целиком
не нашёлся, переводится построчно.

Чтобы найти непереведённое, включите `DumpUntranslated = true`: плагин соберёт рядом с DLL
`untranslated.txt` (окна Ground Control), `untranslated_game.txt` (игра) и `fit_debug.txt`
(почему надпись уменьшена или нет).

### Обновление Ground Control

```
dotnet run --project tools\DllStrings -c Release -- "<игра>\BepInEx\plugins\GroundControlRts\GroundControlRts.dll" ldstr > gc_ldstr.tsv
powershell -File tools\candidates.ps1 gc_ldstr.tsv gc_cand.tsv
powershell -File tools\validate.ps1 gc_ldstr.tsv gc_cand.tsv gc_untranslated.txt
```

`validate.ps1` покажет ключи, которых больше нет в моде, расхождения `{0}` и новые строки без перевода.
