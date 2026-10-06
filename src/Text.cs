using System.Globalization;

namespace KbLight;

/// <summary>
/// Every text the window, the tray and the log show, in Russian and English (spec interface-language).
/// The language is picked once per process: KBLIGHT_LANG=ru|en, otherwise the Windows display language.
/// </summary>
static class Text
{
    public static readonly bool Ru = PickRussian();

    static bool PickRussian()
    {
        string? lang = Environment.GetEnvironmentVariable("KBLIGHT_LANG")?.Trim().ToLowerInvariant();
        if (lang is "ru" or "en") return lang == "ru";
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
    }

    static string T(string ru, string en) => Ru ? ru : en;

    // Window and menu
    public static string AppTitle => T("Подсветка клавиатуры", "Keyboard Lighting");
    public static string KeyboardLine(string? name) => T("Клавиатура: ", "Keyboard: ") + (name ?? T("не определена", "unknown"));
    public static string EffectCaption => T("Эффект", "Effect");
    public static string BrightnessCaption => T("Яркость", "Brightness");
    public static string SpeedCaption => T("Скорость", "Speed");
    public static string SpeedValue(int n) => T($"{n} из 5", $"{n} of 5");
    public static string ColorCaption => T("Цвет", "Color");
    public static string Multicolor => T("Разноцветный", "Multicolor");
    public static string ReverseDirection => T("Обратное направление", "Reverse direction");
    public static string StartWithWindows => T("Запускать при входе в Windows", "Start with Windows");
    public static string LogLink => T("Журнал", "Log");
    public static string ModelsLink => T("Модели", "Models");
    public static string Close => T("Закрыть", "Close");
    public static string SettingsMenu => T("Настройки подсветки…", "Lighting settings…");
    public static string ApplyNowMenu => T("Применить сейчас", "Apply now");
    public static string ExitMenu => T("Выход", "Exit");
    public static string AutostartError(string error) => T("Не удалось изменить автозапуск:\n", "Could not change autostart:\n") + error;

    // Status line and tray tooltip
    public static string Applying => T("применяю…", "applying…");
    public static string Describe(string effect, int brightness) => T($"{effect}, яркость {brightness}%", $"{effect}, brightness {brightness}%");
    public static string AppliedAt(string what, DateTime time) => T($"{what} — применено в {time:HH:mm}", $"{what} — applied at {time:HH:mm}");
    public static string StatusNotFound => T("Клавиатура не найдена. Подсветка применится, когда она подключится.",
        "Keyboard not found. The lighting will be applied when it is connected.");
    public static string StatusUnsupported(string? id) => T($"Клавиатура {id} не знакома программе. Нужен файл модели — ссылка «Модели» в окне.",
        $"Keyboard {id} is unknown to the program. A model file is needed — see the \"Models\" link in the window.");
    public static string StatusFailed(string message) => T("Не удалось применить: ", "Could not apply: ") + message;

    // Reasons in the log
    public static string ReasonStartup => T("запуск программы", "program start");
    public static string ReasonArrived => T("клавиатура подключена", "keyboard connected");
    public static string ReasonResumed => T("выход из сна", "resume from sleep");
    public static string ReasonEdited => T("изменены настройки", "settings changed");
    public static string ReasonManual => T("вручную", "manual");

    // Log messages
    public static string LogStart(string? path) => T("запуск ", "start ") + path;
    public static string LogExit => T("выход", "exit");
    public static string KeyboardNotFound => T("клавиатура не найдена", "keyboard not found");
    public static string KeyboardUnknown(string id) => T($"клавиатура {id} не знакома программе, нужен файл модели",
        $"keyboard {id} is unknown to the program, a model file is needed");
    public static string KeyboardSkipped(string id) => T($"клавиатура {id} не знакома программе, пропускаю",
        $"keyboard {id} is unknown to the program, skipping");
    public static string OpenFailed => T("не удалось открыть интерфейс клавиатуры", "could not open the keyboard interface");
    public static string AlreadySet(string state) => T("уже стоит: ", "already set: ") + state;
    public static string Written(string before, string after) => T($"было: {before}; стало: {after}", $"was: {before}; now: {after}");
    public static string Adopted(string state) => T("первый запуск, взял из клавиатуры: ", "first run, taken from the keyboard: ") + state;
    public static string BadBlockNotTaken => T("клавиатура вернула неожиданный блок настроек, настройки не взяты",
        "the keyboard returned an unexpected settings block, settings not taken");
    public static string BadBlockCancelled => T("клавиатура вернула неожиданный блок настроек, запись отменена",
        "the keyboard returned an unexpected settings block, write cancelled");
    public static string DamagedBlock => T("клавиатура вернула повреждённый блок настроек, пишу поверх последнего исправного",
        "the keyboard returned a damaged settings block, writing over the last good one");
    public static string NotConfirmed => T("клавиатура не подтвердила запись", "the keyboard did not confirm the write");
    public static string NoAnswer(byte cmd) => T($"клавиатура не ответила на команду {cmd:X2}", $"the keyboard did not answer command {cmd:X2}");
    public static string NotSent(byte cmd) => T($"команда {cmd:X2} не отправлена", $"command {cmd:X2} was not sent");
    public static string NotOpened(string path, int error) => T($"не открылся {path}: ошибка {error}", $"could not open {path}: error {error}");
    public static string BadReportSizes(int input, int output, string path) =>
        T($"неожиданные размеры отчётов {input}/{output} у {path}", $"unexpected report sizes {input}/{output} at {path}");
    public static string NoSettings => T("нет settings.json, подсветку ещё не выбирали", "no settings.json, the lighting has not been chosen yet");
    public static string SettingsNotRead(string error) => T($"settings.json не прочитан ({error}), беру значения по умолчанию",
        $"settings.json not read ({error}), using defaults");
    public static string SettingsNotSaved(string error) => T("настройки не сохранены: ", "settings not saved: ") + error;
    public static string AutostartOn => T("автозапуск включён", "autostart on");
    public static string AutostartOff => T("автозапуск выключен", "autostart off");
    public static string AutostartFailed(string error) => T("автозапуск: ", "autostart: ") + error;
    public static string NotificationFailed(int error) => T($"RegisterDeviceNotification не сработал: {error}", $"RegisterDeviceNotification failed: {error}");
    public static string ModelDuplicate(string id, string? file) => T($"модель {id} описана в нескольких файлах, беру {file}",
        $"model {id} is described in several files, using {file}");
    public static string ModelReplaces(string id, string? file) => T($"модель {id}: {file} заменяет встроенное описание",
        $"model {id}: {file} replaces the built-in description");
    public static string ModelNotRead(string file, string reason) => T($"файл модели {file} не прочитан: {reason}", $"model file {file} not read: {reason}");

