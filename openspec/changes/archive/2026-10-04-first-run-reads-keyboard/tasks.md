## 1. Чтение подсветки из клавиатуры

- [x] 1.1 `src/Keyboard.cs`: вынести мьютекс устройства и перебор интерфейсов `MI_01` из `Apply` в общий метод; добавить `Keyboard.Read()` (сеанс `01 → 05 → 02`, без `06`) с итогами `AlreadySet` / `NotFound` / `Failed` и текстами из design.md. Результат — `dotnet build src -c Release`: 0 ошибок, 0 предупреждений.
- [x] 1.2 `src/LightSettings.cs`: `SetFromBlock(ReadOnlySpan<byte> block)` по формулам design.md, решение 5. Результат — сборка без предупреждений.

## 2. Первый запуск в трее

- [x] 2.1 `src/TrayApp.cs`: убрать сохранение значений по умолчанию из конструктора; поле `_firstRun`; `ApplyRequest.Adopt`, слияние запросов по design.md, решение 2; ветка `Keyboard.Read()` в `Apply`; снятие `_firstRun` в `FlushEdits`. Результат — сборка без предупреждений.
- [x] 2.2 `src/TrayApp.cs` `OnApplied` и `src/SettingsForm.cs` `Reload()`: перенос значений, сохранение файла, обновление окна, состояние из `_settings.Describe()`. Результат — сборка без предупреждений.

## 3. Разовые режимы

- [x] 3.1 `src/Program.cs`: без `settings.json` — строка журнала `--apply: NoSettings — нет settings.json, подсветку ещё не выбирали` (или `--check`) и код 3, клавиатура не трогается; обновить комментарий с ключами. Результат — сборка без предупреждений.

## 4. Проверка на клавиатуре (вместе с пользователем)

- [x] 4.1 Подготовка: `dotnet publish src -c Release -o publish`; «Выход» у установленного KbLight; скопировать `%LOCALAPPDATA%\KbLight\settings.json` в `settings.json.user-backup` и убрать оригинал. Результат — `settings.json` нет, бэкап есть.
- [x] 4.2 `python tools/kbtool.py setmode 1 50`, затем `publish\KbLight.exe --check` → код 3, в журнале `--check: NoSettings — …`, `kbtool.py read` — эффект 1, яркость 50.
- [x] 4.3 Запустить `publish\KbLight.exe`, флажок автозапуска не трогать → в журнале `запуск программы: AlreadySet — первый запуск, взял из клавиатуры: effect=1 brightness=50 …`; `kbtool.py read` — по-прежнему эффект 1, яркость 50; в новом `settings.json` — `"Effect": 1`, `"Brightness": 50` и `LastGoodBlock`; в окне выбран «Спектр», 50%.
- [x] 4.4 Без файла, клавиатура отключена: удалить созданный `settings.json`, отключить кабель, запустить `publish\KbLight.exe` → `запуск программы: NotFound — …` и файла нет; подключить кабель → `клавиатура подключена: AlreadySet — первый запуск, взял из клавиатуры: …`, файл создан.
- [x] 4.5 Вернуть всё: «Выход» у проверочной копии; вернуть `settings.json` из бэкапа и удалить бэкап; запустить установленный `KbLight.exe --tray`; `KbLight.exe --apply` → код 0; `kbtool.py read` — «Рябь от нажатий», 100; задача `KbLight` указывает на `%LOCALAPPDATA%\Programs\KbLight\KbLight.exe`.

## 5. Документы

- [x] 5.1 `docs/CHANGELOG.md`, раздел «Не выпущено»: «Изменено — первый запуск берёт подсветку из клавиатуры», «Добавлено — код возврата 3».
- [x] 5.2 `README.md`: первый запуск и код 3 у `--apply`/`--check`; `docs/ARCHITECTURE.md`: очередь записи (`Adopt`, `Keyboard.Read`) и §13 — что проверено.
