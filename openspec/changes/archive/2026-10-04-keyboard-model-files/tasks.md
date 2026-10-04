## 1. Модели

- [x] 1.1 `src/models/zorner-zh99-he.json` — встроенная ZH99 HE (20 эффектов, `options` по нынешнему `Effect.All`); `EmbeddedResource` в `src/KbLight.csproj` с условием `'$(NoBuiltinModels)' != 'true'`. Результат — сборка без ошибок и предупреждений.
- [x] 1.2 `src/KeyboardModel.cs`: `KeyboardModel`, `KeyboardModels.Current()` (встроенная + `models\*.json`, замена по `VID:PID`, кеш по времени изменения и длине, строки журнала один раз), проверка полей с текстами из design.md, решение 3. `Effect.All` заменён таблицей названий `Effect.KnownNames` и разбором `options`. Результат — сборка без предупреждений.

## 2. Обмен с клавиатурой

- [x] 2.1 `src/Keyboard.cs`: выбор интерфейсов по моделям, `ApplyStatus.Unsupported`, `ApplyResult.Model`, строка «пропускаю» для незнакомых при наличии знакомых; `Keyboard.IsInteresting` для `src/SystemWatcher.cs`. Результат — сборка без предупреждений.
- [x] 2.2 `src/Program.cs`: код 4 при `Unsupported`. Результат — сборка без предупреждений.

## 3. Трей, настройки и окно

- [x] 3.1 `src/LightSettings.cs`: поле `Model`; `Describe` берёт название эффекта из модели `Model` или из таблицы. Результат — сборка без предупреждений.
- [x] 3.2 `src/TrayApp.cs`: состояние для `Unsupported` дословно по спеке; обновление `Model` и сохранение в `OnApplied`; событие для окна. Результат — сборка без предупреждений.
- [x] 3.3 `src/SettingsForm.cs`: строка «Клавиатура: …», список эффектов из модели, ссылка «Модели» (создаёт и открывает папку). Результат — сборка без предупреждений.

## 4. Инструменты и документ

- [x] 4.1 `tools/kbtool.py`: ключ `--pid`. Результат — `python tools/kbtool.py --pid FB2A read` выводит блок, `--pid FE20 read` пишет, что клавиатура не найдена.
- [x] 4.2 `tools/make_model.py`. Результат — `python tools/make_model.py --name "ZORNER ZH99 HE" --out <scratch>\zh99.json` даёт файл, совпадающий по `vid`, `pid`, `interface`, кодам и `options` со встроенным `src/models/zorner-zh99-he.json` (сверка скриптом).
- [x] 4.3 `docs/ADDING-A-KEYBOARD.md` (English): найти `VID:PID` (Диспетчер устройств или `kbtool.py list`), проверить `kbtool.py read` (маркер `AA BB`) и `setmode` на глаз, собрать файл `make_model.py` или руками, положить в `models`, проверить журнал, прислать PR в `src/models/` или issue с файлом и выводом `kbtool.py read`/`info`.

## 5. Проверка на клавиатуре (вместе с пользователем)

- [x] 5.1 «Выход» у установленного KbLight; `dotnet publish src -c Release -o publish`; `publish\KbLight.exe --check` → код 0, `--check: AlreadySet — уже стоит: effect=13 …`.
- [x] 5.2 Запустить `publish\KbLight.exe` → окно: «Клавиатура: ZORNER ZH99 HE», 20 эффектов; в `settings.json` появилось `"Model": "19F5:FB2A"` (остальные поля не изменились); ссылка «Модели» открыла `%LOCALAPPDATA%\KbLight\models`.
- [x] 5.3 Положить в `models\test.json` модель `19F5:FB2A` «Тест» с эффектами 1, 3, 13; заново открыть окно → «Клавиатура: Тест», три эффекта; в журнале один раз `модель 19F5:FB2A: test.json заменяет встроенное описание`. Затем испортить файл (`brighness`) → в журнале один раз `файл модели test.json не прочитан: неизвестный параметр brighness`, окно снова показывает ZH99 HE. Удалить `test.json`.
- [x] 5.4 «Выход»; `dotnet publish src -c Release -o publish-test -p:NoBuiltinModels=true`; `publish-test\KbLight.exe --apply` → код 4, `--apply: Unsupported — клавиатура 19F5:FB2A не знакома программе, нужен файл модели`; `kbtool.py read` — блок не изменился. Запустить `publish-test\KbLight.exe` → состояние «Клавиатура 19F5:FB2A не знакома программе. Нужен файл модели — ссылка «Модели» в окне.», в журнале одна попытка без повторов. «Выход», удалить `publish-test`.
- [x] 5.5 Вернуть всё: запустить установленный `KbLight.exe --tray`, `KbLight.exe --apply` → код 0, `kbtool.py read` — «Рябь от нажатий», 100; задача `KbLight` указывает на установленный exe; `settings.json` пользователя возвращён из резервной копии побайтно, папка `models`, созданная проверкой, удалена.

## 6. Документы

- [x] 6.1 `docs/CHANGELOG.md` («Не выпущено»): файлы моделей, `Unsupported` и код 4, поле `Model`, строка «Клавиатура» и ссылка «Модели», `make_model.py`, `kbtool.py --pid`.
- [x] 6.2 `README.md`: модель ZORNER ZH99 HE, папка `models`, код 4, ссылка на `docs/ADDING-A-KEYBOARD.md`; `docs/PROTOCOL.md` §1 — модель `FB2A` = ZORNER ZH99 HE; `docs/ARCHITECTURE.md` — файлы, выбор интерфейсов, §7 (`Model`), §13; `docs/ADR.md` — ADR-009 «Модели — файлы данных»; `CLAUDE.md` — строка о `src/models/` и `make_model.py`, если нужно.
