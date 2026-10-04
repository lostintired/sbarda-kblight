## MODIFIED Requirements

### Requirement: Файл настроек
Настройки SHALL храниться в `%LOCALAPPDATA%\KbLight\settings.json` — JSON с отступами и полями `Effect`, `Brightness`, `Speed`, `ReverseDirection`, `Multicolor`, `ColorIndex`, `Rgb`, `LastGoodBlock`, `Model`, `CheckUpdates`. Файл SHALL записываться через временный `settings.json.tmp` с заменой, чтобы прерванная запись не оставила половину файла. `Model` SHALL хранить `VID:PID` последней клавиатуры, в которую подсветка записана или из которой взята (`"19F5:FB2A"`), и обновляться после такого результата, если изменился. `CheckUpdates` SHALL хранить, проверять ли обновления (update-check): `true` по умолчанию, в том числе когда поля нет в файле. Пока подсветка ещё не взята из клавиатуры при первом запуске, переключение флажка SHALL запоминаться в памяти и попадать в файл вместе с подсветкой, а не создавать файл раньше времени.

#### Scenario: Сохранение после правки
- **WHEN** пользователь выбрал в окне эффект «Волна»
- **THEN** в `settings.json` — `"Effect": 6`, а `settings.json.tmp` после сохранения не остаётся

#### Scenario: Модель запоминается
- **WHEN** трей записал подсветку в ZH99 HE, а в `settings.json` поля `Model` не было
- **THEN** в `settings.json` появилось `"Model": "19F5:FB2A"`

#### Scenario: Файл от прежней версии
- **WHEN** в `settings.json` нет поля `CheckUpdates`
- **THEN** проверка обновлений включена, при следующем сохранении в файле появляется `"CheckUpdates": true`
