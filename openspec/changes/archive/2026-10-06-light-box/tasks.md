# Tasks

## 1. Модель и настройки

- [x] 1.1 Поле `lightBox` в `KeyboardModel` (разбор в `KeyboardModels.Parse`, по умолчанию `false`), `"lightBox": true` в `src/models/zorner-zh99-he.json`; `docs/ADDING-A-KEYBOARD.md` — описание поля — проверка: `dotnet build src -c Release` без ошибок и предупреждений
- [x] 1.2 `LightBoxSettings` (неизменяемый `record`: `Mode`, `Brightness`, `Speed`, `Multicolor`, `Rgb`; `FromBlock`, `GetColor`), `LightSettings.LightBox`, таблица `LightBoxMode` с `EffectOptions`; `SetFromBlock` берёт light box для модели с ним — проверка: сборка без предупреждений; `settings.json` без `LightBox` читается (`LightBox == null`), запись после правки даёт объект с пятью полями

## 2. Запись в клавиатуру

- [x] 2.1 `LightState.From(settings, model)` с частью light box (байты 24–26, 28–31, байт 28 по режиму, ограничения диапазонов), `WriteTo`/`Matches` с ней, `Describe(block, model)` с ` | box …`; `Keyboard.Apply(LightSettings …)`, `ForEachInterface` передаёт модель действию; `TrayApp.Apply` и `Program.ApplyOnce` передают настройки — проверка: сборка без предупреждений; с `settings.json` без `LightBox` `publish\KbLight.exe --check` (трей закрыт) даёт код 0 и строку `--check: AlreadySet — уже стоит: … | box mode=0 brightness=100 speed=2 multicolor=1 rgb=FF0000`, `kbtool.py read` — байты 24–31 не изменились
- [x] 2.2 Сверка и запись light box: в копию `settings.json` вписать `"LightBox": { "Mode": 3, "Brightness": 100, "Speed": 4, "Multicolor": false, "Rgb": "#FF8000" }`, перед этим `kbtool.py setbytes 27=01` — проверка: `--check` → `Written — было: … | box mode=0 …; стало: … | box mode=3 brightness=100 speed=4 multicolor=0 rgb=FF8000`, `kbtool.py read` — байты 24–31 `03 64 00 01 00 ff 80 00`, байты 0–7 и 17–23 как до записи; повторный `--check` → `AlreadySet`; глазами — быстрое оранжевое дыхание (скорость 4 — «5 из 5»)
- [x] 2.3 `docs/ADR.md` — ADR-003 дополнен байтами light box; `docs/PROTOCOL.md` — строка про KbLight в §4 (какие байты пишет) — проверка: `openspec validate --all --strict` проходит

## 3. Трей: взять light box из клавиатуры

- [x] 3.1 `TrayApp.OnApplied`: при `LightBox == null`, модели с light box и блоке в результате — `LightBoxSettings.FromBlock`, сохранение, `_form?.Reload()`; первый запуск берёт light box вместе с подсветкой — проверка: `settings.json` без `LightBox`, light box в клавиатуре заводской, запуск `publish\KbLight.exe --tray` (установленный трей закрыт) → в `settings.json` `"LightBox": { "Mode": 0, "Brightness": 100, "Speed": 2, "Multicolor": true, "Rgb": "#FF0000" }`, `kbtool.py read` — байты 24–31 `00 64 02 00 01 ff 00 00`
- [x] 3.2 Первый запуск без `settings.json` (резервная копия файла пользователя) — проверка: в журнале `запуск программы: AlreadySet — первый запуск, взял из клавиатуры: … | box mode=0 …`, `settings.json` содержит `LightBox`, команды записи не было (байты блока до и после совпадают)

## 4. Окно

- [x] 4.1 Тексты в `Text.cs` по таблице «Тексты light box» спеки `interface-language` (заголовок, «Режим», названия пяти режимов) — проверка: сборка без предупреждений
- [x] 4.2 Группа «Light box» в `SettingsForm`: заголовок, «Режим», «Яркость», «Скорость», «Цвет» + «Разноцветный»; доступность по режиму; первая правка создаёт `LightBox`; видимость по `lightBox` модели и обновление в `Reload` — проверка: окно ZH99 HE показывает группу под «Обратное направление»; «Мигание» — доступны только яркость и скорость, «Выключен» — ничего; выбор «Ровный цвет» и синего — в журнале `изменены настройки: Written — … | box mode=2 … rgb=0000FF`, light box синий
- [x] 4.3 Модель без light box: свой файл `models\test.json` для `19F5:FB2A` без `lightBox` — проверка: окно без группы, `--apply` → `kbtool.py read` байты 24–31 как до записи, в журнале нет `| box`; после проверки файл удалён
- [x] 4.4 Английский: `KBLIGHT_LANG=en` — проверка: заголовок «Light box», «Mode», режимы «Flowing lines», «Flashing», «Steady color», «Breathing», «Off»

## 5. Сценарии и восстановление

- [x] 5.1 Отключение питания: в окне ровный оранжевый light box, кабель вынуть и вставить — проверка: в журнале `клавиатура подключена: Written — было: … | box mode=0 …; стало: … | box mode=2 … rgb=FF8000`, light box оранжевый
- [x] 5.2 Сценарии 1–4 из `CLAUDE.md` («Проверка») с новой сборкой — проверка: ожидаемые строки журнала, с частью ` | box …`
- [x] 5.3 После опытов: `settings.json` пользователя из резервной копии (с `LightBox`, если пользователь выбрал свой light box, — спросить), тестовые файлы удалены, установленный трей запущен, `KbLight.exe --apply` → код 0

## 6. Документы и подготовка версии 1.3.0

- [x] 6.1 `README.md` и `README.ru.md`: light box в окне, что пишется при каких событиях, клавиши Fn и KbLight — проверка: оба README говорят одно и то же
- [x] 6.2 `docs/ARCHITECTURE.md` — light box в `LightState`, взятие из клавиатуры в трее, путь модели в запись; ручные проверки §13 — что проверено — проверка: разделы совпадают с кодом
- [x] 6.3 Версия: `<Version>1.3.0</Version>` в `src/KbLight.csproj`, `docs/CHANGELOG.md` — `## [1.3.0] — <дата>`, `.github/release-notes/v1.3.0.md` по-английски — проверка: `dotnet publish src -c Release -o publish`, `publish\KbLight.exe --version` → `KbLight 1.3.0`, `openspec validate --all --strict` проходит; тег и релиз не создаются без слова пользователя
