# Tasks

## 1. Тексты и выбор языка

- [x] 1.1 `src/Text.cs`: выбор языка (`KBLIGHT_LANG` ru/en без учёта регистра → иначе `CurrentUICulture`, `ru` → русский) и все тексты из таблиц спеки `interface-language` парами ru/en — проверка: `dotnet build src -c Release`, 0 ошибок и 0 предупреждений
- [x] 1.2 Названия эффектов: `Effect.KnownNames`/`KnownName` отдают русское или английское название по языку, `effect <код>` для неизвестного; `name` из файла модели не переводится — проверка: сборка; в окне с `KBLIGHT_LANG=en` список эффектов ZH99 HE по-английски («Spectrum» … «Wave Band»)

## 2. Перевод вызывающего кода

- [x] 2.1 `SettingsForm.cs`, `TrayApp.cs` (меню, причины, строка состояния, подсказка значка, окно ошибки автозапуска) берут тексты из `Text` — проверка: сборка; с `KBLIGHT_LANG=en` окно «Keyboard Lighting» со всеми подписями из спеки, меню значка по-английски, текст не обрезан
- [x] 2.2 `Keyboard.cs`, `KeyboardModel.cs`, `LightSettings.cs`, `Program.cs`, `SystemWatcher.cs` пишут в журнал и в результат тексты из `Text` — проверка: сборка; `grep -n '[А-Яа-яЁё]' src/*.cs` находит кириллицу только в `src/Text.cs`

## 3. Проверка на клавиатуре

- [x] 3.1 Русский режим без изменений: собранный `publish\KbLight.exe --check` → код 0, в журнале `--check: AlreadySet — уже стоит: …`, как до change
- [x] 3.2 Английский режим: `KBLIGHT_LANG=en`, подмена `python tools/kbtool.py setmode 1`, затем `KbLight.exe --check` → код 0, в журнале `--check: Written — was: effect=1 …; now: effect=13 …`
- [x] 3.3 Английский трей (установленный трей закрыт на время опыта): запуск с `KBLIGHT_LANG=en` → в журнале `start …` и `program start: AlreadySet — already set: …` или `Written — was: …`; правка яркости в окне → `settings changed: Written — …`, строка состояния `Ripple, brightness <N>% — applied at ЧЧ:ММ`; испорченный тестовый файл модели → `model file <файл> not read: unknown option …`; после опыта тестовый файл удалён, `settings.json` из резервной копии, установленный трей запущен, `KbLight.exe --apply` → код 0
- [x] 3.4 `KBLIGHT_LANG=de` при русской Windows → журнал по-русски

## 4. Документы

- [x] 4.1 `CLAUDE.md`: правило «тексты окна, меню и журнала — по-русски» заменить на «по-русски и по-английски, пары — в `src/Text.cs`, таблица — спека `interface-language`; новый текст добавляется в обе» — проверка: прочитать раздел «Правила»
- [x] 4.2 `docs/ARCHITECTURE.md`: `Text.cs` в списке файлов и раздел о выборе языка; `docs/ADDING-A-KEYBOARD.md`: строки журнала и окна по-английски с русскими в скобках; `README.md`: язык интерфейса и `KBLIGHT_LANG`; `docs/CHANGELOG.md`: запись в Unreleased — проверка: `openspec validate --all --strict` проходит, ссылки в документах указывают на существующие файлы
