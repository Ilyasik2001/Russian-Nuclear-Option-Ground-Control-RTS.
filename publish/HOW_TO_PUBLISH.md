# Как выложить мод

Steam Workshop для этого не подходит: Мастерская Nuclear Option принимает только миссии и раскраски,
а плагины BepInEx из подписок игра не загружает. Моды вроде этого выкладывают так:
**GitHub** (исходники и архив релиза) → **каталог NOMM** (заявка в репозиторий NOMNOM).

Названия:

| Где | Как |
|---|---|
| Название мода (NOMM, релиз, журнал BepInEx) | `Rusifikator Nuclear Option + Ground Control` |
| Описание репозитория на GitHub | `Русификатор Nuclear Option + Ground Control` |
| Репозиторий, архив, папка мода, DLL | `Rusifikator-NuclearOption-GroundControl` |

Всё нужное уже лежит в папке проекта:

| Файл | Зачем |
|---|---|
| `dist\Rusifikator-NuclearOption-GroundControl-1.0.0.zip` | архив релиза |
| `dist\Rusifikator-NuclearOption-GroundControl-1.0.0.zip.sha256` | его контрольная сумма |
| `publish\DESCRIPTION.md` | тексты: название, описания, текст релиза |
| `publish\nomnom\Rusifikator-NuclearOption-GroundControl.json` | заявка в каталог NOMM |

---

## Шаг 1. Репозиторий на GitHub

1. Откройте https://github.com/new под своим аккаунтом **ILya**.
2. **Repository name:** `Rusifikator-NuclearOption-GroundControl`.
3. **Description:** `Русификатор Nuclear Option + Ground Control`.
4. Видимость **Public**.
5. Галочки «Add a README», «.gitignore», «license» **не ставьте** — эти файлы уже есть.
6. Нажмите **Create repository**.

> Проверьте адрес нового репозитория. Если он не `https://github.com/ILya/Rusifikator-NuclearOption-GroundControl`
> (например, другой регистр букв в нике), поправьте ссылки в
> `publish\nomnom\Rusifikator-NuclearOption-GroundControl.json`, `publish\DESCRIPTION.md`, `README.md`
> и в команде ниже.

## Шаг 2. Отправить файлы

В папке проекта уже есть git-репозиторий со всеми коммитами. Выполните в терминале:

```
git -C "D:\mods NC\Rusifikator-NuclearOption-GroundControl" remote add origin https://github.com/ILya/Rusifikator-NuclearOption-GroundControl.git
git -C "D:\mods NC\Rusifikator-NuclearOption-GroundControl" push -u origin main
```

При первой отправке откроется окно входа в GitHub — войдите в аккаунт.

*Без командной строки:* установите GitHub Desktop → **File → Add local repository** →
выберите `D:\mods NC\Rusifikator-NuclearOption-GroundControl` → **Publish repository**.
Имя репозитория GitHub Desktop возьмёт из названия папки; в поле **Description** впишите
`Русификатор Nuclear Option + Ground Control` и снимите галочку **Keep this code private**.
Шаг 1 в этом случае не нужен.

## Шаг 3. Темы репозитория

На странице репозитория нажмите ⚙ рядом с **About**. Описание там уже стоит; в **Topics** добавьте:
`nuclear-option`, `ground-control`, `bepinex`, `russian`, `translation`, `rusifikator`.

## Шаг 4. Релиз

1. На странице репозитория: **Releases → Draft a new release**.
2. **Choose a tag:** впишите `v1.0.0` и нажмите **Create new tag**.
3. **Release title:** `Rusifikator Nuclear Option + Ground Control 1.0.0`.
4. Описание — раздел 3 из `publish\DESCRIPTION.md`.
5. Перетащите в окно файл `dist\Rusifikator-NuclearOption-GroundControl-1.0.0.zip`.
6. **Publish release.**

Ссылка на архив должна получиться такой:
`https://github.com/ILya/Rusifikator-NuclearOption-GroundControl/releases/download/v1.0.0/Rusifikator-NuclearOption-GroundControl-1.0.0.zip`
— именно она указана в заявке для NOMM.

> Загружайте именно тот архив, что лежит в `dist` сейчас: его контрольная сумма уже вписана в заявку.
> Если пересоберёте архив, сумма изменится — возьмите новую из
> `dist\Rusifikator-NuclearOption-GroundControl-1.0.0.zip.sha256` и замените строку `"hash"`
> в `publish\nomnom\Rusifikator-NuclearOption-GroundControl.json`.

## Шаг 5. Каталог NOMM

1. Прочитайте правила каталога: https://github.com/KopterBuzz/NOMNOM (раздел про Mod Submission).
   Наш мод им соответствует: открытый код, авторы и лицензии указаны.
2. Нажмите **Fork** — у вас появится копия репозитория NOMNOM.
3. В своей копии откройте папку `modManifests` → **Add file → Upload files** →
   загрузите `publish\nomnom\Rusifikator-NuclearOption-GroundControl.json` → **Commit changes**.
4. Нажмите **Contribute → Open pull request**. Заголовок:
   `Add Rusifikator Nuclear Option + Ground Control`, текст:
   `Russian translation of Nuclear Option and the Ground Control (RTS) mod.`
5. Дождитесь автоматической проверки (зелёная галочка) и одобрения. После этого мод появится в NOMM.

## Шаг 6. Рассказать игрокам

- Discord Nuclear Option — канал модов (вход из главного меню игры).
- Обсуждения игры в Steam — тема «Русификатор Nuclear Option + Ground Control» с описанием
  (раздел 2 из `publish\DESCRIPTION.md`) и ссылкой на GitHub. Приложите пару скриншотов.
- Автору Ground Control — что его мод переведён на русский.
- По желанию: Nexus Mods (раздел Nuclear Option) — туда загружается тот же архив.

---

## Как выпускать обновления

1. Поменяйте версию в двух местах: `<Version>` в `Rusifikator-NuclearOption-GroundControl.csproj` и
   `Version` в `Plugin.cs` (например, `1.0.1`).
2. Допишите изменения в `CHANGELOG.md`.
3. Соберите архив: `powershell -File tools\package.ps1`.
4. Отправьте изменения: `git add -A`, `git commit -m "1.0.1"`, `git push`.
5. Создайте новый релиз `v1.0.1` с новым архивом (шаг 4).

Повторно подавать заявку в NOMM не нужно: в ней включено `autoUpdateArtifacts`,
и каталог сам находит новые релизы на GitHub.