    // Why a model file was not read
    public static string NotJson => T("это не JSON", "not JSON");
    public static string FileEmpty => T("файл пуст", "the file is empty");
    public static string NoName => T("нет поля name", "no name field");
    public static string BadId(string field) => T($"{field} должен быть четырьмя шестнадцатеричными цифрами", $"{field} must be four hex digits");
    public static string BadInterface => T("interface должен быть числом 0–255", "interface must be a number 0–255");
    public static string NoEffects => T("список effects пуст", "the effects list is empty");
    public static string BadEffectId => T("у эффекта нет id 0–255", "an effect has no id 0–255");
    public static string EffectRepeated(int id) => T($"эффект {id} повторяется", $"effect {id} is repeated");
    public static string UnknownOption(string option) => T($"неизвестный параметр {option}", $"unknown option {option}");

    // Version and update check
    public static string CheckUpdates => T("Проверять обновления", "Check for updates");
    public static string UpdateLink(Version v) => T($"доступна версия {v}", $"version {v} is available");
    public static string UpdateTitle(Version v) => T($"Доступна версия KbLight {v}", $"KbLight {v} is available");
    public static string UpdateBody => T("Нажмите, чтобы открыть страницу загрузки.", "Click to open the download page.");
    public static string UpdateFound(Version v, string url) => T($"доступна версия {v}: {url}", $"version {v} is available: {url}");
    public static string UpdateFailed(string reason) => T("проверка обновлений не удалась: ", "update check failed: ") + reason;

    // Effect names by code: Russian as in KbLight 1.0, English as sbarda shows them (language\1033.lan, docs/PROTOCOL.md §5).
    static readonly Dictionary<int, (string Ru, string En)> EffectNames = new()
    {
        [1] = ("Спектр", "Spectrum"), [2] = ("Ступени", "Staircase"), [3] = ("Статичный цвет", "Static"),
        [4] = ("Дыхание", "Breathing"), [5] = ("Цветение", "Hundred Flowers"), [6] = ("Волна", "Wave"),
        [7] = ("Волна вверх-вниз", "Up and down wave"), [8] = ("Фонтан", "Fountain"), [9] = ("Млечный путь", "Galaxy"),
        [10] = ("Вращение", "Rotation"), [11] = ("Прилив", "Tide"), [12] = ("Морская волна", "Sea wave"),
        [13] = ("Рябь от нажатий", "Ripple"), [14] = ("Рябь на подсветке", "Constant Ripple"), [15] = ("Одна клавиша", "Single point"),
        [16] = ("Сетка", "Grid"), [17] = ("Пианино", "Piano"), [18] = ("Перелив", "Flowing light"), [19] = ("Дождь", "Falling rain"),
        [20] = ("Звёздный свет", "Starlight"), [21] = ("Фейерверк", "Fireworks"), [22] = ("Волновая полоса", "Wave Band"),
    };

    // Light box group (spec interface-language, "Тексты light box"); the group title is the same in both languages.
    public static string LightBoxCaption => "Light box";
    public static string LightBoxModeCaption => T("Режим", "Mode");

    public static string LightBoxModeName(int id) => id switch
    {
        0 => T("Плывущие линии", "Flowing lines"),
        1 => T("Мигание", "Flashing"),
        2 => T("Ровный цвет", "Steady color"),
        3 => T("Дыхание", "Breathing"),
        _ => T("Выключен", "Off"),
    };

    public static string EffectName(int id) =>
        EffectNames.TryGetValue(id, out var name) ? T(name.Ru, name.En) : T($"эффект {id}", $"effect {id}");
}
