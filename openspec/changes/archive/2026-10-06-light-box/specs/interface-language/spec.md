## MODIFIED Requirements

### Requirement: Что не зависит от языка
На обоих языках SHALL совпадать: формат времени строк журнала; статусы `Written`, `AlreadySet`, `NotFound`, `Unsupported`, `Failed`, `NoSettings`; вид строки попытки записи `<причина>: <статус> — <сообщение>`; состояние клавиатуры `effect=<код> brightness=<0–100> speed=<0–4> dir=<0|1> multicolor=<0|1> colorIndex=<номер> rgb=<RRGGBB>` и его продолжение ` | box mode=<0–255> brightness=<0–255> speed=<число> multicolor=<0–255> rgb=<RRGGBB>`; поля `LightBox`, `lightBox` и название группы «Light box»; ключи командной строки и коды выхода; файлы `settings.json` и файлы моделей. Название модели (`name`) и название эффекта (`effects[].name`) из файла модели SHALL показываться как в файле, без перевода. Тексты ошибок, полученные от Windows и .NET (например, причина, по которой не прочитан файл), SHALL вставляться как есть.

#### Scenario: Файл модели с русским названием
- **WHEN** выбран английский, а в файле модели `"name": "Моя клавиатура"`
- **THEN** окно показывает «Keyboard: Моя клавиатура»

## ADDED Requirements

### Requirement: Тексты light box
Тексты группы «Light box» SHALL быть на выбранном языке по таблице; подписи «Яркость», «Скорость», «<N> из 5», «Цвет» и «Разноцветный» в группе SHALL быть те же, что у основной подсветки (требование «Английские тексты»).

| Русский | English |
|---|---|
| Light box (заголовок группы) | Light box |
| Режим | Mode |
| Плывущие линии | Flowing lines |
| Мигание | Flashing |
| Ровный цвет | Steady color |
| Дыхание | Breathing |
| Выключен | Off |

#### Scenario: Light box по-английски
- **WHEN** выбран английский и пользователь раскрыл список «Mode» в группе «Light box»
- **THEN** в нём «Flowing lines», «Flashing», «Steady color», «Breathing», «Off»
