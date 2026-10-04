# CLAUDE.md — KbLight

KbLight — утилита в трее Windows: держит выбранную пользователем подсветку клавиатуры sbarda (проверена на ZORNER ZH99 HE, USB `19F5:FB2A`; другие модели — файлами моделей) и пишет её в клавиатуру при входе в Windows, при подключении клавиатуры, после сна и при каждой правке. Только подсветка: раскладка, макросы, срабатывание — вне проекта (ADR-002).

Отвечать и писать документы по-русски; комментарии в коде — по-английски.

## Документы

| Что | Где |
|---|---|
| Что программа обязана делать | `openspec/specs/` — источник правды по поведению |
| Как устроена: файлы, потоки, очередь записи, пути | `docs/ARCHITECTURE.md` |
| Протокол клавиатуры: проверено, выведено, не расшифровано | `docs/PROTOCOL.md` |
| Модели клавиатур: формат файла, как добавить свою | `docs/ADDING-A-KEYBOARD.md`, встроенные — `src/models/` |
| Почему так решили | `docs/ADR.md` |
| Версии | `docs/CHANGELOG.md` |
| Для пользователя: установка, ключи, удаление | `README.md` (по-английски), `README.ru.md` — держать одинаковыми по смыслу |

## Сборка и установка

```
dotnet build src -c Release                 # ворота: 0 ошибок, 0 предупреждений
dotnet publish src -c Release -o publish    # publish\KbLight.exe, один файл
```

Рабочая установка — `%LOCALAPPDATA%\Programs\KbLight\KbLight.exe`, её запускает задача автозапуска. Обновлять — только по просьбе пользователя: закрыть программу («Выход» в меню значка или `Stop-Process -Name KbLight`), скопировать `publish\KbLight.exe` поверх и запустить `KbLight.exe --tray`. Задача указывает на этот путь, так что переустановка автозапуск не ломает.

Версия — `<Version>` в `src/KbLight.csproj`, вместе с записью в `docs/CHANGELOG.md`.

## Выпуск версии

Только по явному слову пользователя — это публикация.

1. `<Version>` в `src/KbLight.csproj`; в `docs/CHANGELOG.md` раздел «Не выпущено» → `## [X.Y.Z] — дата`; описание релиза по-английски для пользователей — `.github/release-notes/vX.Y.Z.md` (без него в релиз уйдёт русский раздел CHANGELOG).
2. Коммит, `git push`; дождаться зелёного `Build` (`gh run list`).
3. `git tag vX.Y.Z` и `git push origin vX.Y.Z` — workflow `Release` (`.github/workflows/release.yml`) собирает exe на GitHub, сверяет тег с `<Version>` и `--version`, считает SHA256 и создаёт релиз. Проверка после — `gh release view vX.Y.Z`, скачать exe и сверить хеш.

`Build` (`.github/workflows/build.yml`) — ворота на каждый push в `main` и pull request: сборка с `-warnaserror` и `openspec validate --all --strict`.

## Проверка

Автотестов нет: поведение завязано на живую клавиатуру. Проверка — сценарии ниже; результат смотрим в журнале `%LOCALAPPDATA%\KbLight\kblight.log` и прямым чтением клавиатуры.

1. `python tools/kbtool.py read` — что лежит в клавиатуре сейчас, без KbLight.
2. `KbLight.exe --check` → код 0, в журнале `--check: AlreadySet — уже стоит: …`.
3. Подмена: `python tools/kbtool.py setmode 1` («Спектр»), затем `KbLight.exe --check` → код 0, в журнале `--check: Written — было: effect=1 …; стало: effect=13 …`.
4. Трей: запустить exe — в журнале `запуск …` и `запуск программы: Written — …`; правка в окне — `изменены настройки: Written — …`, и `kbtool.py read` показывает новое значение.

5. Проверка обновлений: `dotnet publish src -c Release -p:Version=1.0.0 -o <временная папка>`, закрыть установленный трей, запустить копию из папки → через минуту в журнале `доступна версия <последний релиз>: https://github.com/lostintired/sbarda-kblight/releases/tag/v…`, в окне ссылка «доступна версия …». Сеть без интернета — `HTTPS_PROXY=http://127.0.0.1:9` → `проверка обновлений не удалась: …`.

Что ещё не проверено руками — `docs/ARCHITECTURE.md`, §13.

Перед опытами закрыть sbarda.exe. `kbtool.py` мьютекс KbLight не берёт — не запускать его, пока трей пишет. После опытов вернуть подсветку пользователя: `KbLight.exe --apply`. `settings.json` пользователя без нужды не трогать, а если трогали — вернуть как было.

## Правила

- Запись в клавиатуру — только «прочитать блок → поменять байты 8–16 → записать → перечитать и сверить» (ADR-003). Чужие байты блока не трогать и не «чинить».
- Новая команда или новый байт протокола — сначала проверка на клавиатуре (`tools/kbtool.py`, перехват `tools/capture/`), потом код; в `docs/PROTOCOL.md` — пометка ✔, ◐ или ?.
- С клавиатурой общаются только поток записи трея (очередь `TrayApp`) и разовые режимы через `Keyboard.Apply`; поток окна устройство не ждёт.
- Без прав администратора и без NuGet-зависимостей (ADR-004, ADR-005).
- Тексты окна, меню и журнала — на двух языках, по языку Windows или `KBLIGHT_LANG=ru|en`; пары лежат в `src/Text.cs`, других литералов текста в коде нет. Спецификации цитируют русские тексты дословно, английские — таблица в спеке `interface-language`; новый или изменённый текст — в обе и в `Text.cs`.
- P/Invoke: `HidD_*` возвращают `BOOLEAN` (1 байт) — `[return: MarshalAs(UnmanagedType.U1)]`; буферы overlapped-операции живут, пока она не завершилась (`HidDevice.Transfer`).

## OpenSpec

Процесс с 2026-10-04 (CLI `openspec`, глобально из npm): `/opsx:explore` по желанию → `/opsx:propose <что меняем>` → `/opsx:apply` → `/opsx:archive`. У Codex — скиллы `openspec-*` из `.agents/skills/`. Изменение поведения начинается с change в `openspec/changes/<имя>/`; правила артефактов — `openspec/config.yaml`. Проверка артефактов — `openspec validate --all --strict`; что в работе — `openspec list`, спеки — `openspec list --specs`.
