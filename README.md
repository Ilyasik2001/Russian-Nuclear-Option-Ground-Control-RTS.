# Русификатор Nuclear Option + Ground Control

Русский перевод **Nuclear Option** и мода **Ground Control (RTS)** 

- Интерфейс игры: меню, HUD, настройки, ангар, энциклопедия, описания миссий, обучение (все 5 уроков), подсказки.
- Мод Ground Control: все окна командования, постройки, депо, снабжение, авиация, обучение, сообщения.
- Шрифт с кириллицей для игры (Tektur) и для окон Ground Control (системный Arial).
- Русский текст, который не влезает в кнопку, автоматически рисуется шрифтом чуть мельче.
- Текст переводится в момент появления, без постоянного перебора экрана, — игра не подтормаживает.

Обозначения оружия (AGM-48, IRM-S2), позывные, названия серверов и миссий из Мастерской остаются как есть.

## Требования

- Nuclear Option  (проверено на Steam вресии игры 0.34.6).
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (проверено на 5.4.23.4) обязателен, без него моды работать не будут !
## По желанию 
- [Ground Control (RTS)](https://github.com/simonsimme/Ground-Controll-RTS) — мод по желанию (Рекомендую) (проверено на 1.0.0.0). Без него переводится только игра.
- [NOMM](https://github.com/Combat787/NOMM) — менеджер модов по желанию

## Установка

**Вручную** — скачайте архив со страницы [релизов](https://github.com/Ilyasik2001/Rusifikator-NuclearOption-GroundControl/releases)
и распакуйте в D:\steamapps\common\Nuclear Option\BepInEx\plugins\(Распаковка архива мода)
Должно получится так: D:\steamapps\common\Nuclear Option\BepInEx\plugins\Rusifikator-NuclearOption-GroundControl 1.0.1

**Через менеджер NOMM** — найдите «Rusifikator Nuclear Option + Ground Control» в списке модов и установите.

Нашли непереведённое — пришлите скриншот в [Issues](https://github.com/Ilyasik2001/Rusifikator-NuclearOption-GroundControl/issues).


## Авторы

- Плагин, перевод игры и мода Ground Control — Tatarok.
- Шрифт Tektur — The Tektur Project Authors, SIL Open Font License 1.1.

Подробнее о лицензиях — `LICENSE` и `THIRD_PARTY_NOTICES.md`.

---

## English

Rusifikator Nuclear Option + Ground Control is a Russian translation of Nuclear Option and the
Ground Control (RTS) mod, as a BepInEx 5 plugin.

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


### Обновление Ground Control

```
dotnet run --project tools\DllStrings -c Release -- "<игра>\BepInEx\plugins\GroundControlRts\GroundControlRts.dll" ldstr > gc_ldstr.tsv
powershell -File tools\candidates.ps1 gc_ldstr.tsv gc_cand.tsv
powershell -File tools\validate.ps1 gc_ldstr.tsv gc_cand.tsv gc_untranslated.txt
```

`validate.ps1` покажет ключи, которых больше нет в моде, расхождения `{0}` и новые строки без перевода.
