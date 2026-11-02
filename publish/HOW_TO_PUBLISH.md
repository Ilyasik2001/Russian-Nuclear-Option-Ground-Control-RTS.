# Как выложить мод

Steam Workshop для этого не подходит: Мастерская Nuclear Option принимает только миссии и раскраски,
а плагины BepInEx из подписок игра не загружает. Моды вроде этого выкладывают так:
**GitHub** (исходники и архив релиза) → **каталог NOMM** (заявка в репозиторий NOMNOM).

Всё нужное уже лежит в папке проекта:

| Файл | Зачем |
|---|---|
| `dist\NuclearOptionRussian-1.0.0.zip` | архив релиза |
| `dist\NuclearOptionRussian-1.0.0.zip.sha256` | его контрольная сумма |
| `publish\DESCRIPTION.md` | тексты: короткое описание, описание мода, текст релиза |
| `publish\nomnom\NuclearOptionRussian.json` | заявка в каталог NOMM |

---

## Шаг 1. Репозиторий на GitHub

1. Откройте https://github.com/new под своим аккаунтом **ILya**.
2. **Repository name:** `NuclearOptionRussian`, видимость **Public**.
3. Галочки «Add a README», «.gitignore», «license» **не ставьте** — эти файлы уже есть.
4. Нажмите **Create repository**.

> Проверьте адрес нового репозитория. Если он не `https://github.com/ILya/NuclearOptionRussian`
> (например, другой регистр букв в нике), поправьте ссылки в `publish\nomnom\NuclearOptionRussian.json`,
> `publish\DESCRIPTION.md` и в команде ниже.

## Шаг 2. Отправить файлы

В папке проекта уже создан git-репозиторий с первым коммитом. Выполните в терминале:

```
git -C "D:\mods NC\NuclearOptionRussian" remote add origin https://github.com/ILya/NuclearOptionRussian.git
git -C "D:\mods NC\NuclearOptionRussian" push -u origin main
```

При первой отправке откроется окно входа в GitHub — войдите в аккаунт.

*Без командной строки:* установите GitHub Desktop → **File → Add local repository** →
выберите `D:\mods NC\NuclearOptionRussian` → **Publish repository**.

## Шаг 3. Описание репозитория

На странице репозитория нажмите ⚙ рядом с **About** и вставьте короткое описание
(раздел 1 из `publish\DESCRIPTION.md`). В **Topics** можно добавить: `nuclear-option`, `bepinex`,
`russian`, `translation`.

## Шаг 4. Релиз

1. На странице репозитория: **Releases → Draft a new release**.
2. **Choose a tag:** впишите `v1.0.0` и нажмите **Create new tag**.
3. **Release title:** `Nuclear Option Russian 1.0.0`.
4. Описание — раздел 3 из `publish\DESCRIPTION.md`.
5. Перетащите в окно файл `dist\NuclearOptionRussian-1.0.0.zip`.
6. **Publish release.**

Ссылка на архив должна получиться такой:
`https://github.com/ILya/NuclearOptionRussian/releases/download/v1.0.0/NuclearOptionRussian-1.0.0.zip`
— именно она указана в заявке для NOMM.

> Загружайте именно тот архив, что лежит в `dist` сейчас: его контрольная сумма уже вписана в заявку.
> Если пересоберёте архив, сумма изменится — возьмите новую из `dist\NuclearOptionRussian-1.0.0.zip.sha256`
> и замените строку `"hash"` в `publish\nomnom\NuclearOptionRussian.json`.

## Шаг 5. Каталог NOMM

1. Прочитайте правила каталога: https://github.com/KopterBuzz/NOMNOM (раздел про Mod Submission).
   Наш мод им соответствует: открытый код, авторы и лицензии указаны.
2. Нажмите **Fork** — у вас появится копия репозитория NOMNOM.
3. В своей копии откройте папку `modManifests` → **Add file → Upload files** →
   загрузите `publish\nomnom\NuclearOptionRussian.json` → **Commit changes**.
4. Нажмите **Contribute → Open pull request**. Заголовок:
   `Add Nuclear Option Russian`, текст: `Russian translation of Nuclear Option and the Ground Control (RTS) mod.`
5. Дождитесь автоматической проверки (зелёная галочка) и одобрения. После этого мод появится в NOMM.

## Шаг 6. Рассказать игрокам

- Discord Nuclear Option — канал модов (вход из главного меню игры).
- Обсуждения игры в Steam — тема с описанием (раздел 2 из `publish\DESCRIPTION.md`)
  и ссылкой на GitHub. Приложите пару скриншотов.
- Автору Ground Control — что его мод переведён на русский.
- По желанию: Nexus Mods (раздел Nuclear Option) — туда загружается тот же архив.

---

## Как выпускать обновления

1. Поменяйте версию в двух местах: `<Version>` в `NuclearOptionRussian.csproj` и
   `Version` в `Plugin.cs` (например, `1.0.1`).
2. Допишите изменения в `CHANGELOG.md`.
3. Соберите архив: `powershell -File tools\package.ps1`.
4. Отправьте изменения: `git add -A`, `git commit -m "1.0.1"`, `git push`.
5. Создайте новый релиз `v1.0.1` с новым архивом (шаг 4).

Повторно подавать заявку в NOMM не нужно: в ней включено `autoUpdateArtifacts`,
и каталог сам находит новые релизы на GitHub.
