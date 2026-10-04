## Purpose

На каком языке KbLight показывает окно, меню, строку состояния и пишет журнал: русский для русской Windows, английский для остальных — и какой английский текст соответствует каждому русскому тексту, который цитируют другие спеки.

## ADDED Requirements

### Requirement: Выбор языка
KbLight SHALL выбирать язык один раз при запуске (трей и разовые режимы `--apply`, `--check`, `--autostart`): если задана переменная окружения `KBLIGHT_LANG` со значением `ru` или `en` (без учёта регистра) — этот язык; иначе русский, если язык интерфейса Windows — русский (любой регион), и английский — для любого другого языка Windows. Другие значения `KBLIGHT_LANG` SHALL NOT учитываться. Смена языка Windows при работающей программе SHALL действовать только после перезапуска KbLight.

#### Scenario: Русская Windows
- **WHEN** язык интерфейса Windows — русский, `KBLIGHT_LANG` не задана
- **THEN** окно называется «Подсветка клавиатуры», в журнале `запуск программы: Written — …` — как в остальных спеках

#### Scenario: Английская Windows
- **WHEN** язык интерфейса Windows — английский (или немецкий, или любой не русский), `KBLIGHT_LANG` не задана
- **THEN** окно называется «Keyboard Lighting», в журнале `program start: Written — was: effect=…; now: effect=…`

#### Scenario: Переменная перекрывает Windows
- **WHEN** язык Windows русский, KbLight запущен с `KBLIGHT_LANG=en`
- **THEN** окно, меню и журнал — по-английски

#### Scenario: Неизвестное значение
- **WHEN** `KBLIGHT_LANG=de`
- **THEN** язык выбирается по Windows, как без переменной

### Requirement: Один язык на всё
Окно, меню значка, подсказка значка, строка состояния, окно ошибки автозапуска, названия эффектов и строки журнала SHALL быть на выбранном языке. Русские тексты SHALL совпадать с теми, что цитируют остальные спеки; английские SHALL соответствовать им по таблицам требования «Английские тексты».

#### Scenario: Английский журнал и окно вместе
- **WHEN** выбран английский, пользователь выбрал в окне «Breathing»
- **THEN** строка состояния `Breathing, brightness 100% — applied at 14:05`, в журнале `settings changed: Written — …`

### Requirement: Что не зависит от языка
На обоих языках SHALL совпадать: формат времени строк журнала; статусы `Written`, `AlreadySet`, `NotFound`, `Unsupported`, `Failed`, `NoSettings`; вид строки попытки записи `<причина>: <статус> — <сообщение>`; состояние клавиатуры `effect=<код> brightness=<0–100> speed=<0–4> dir=<0|1> multicolor=<0|1> colorIndex=<номер> rgb=<RRGGBB>`; ключи командной строки и коды выхода; файлы `settings.json` и файлы моделей. Название модели (`name`) и название эффекта (`effects[].name`) из файла модели SHALL показываться как в файле, без перевода. Тексты ошибок, полученные от Windows и .NET (например, причина, по которой не прочитан файл), SHALL вставляться как есть.

#### Scenario: Файл модели с русским названием
- **WHEN** выбран английский, а в файле модели `"name": "Моя клавиатура"`
- **THEN** окно показывает «Keyboard: Моя клавиатура»

### Requirement: Английские тексты
Английские тексты SHALL быть такими (`<…>` — подставляемое значение, одинаковое на обоих языках).

Окно и меню:

| Русский | English |
|---|---|
| Подсветка клавиатуры (заголовок окна, первая строка подсказки значка, заголовок окна ошибки) | Keyboard Lighting |
| Клавиатура: <название> | Keyboard: <название> |
| не определена | unknown |
| Эффект | Effect |
| Яркость | Brightness |
| Скорость | Speed |
| <N> из 5 | <N> of 5 |
| Цвет | Color |
| Разноцветный | Multicolor |
| Обратное направление | Reverse direction |
| Запускать при входе в Windows (флажок окна и пункт меню) | Start with Windows |
| Журнал | Log |
| Модели | Models |
| Закрыть | Close |
| Настройки подсветки… | Lighting settings… |
| Применить сейчас | Apply now |
| Выход | Exit |
| Не удалось изменить автозапуск:<перевод строки><ошибка> | Could not change autostart:<перевод строки><ошибка> |

Строка состояния и подсказка значка:

| Русский | English |
|---|---|
| применяю… | applying… |
| <эффект>, яркость <N>% — применено в <ЧЧ:ММ> | <эффект>, brightness <N>% — applied at <ЧЧ:ММ> |
| Клавиатура не найдена. Подсветка применится, когда она подключится. | Keyboard not found. The lighting will be applied when it is connected. |
| Клавиатура <VID:PID> не знакома программе. Нужен файл модели — ссылка «Модели» в окне. | Keyboard <VID:PID> is unknown to the program. A model file is needed — see the "Models" link in the window. |
| Не удалось применить: <сообщение> | Could not apply: <сообщение> |

Причины в журнале:

