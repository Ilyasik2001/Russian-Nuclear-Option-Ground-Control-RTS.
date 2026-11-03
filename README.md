# Русификатор Nuclear Option + Ground Control

Русский перевод **Nuclear Option** и мода **Ground Control (RTS)** 

Этот проект начинался как перевод мода Ground Control — той самой RTS, которая превращает Nuclear Option в стратегию с командованием армией. Но в процессе работы я понял, что хочу перевести и саму игру. В итоге родился полный русификатор: от главного меню до последнего окна в командовании.

## Что важно знать:

Гибридный подход. Мод Ground Control ставить не обязательно. Без него русификатор переведет только саму Nuclear Option. Если мод установлен — переведется и он.

Работает незаметно. Текст переводится прямо в момент появления на экране, без постоянного перебора всех надписей. Поэтому камера и техника двигаются плавно, без рывков и просадок FPS.
## Скрины перевода 
<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/a4677f0f-e953-48ee-af0b-aaab7ec41903" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/7d6e9e54-1347-4161-8f96-c00cb69d7ac8" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/85fb64da-386d-4c41-a11a-e355d858a14b" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/cea9c4c3-d8b4-4235-8194-83a17c721968" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/96c814ca-e162-4c3e-b52a-56e1bcfbcf74" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/7b6cd5ad-be21-4af8-bc4f-a3b5c29ed0cb" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/d9d2631c-d057-4848-9829-dc15043b6527" />

<img width="978" height="552" alt="image" src="https://github.com/user-attachments/assets/bb454e85-1b69-4099-b417-f05c37057c8d" />

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
