## ADDED Requirements

### Requirement: Тексты выключателей и тихих часов
Новые тексты SHALL находиться в общем наборе переводов и выбираться по существующему правилу языка. Время SHALL отображаться в формате ЧЧ:ММ на обоих языках.

| Русский | English |
|---|---|
| Подсветка клавиш включена | Key lighting enabled |
| Light box включён | Light box enabled |
| Тихие часы | Quiet hours |
| Выключать подсветку по расписанию | Turn off lighting on a schedule |
| С | From |
| До | Until |
| В тихие часы обе подсветки выключены. После окончания вернутся прежние настройки. | Both lights are off during quiet hours. Previous settings return afterwards. |
| Тихие часы до <ЧЧ:ММ> | Quiet hours until <HH:mm> |
| Начало и окончание совпадают — тихие часы не действуют | Start and end are the same — quiet hours are inactive |
| Проверьте время тихих часов | Check the quiet hours times |
| неверное время тихих часов; расписание не действует | invalid quiet hours time; schedule is inactive |
| начало тихих часов | quiet hours started |
| окончание тихих часов | quiet hours ended |
| Тихие часы до <ЧЧ:ММ> — подсветка выключена — применено в <ЧЧ:ММ> | Quiet hours until <HH:mm> — lighting off — applied at <HH:mm> |
| Клавиши: <состояние>; Light box: <состояние> — применено в <ЧЧ:ММ> | Keys: <state>; Light box: <state> — applied at <HH:mm> |
| Клавиши: <состояние> — применено в <ЧЧ:ММ> | Keys: <state> — applied at <HH:mm> |
| выключены | off |
| выключен | off |
| яркость <N>% | brightness <N>% |

#### Scenario: Английские тихие часы
- **WHEN** выбран английский язык и в 01:00 успешно применено ночное выключение до 09:00
- **THEN** в окне «Quiet hours until 09:00», статус «Quiet hours until 09:00 — lighting off — applied at 01:00», причина записи в журнале «quiet hours started»

#### Scenario: Английский выключатель
- **WHEN** выбран английский язык и показана модель с Light box
- **THEN** видны «Key lighting enabled», «Light box enabled», «Quiet hours», «Turn off lighting on a schedule», «From» и «Until»