| Русский | English |
|---|---|
| запуск программы | program start |
| клавиатура подключена | keyboard connected |
| выход из сна | resume from sleep |
| изменены настройки | settings changed |
| вручную | manual |

Сообщения журнала:

| Русский | English |
|---|---|
| запуск <путь> | start <путь> |
| выход | exit |
| клавиатура не найдена | keyboard not found |
| клавиатура <VID:PID> не знакома программе, нужен файл модели | keyboard <VID:PID> is unknown to the program, a model file is needed |
| клавиатура <VID:PID> не знакома программе, пропускаю | keyboard <VID:PID> is unknown to the program, skipping |
| не удалось открыть интерфейс клавиатуры | could not open the keyboard interface |
| уже стоит: <состояние> | already set: <состояние> |
| было: <состояние>; стало: <состояние> | was: <состояние>; now: <состояние> |
| первый запуск, взял из клавиатуры: <состояние> | first run, taken from the keyboard: <состояние> |
| клавиатура вернула неожиданный блок настроек, настройки не взяты | the keyboard returned an unexpected settings block, settings not taken |
| клавиатура вернула неожиданный блок настроек, запись отменена | the keyboard returned an unexpected settings block, write cancelled |
| клавиатура вернула повреждённый блок настроек, пишу поверх последнего исправного | the keyboard returned a damaged settings block, writing over the last good one |
| клавиатура не подтвердила запись | the keyboard did not confirm the write |
| клавиатура не ответила на команду <XX> | the keyboard did not answer command <XX> |
| команда <XX> не отправлена | command <XX> was not sent |
| не открылся <путь>: ошибка <N> | could not open <путь>: error <N> |
| неожиданные размеры отчётов <in>/<out> у <путь> | unexpected report sizes <in>/<out> at <путь> |
| <режим>: NoSettings — нет settings.json, подсветку ещё не выбирали | <режим>: NoSettings — no settings.json, the lighting has not been chosen yet |
| settings.json не прочитан (<ошибка>), беру значения по умолчанию | settings.json not read (<ошибка>), using defaults |
| настройки не сохранены: <ошибка> | settings not saved: <ошибка> |
| автозапуск включён | autostart on |
| автозапуск выключен | autostart off |
| автозапуск: <ошибка> | autostart: <ошибка> |
| RegisterDeviceNotification не сработал: <N> | RegisterDeviceNotification failed: <N> |
| модель <VID:PID> описана в нескольких файлах, беру <файл> | model <VID:PID> is described in several files, using <файл> |
| модель <VID:PID>: <файл> заменяет встроенное описание | model <VID:PID>: <файл> replaces the built-in description |
| файл модели <файл> не прочитан: <причина> | model file <файл> not read: <причина> |

Причины, по которым не прочитан файл модели:

| Русский | English |
|---|---|
| это не JSON | not JSON |
| файл пуст | the file is empty |
| нет поля name | no name field |
| <vid\|pid> должен быть четырьмя шестнадцатеричными цифрами | <vid\|pid> must be four hex digits |
| interface должен быть числом 0–255 | interface must be a number 0–255 |
| список effects пуст | the effects list is empty |
| у эффекта нет id 0–255 | an effect has no id 0–255 |
| эффект <код> повторяется | effect <код> is repeated |
| неизвестный параметр <параметр> | unknown option <параметр> |

Названия эффектов (код — русский — English; английские — как в sbarda): 1 «Спектр» — «Spectrum», 2 «Ступени» — «Staircase», 3 «Статичный цвет» — «Static», 4 «Дыхание» — «Breathing», 5 «Цветение» — «Hundred Flowers», 6 «Волна» — «Wave», 7 «Волна вверх-вниз» — «Up and down wave», 8 «Фонтан» — «Fountain», 9 «Млечный путь» — «Galaxy», 10 «Вращение» — «Rotation», 11 «Прилив» — «Tide», 12 «Морская волна» — «Sea wave», 13 «Рябь от нажатий» — «Ripple», 14 «Рябь на подсветке» — «Constant Ripple», 15 «Одна клавиша» — «Single point», 16 «Сетка» — «Grid», 17 «Пианино» — «Piano», 18 «Перелив» — «Flowing light», 19 «Дождь» — «Falling rain», 20 «Звёздный свет» — «Starlight», 21 «Фейерверк» — «Fireworks», 22 «Волновая полоса» — «Wave Band»; эффект без названия — «эффект <код>» — «effect <код>».

#### Scenario: Незнакомая клавиатура по-английски
- **WHEN** выбран английский, подключена только клавиатура `19F5:ABCD` без файла модели
- **THEN** в окне «Keyboard 19F5:ABCD is unknown to the program. A model file is needed — see the "Models" link in the window.», в журнале `program start: Unsupported — keyboard 19F5:ABCD is unknown to the program, a model file is needed`

#### Scenario: Ошибка в файле модели по-английски
- **WHEN** выбран английский, в файле модели `test.json` параметр `brighness`
- **THEN** в журнале `model file test.json not read: unknown option brighness`
