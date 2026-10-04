## ADDED Requirements

### Requirement: Тексты версии и обновлений
Тексты версии и проверки обновлений SHALL быть на выбранном языке по таблице (`<…>` — подставляемое значение). Строка версии `KbLight <версия>` в окне, журнале и консоли SHALL быть одинаковой на обоих языках.

| Русский | English |
|---|---|
| Проверять обновления | Check for updates |
| доступна версия <N> (ссылка в окне) | version <N> is available |
| Доступна версия KbLight <N> (заголовок уведомления) | KbLight <N> is available |
| Нажмите, чтобы открыть страницу загрузки. | Click to open the download page. |
| доступна версия <N>: <адрес> (журнал) | version <N> is available: <адрес> |
| проверка обновлений не удалась: <причина> | update check failed: <причина> |

#### Scenario: Новая версия по-английски
- **WHEN** выбран английский, найдена версия 1.2.0
- **THEN** уведомление «KbLight 1.2.0 is available», в окне ссылка «version 1.2.0 is available», в журнале `version 1.2.0 is available: https://github.com/lostintired/sbarda-kblight/releases/tag/v1.2.0`
