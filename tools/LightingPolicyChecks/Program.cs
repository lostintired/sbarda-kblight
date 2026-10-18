using System.Reflection;
using System.Text.Json;
using KbLight;

static class Checks
{
    static int _passed;
    static readonly DateTime Day = new(2026, 10, 18, 12, 0, 0);
    static readonly DateTime Night = new(2026, 10, 18, 1, 0, 0);

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Contains("--preview"))
            {
                Application.SetColorMode(SystemColorMode.Dark);
                var preview = Profile();
                preview.Multicolor = false;
                preview.Rgb = "#00EAFF";
                preview.QuietHours = new();
                using var window = new SettingsForm(preview, true);
                window.Text += " — предпросмотр";
                window.SetStatus("Предпросмотр: изменения в этом окне не затрагивают клавиатуру и ваши настройки.");
                Application.Run(window);
                return 0;
            }
            CheckPolicy();
            CheckSettings();
            CheckBlocks();
            if (args.Contains("--ui")) CheckWindow(args);
            if (args.Contains("--device")) CheckDevice();
            if (args.Contains("--tray")) CheckTray();
            if (args.Contains("--wait")) CheckWait();
            Console.WriteLine($"Проверок пройдено: {_passed}; ошибок: 0.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Проверка не пройдена после {_passed} успешных: {e}");
            return 1;
        }
    }

    static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException(name);
        _passed++;
    }

    static LightSettings Profile() => new()
    {
        Model = "19F5:FB2A", Effect = 6, Brightness = 65, Speed = 2, Rgb = "#123456",
        LightBox = new() { Mode = 3, Brightness = 35, Rgb = "#FF8000", Multicolor = false },
        QuietHours = new() { Enabled = true }, CheckUpdates = false,
    };

    static void CheckPolicy()
    {
        var s = Profile();
        foreach (var (time, active) in new[] { ("22:59:59", false), ("23:00:00", true), ("23:59:59", true),
            ("00:00:00", true), ("08:59:59", true), ("09:00:00", false), ("12:00:00", false) })
            Check("Граница ночного интервала " + time, LightingPolicy.At(s, DateTime.Parse("2026-10-18T" + time)).Schedule.Active == active);
        s.QuietHours = new() { Enabled = true, Start = "13:00", End = "15:00" };
        foreach (var (hour, active) in new[] { (12, false), (13, true), (14, true), (15, false), (23, false) })
            Check("Дневной интервал " + hour, LightingPolicy.At(s, Day.Date.AddHours(hour)).Schedule.Active == active);
        s.QuietHours = new() { Enabled = false };
        Check("Выключенное расписание", !LightingPolicy.At(s, Night).Schedule.Active);
        s.QuietHours = new() { Enabled = true, Start = "09:00", End = "09:00" };
        Check("Равные времена", !LightingPolicy.At(s, Night).Schedule.Active && LightingPolicy.At(s, Night).Schedule.SameTime);
        foreach (string? bad in new[] { "24:00", "25:10", "9:00", "09:0", "09:00:00", "abc", "", null })
        {
            s.QuietHours = new() { Enabled = true, Start = bad };
            Check("Неверное начало " + bad, LightingPolicy.At(s, Night).Schedule is { IsValid: false, Active: false });
            s.QuietHours = new() { Enabled = true, End = bad };
            Check("Неверный конец " + bad, LightingPolicy.At(s, Night).Schedule is { IsValid: false, Active: false });
        }
        Check("Отсутствующее расписание", QuietHoursState.At(null, Night) is { IsValid: true, Active: false });
        for (int bits = 0; bits < 8; bits++)
        {
            s = Profile();
            s.Enabled = (bits & 1) != 0;
            s.LightBoxEnabled = (bits & 2) != 0;
            var policy = LightingPolicy.At(s, (bits & 4) != 0 ? Night : Day);
            Check("Сочетание выключателей " + bits,
                policy.KeysOff == (!s.Enabled || (bits & 4) != 0) && policy.BoxOff == (!s.LightBoxEnabled || (bits & 4) != 0));
        }
        s = Profile();
        string before = JsonSerializer.Serialize(s);
        _ = LightingPolicy.At(s, Night);
        _ = LightingPolicy.At(s, Day);
        Check("Расчёт не меняет профиль", before == JsonSerializer.Serialize(s));
        Check("Перевод времени вперёд", !LightingPolicy.At(s, Day.Date.AddHours(22)).Schedule.Active
            && LightingPolicy.At(s, Day.Date.AddHours(23.5)).Schedule.Active);
        Check("Перевод времени назад", !LightingPolicy.At(s, Day).Schedule.Active && LightingPolicy.At(s, Day.AddHours(-4)).Schedule.Active);
    }

    static void CheckSettings()
    {
        File.WriteAllText(AppFiles.Settings, """{"Brightness":65,"LightBox":{"Brightness":35}}""");
        var s = LightSettings.Load();
        Check("Старый профиль", s.Enabled && s.LightBoxEnabled && !s.QuietHours.Enabled && s.Brightness == 65 && s.LightBox?.Brightness == 35);
        s.Save();
        Check("Нет временного файла", !File.Exists(AppFiles.Settings + ".tmp"));
        var copy = LightSettings.Load();
        Check("Сохранение яркостей", copy.Brightness == 65 && copy.LightBox?.Brightness == 35);
        Check("Исходное время", copy.QuietHours.Start == "23:00" && copy.QuietHours.End == "09:00");
        File.WriteAllText(AppFiles.Settings, """{"Brightness":65,"QuietHours":null}""");
        Check("Null расписание из JSON", !LightSettings.Load().QuietHours.Enabled);
        File.WriteAllText(AppFiles.Settings, """{"Brightness":65,"QuietHours":{"Enabled":true,"Start":"25:10"}}""");
        s = LightSettings.Load();
        Check("Ошибка времени не сбрасывает яркость", s.Brightness == 65);
        Check("Одна строка об ошибке", File.ReadAllLines(AppFiles.Log).Count(l => l == KbLight.Text.QuietHoursInvalidLog) == 1);
        for (int i = 0; i < 5; i++) _ = LightingPolicy.At(s, Night);
        Check("Расчёт не повторяет ошибку в журнале", File.ReadAllLines(AppFiles.Log).Length == 1);
        var snapshot = s.Clone();
        s.QuietHours = s.QuietHours with { Start = "23:00" };
        Check("Снимок расписания неизменяемый", snapshot.QuietHours.Start == "25:10");
    }

    static void CheckBlocks()
    {
        var model = KeyboardModels.Find("19F5:FB2A")!;
        Check("Встроенная модель найдена", model is { LightBox: true });
        var s = Profile();
        var baseline = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();
        baseline[2] = 0xAA; baseline[3] = 0xBB;
        string original = JsonSerializer.Serialize(s);
        foreach (var now in new[] { Day, Night })
        {
            var policy = LightingPolicy.At(s, now);
            var state = LightState.From(s, model, policy);
            var block = baseline.ToArray();
            state.WriteTo(block);
            Check("Отправляемые яркости " + now.Hour, block[9] == (policy.KeysOff ? 0 : 65) && block[25] == (policy.BoxOff ? 0 : 35));
            Check("Сверка отправленного блока", state.Matches(block));
            for (int i = 0; i < 32; i++)
                if (!(i >= 8 && i <= 16 || i >= 24 && i != 27))
                    Check("Сохранён чужой байт " + i, block[i] == baseline[i]);
            var result = new ApplyResult(ApplyStatus.Written, "", block, model, Policy: policy);
            string description = result.DescribeApplied(s);
            Check("Статус фактического состояния", policy.Schedule.Active ? description == KbLight.Text.QuietHoursApplied(new(9, 0)) : description == s.Describe(model));
        }
        Check("Преобразование не меняет профиль", original == JsonSerializer.Serialize(s));
        s.Enabled = false;
        var manual = baseline.ToArray();
        LightState.From(s, model, LightingPolicy.At(s, Day)).WriteTo(manual);
        Check("Выключена только клавиатура", manual[9] == 0 && manual[25] == 35);
        Check("Статус ручного выключения", new ApplyResult(ApplyStatus.Written, "", manual, model).DescribeApplied(s) == KbLight.Text.LightsState(0, 35));
        s.Enabled = true; s.LightBoxEnabled = false;
        LightState.From(s, model, LightingPolicy.At(s, Day)).WriteTo(manual);
        Check("Выключен только Light box", manual[9] == 65 && manual[25] == 0);
        s.LightBox = s.LightBox! with { Mode = 4 };
        s.LightBoxEnabled = true;
        LightState.From(s, model, LightingPolicy.At(s, Day)).WriteTo(manual);
        Check("Старый режим Выключен сохранён", manual[24] == 4 && s.LightBox.Mode == 4);
        s.LightBox = null;
        var unknown = baseline.ToArray();
        LightState.From(s, model, LightingPolicy.At(s, Night)).WriteTo(unknown);
        Check("Неизвестный Light box не меняется", unknown.AsSpan(24).SequenceEqual(baseline.AsSpan(24)));
        s.LightBox = new();
        var noBox = baseline.ToArray();
        LightState.From(s, model with { LightBox = false }, LightingPolicy.At(s, Night)).WriteTo(noBox);
        Check("Модель без Light box", noBox.AsSpan(24).SequenceEqual(baseline.AsSpan(24)));
    }

    static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    static void CheckWindow(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetColorMode(args.Contains("--dark") ? SystemColorMode.Dark : SystemColorMode.Classic);
        var settings = Profile();
        settings.QuietHours = new();
        using var form = new SettingsForm(settings, false);
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
        int changes = 0;
        form.SettingsChanged += () => changes++;
        form.Reload();
        Check("Загрузка окна без правок", changes == 0);
        Check("Время по умолчанию", Field<LightingTimePicker>(form, "_quietStart").Value.Hour == 23 && Field<LightingTimePicker>(form, "_quietEnd").Value.Hour == 9);
        Check("Поля времени отключены", !Field<LightingTimePicker>(form, "_quietStart").Enabled);
        Field<CheckBox>(form, "_enabled").Checked = false;
        Check("Ручное выключение в окне", !settings.Enabled && Field<LightingSlider>(form, "_brightness").Enabled && Field<LightingSlider>(form, "_brightness").Value == 65);
        Field<CheckBox>(form, "_enabled").Checked = true;
        Field<CheckBox>(form, "_boxEnabled").Checked = false;
        Check("Выключение Light box в окне", !settings.LightBoxEnabled && Field<ComboBox>(form, "_boxMode").Enabled && settings.LightBox?.Brightness == 35);
        Field<CheckBox>(form, "_boxEnabled").Checked = true;
        Field<CheckBox>(form, "_quietEnabled").Checked = true;
        Check("Включение расписания в окне", settings.QuietHours.Enabled && Field<LightingTimePicker>(form, "_quietStart").Enabled);
        form.SetQuietHoursState(QuietHoursState.At(settings.QuietHours, Night));
        Check("Ночная подпись", Field<Label>(form, "_quietState").Text == KbLight.Text.QuietHoursActive(new(9, 0)));
        Check("Ночью профиль доступен", Field<LightingSlider>(form, "_brightness").Enabled);
        settings.QuietHours = settings.QuietHours with { Start = "25:00" };
        form.Reload();
        Check("Ошибка времени видна", Field<Label>(form, "_quietState").Text == KbLight.Text.QuietHoursInvalid);
        settings.QuietHours = new() { Enabled = true, Start = "09:00", End = "09:00" };
        form.Reload();
        Check("Равные времена видны", Field<Label>(form, "_quietState").Text == KbLight.Text.QuietHoursSameTime);
        Field<LightingTimePicker>(form, "_quietStart").Value = DateTime.Today.AddHours(23);
        Check("Правка времени сохраняется в профиль", settings.QuietHours.Start == "23:00");
        settings.QuietHours = new() { Enabled = true };
        form.Reload();
        CheckRedesign(form, settings, () => changes);
        form.SetStatus(KbLight.Text.AppliedAt(KbLight.Text.LightsState(settings.Brightness, settings.LightBox?.Brightness), Day));
        int scaleArg = Array.IndexOf(args, "--scale");
        if (scaleArg >= 0)
        {
            float scale = float.Parse(args[scaleArg + 1], System.Globalization.CultureInfo.InvariantCulture);
            form.Scale(new SizeF(scale, scale));
            typeof(SettingsForm).GetMethod("FitWorkingArea", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null);
            Application.DoEvents();
        }
        int smallArg = Array.IndexOf(args, "--small");
        if (smallArg >= 0) { form.Size = new Size(800, 560); Application.DoEvents(); }
        Check("Окно помещается по высоте", form.Height <= Screen.FromControl(form).WorkingArea.Height);
        Check("Окно помещается по ширине", form.Width <= Screen.FromControl(form).WorkingArea.Width);
        var status = Field<Label>(form, "_status");
        Check("Состояние закреплено", form.ClientRectangle.Contains(form.RectangleToClient(status.RectangleToScreen(status.ClientRectangle))));
        var direction = Field<CheckBox>(form, "_reverse");
        Check("Панель не обрезает направление", direction.Parent!.ClientRectangle.Contains(direction.Bounds));
        var body = Field<Panel>(form, "_body");
        foreach (Control control in new Control[] { Field<LightingColorPicker>(form, "_color"), Field<LightingColorPicker>(form, "_boxColor"), Field<CheckBox>(form, "_reverse"), Field<Label>(form, "_version") })
        {
            body.ScrollControlIntoView(control); Application.DoEvents();
            var r = body.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
            Check("Поле достижимо прокруткой: " + control.GetType().Name, r.Left >= 0 && r.Top >= 0 && r.Right <= body.ClientSize.Width && r.Bottom <= body.ClientSize.Height);
        }
        body.AutoScrollPosition = Point.Empty;

        Console.WriteLine($"Окно: DPI={form.DeviceDpi}; размер={form.Width}x{form.Height}; рабочая область={Screen.FromControl(form).WorkingArea.Size}.");
        var close = AllControls(form).OfType<Button>().Single(b => b.Text == KbLight.Text.Close);
        form.ScrollControlIntoView(close);
        Application.DoEvents();
        var closeBounds = form.RectangleToClient(close.RectangleToScreen(close.ClientRectangle));
        Check("Кнопка Закрыть доступна", form.ClientRectangle.Contains(closeBounds));
        int imageArg = Array.IndexOf(args, "--image");
        if (imageArg >= 0)
        {
            string bottomImage = Path.Combine(Path.GetDirectoryName(args[imageArg + 1])!,
                Path.GetFileNameWithoutExtension(args[imageArg + 1]) + "-bottom.png");
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(bottomImage);
        }
        body.AutoScrollPosition = Point.Empty;
        Application.DoEvents();
        if (imageArg >= 0)
        {
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(args[imageArg + 1]);
        }
        Directory.CreateDirectory(KeyboardModels.Folder);
        File.WriteAllText(Path.Combine(KeyboardModels.Folder, "no-box.json"),
            """{"name":"Без Light box","vid":"19F5","pid":"FB2A","interface":1,"effects":[{"id":6,"options":["brightness"]}]}""");
        form.Reload();
        Check("Выключатель Light box скрыт без поддержки модели", !Field<CheckBox>(form, "_boxEnabled").Visible);
        Check("Колонка Light box освобождена", Field<TableLayoutPanel>(form, "_columns").ColumnStyles[1].Width == 0);
        form.Close();
        CheckCloseModes();
    }

    static void CheckRedesign(SettingsForm form, LightSettings settings, Func<int> count)
    {
        var keys = Field<LightingColorPicker>(form, "_color");
        var box = Field<LightingColorPicker>(form, "_boxColor");
        var hex = Field<TextBox>(keys, "_hex");
        var effect = Field<ComboBox>(form, "_effect");
        var mode = Field<ComboBox>(form, "_boxMode");
        Check("Все эффекты модели сохранены", effect.Items.Count == 20);
        int edits = count();
        settings.Multicolor = false;
        form.Reload();
        Check("Reload палитры без событий", count() == edits);
        string boxBefore = settings.LightBox!.Rgb;
        hex.Text = "0000ff";
        Check("Корректный HEX нормализован", settings.Rgb == "#0000FF" && hex.Text == "#0000FF");
        Check("Изменение цвета ровно одно", count() == edits + 1);
        Check("Цвета зон независимы", settings.LightBox.Rgb == boxBefore);
        foreach (string invalid in new[] { "#12", "#ZZZZZZ", "", "#-12345", "##12345" })
        {
            edits = count(); hex.Text = invalid;
            Check("Неверный HEX не сохраняется: " + invalid, settings.Rgb == "#0000FF" && count() == edits);
            Check("Ошибка HEX видна: " + invalid, Field<Label>(keys, "_error").Text == KbLight.Text.HexInvalid);
            typeof(Control).GetMethod("OnLeave", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(hex, [EventArgs.Empty]);
            Check("Возврат правильного HEX: " + invalid, hex.Text == "#0000FF" && count() == edits);
        }
        foreach (string color in new[] { "#000000", "#FFFFFF", "#808080", "#FF0000", "#00FF00", "#0000FF" })
        {
            hex.Text = color;
            Check("Точный цвет " + color, settings.Rgb == color && LightSettings.FormatColor(keys.SelectedColor) == color);
        }
        Field<CheckBox>(form, "_enabled").Checked = false;
        Field<LightingSlider>(form, "_brightness").Value = 40;
        Check("Правка выключенной зоны", !settings.Enabled && settings.Brightness == 40 && LightingPolicy.At(settings, Day).KeysOff);
        Check("Выключенная зона не блокирует палитру", keys.Enabled);
        Field<CheckBox>(form, "_enabled").Checked = true;
        Check("Возврат отредактированного профиля", settings.Brightness == 40 && !LightingPolicy.At(settings, Day).KeysOff);
        foreach (Effect e in effect.Items)
        {
            effect.SelectedItem = e;
            var options = e.Options;
            Check("Яркость эффекта " + e.Id, Field<LightingSlider>(form, "_brightness").Enabled == options.HasFlag(EffectOptions.Brightness));
            Check("Скорость эффекта " + e.Id, Field<LightingSlider>(form, "_speed").Enabled == options.HasFlag(EffectOptions.Speed));
            Check("Цвет эффекта " + e.Id, keys.Enabled == options.HasFlag(EffectOptions.Color));
            Check("Направление эффекта " + e.Id, Field<CheckBox>(form, "_reverse").Enabled == ((options & EffectOptions.AnyDirection) != 0));
        }
        foreach (LightBoxMode m in mode.Items)
        {
            mode.SelectedItem = m;
            Check("Яркость режима " + m.Id, Field<LightingSlider>(form, "_boxBrightness").Enabled == m.Options.HasFlag(EffectOptions.Brightness));
            Check("Скорость режима " + m.Id, Field<LightingSlider>(form, "_boxSpeed").Enabled == m.Options.HasFlag(EffectOptions.Speed));
            Check("Цвет режима " + m.Id, box.Enabled == m.Options.HasFlag(EffectOptions.Color));
        }
        mode.SelectedIndex = 3;
        Field<CheckBox>(form, "_boxEnabled").Checked = false;
        Field<TextBox>(box, "_hex").Text = "#123ABC";
        Check("Цвет выключенного Light box", settings.LightBox.Rgb == "#123ABC" && !settings.LightBoxEnabled && LightingPolicy.At(settings, Day).BoxOff);
        Field<CheckBox>(form, "_boxEnabled").Checked = true;
        effect.SelectedItem = effect.Items.Cast<Effect>().Single(e => e.Id == 6);
        Field<CheckBox>(form, "_multicolor").Checked = true;
        Check("Разноцветный отключает палитру", !keys.Enabled && Field<CheckBox>(form, "_reverse").Enabled);
        Field<CheckBox>(form, "_multicolor").Checked = false;
        int autostart = 0, updates = 0;
        form.AutostartToggled += _ => autostart++;
        form.UpdatesToggled += _ => updates++;
        edits = count();
        Field<CheckBox>(form, "_autostart").Checked = true;
        Field<CheckBox>(form, "_checkUpdates").Checked = true;
        Check("Служебные флажки не пишут свет", count() == edits && autostart == 1 && updates == 1);
        form.SetAutostart(false);
        Check("Внешний автозапуск без обратного события", autostart == 1 && count() == edits);
        form.SetUpdate(new ReleaseInfo(new Version(9, 9, 9), "https://example.invalid"));
        Check("Ссылка обновления видна", Field<LinkLabel>(form, "_updateLink").Visible);
        form.SetUpdate(null);
        Check("Ссылка обновления скрывается", !Field<LinkLabel>(form, "_updateLink").Visible);
        var slider = Field<LightingSlider>(form, "_speed");
        slider.Focus();
        int before = slider.Value;
        typeof(LightingSlider).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(slider, [new KeyEventArgs(Keys.Left)]);
        Check("Ползунок с клавиатуры", slider.Value == Math.Max(0, before - 1));
        Check("Доступное имя ползунка", !string.IsNullOrEmpty(slider.AccessibilityObject.Name));
        var field = AllControls(keys).Single(c => c.GetType().Name == "ColorField");
        hex.Text = "#FF0000";
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(field, [new KeyEventArgs(Keys.Down)]);
        Check("Поле цвета с клавиатуры", settings.Rgb != "#FF0000");
        var time = Field<LightingTimePicker>(form, "_quietStart");
        time.Value = Day.Date.AddHours(23);
        typeof(LightingTimePicker).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(time, [new KeyEventArgs(Keys.Up)]);
        Check("Время с клавиатуры через полночь", time.Value.Hour == 0 && settings.QuietHours.Start == "00:00");
        settings.Brightness = 65; settings.Effect = 6; settings.Speed = 2; settings.Rgb = "#00EAFF";
        settings.LightBox = new() { Mode = 3, Brightness = 35, Speed = 2, Rgb = "#FF8000", Multicolor = false };
        settings.QuietHours = new();
        form.Reload();
        Check("Тема графитовая", form.BackColor == LightingTheme.Background);
        Check("Три колонки", Field<TableLayoutPanel>(form, "_columns").ColumnCount == 3);
        var toggle = Field<CheckBox>(form, "_enabled");
        bool wasOn = toggle.Checked;
        toggle.AccessibilityObject.DoDefaultAction(); Application.DoEvents();
        Check("Переключатель через доступное действие", toggle.Checked != wasOn && settings.Enabled == toggle.Checked);
        toggle.Checked = true;
        int beforeReload = count();
        settings.Model = "FFFF:FFFF";
        form.Reload();
        Check("Неизвестная модель не объявлена подключённой", Field<Label>(form, "_model").Text == KbLight.Text.KeyboardLine(null));
        form.SetStatus(KbLight.Text.StatusNotFound);
        Check("Отключение отражено состоянием", Field<Label>(form, "_status").Text == KbLight.Text.StatusNotFound);
        foreach (string status in new[] { KbLight.Text.Applying, KbLight.Text.StatusFailed("USB"), KbLight.Text.StatusUnsupported("FFFF:FFFF") })
        {
            form.SetStatus(status);
            Check("Состояние передано без подмены", Field<Label>(form, "_status").Text == status);
        }
        settings.Model = "19F5:FB2A"; form.Reload();
        Check("Внешняя модель без записи", count() == beforeReload);

        var reverse = Field<CheckBox>(form, "_reverse");
        Check("Направление не обрезано панелью", reverse.Parent!.ClientRectangle.Contains(reverse.Bounds));

    }

    static void CheckCloseModes()
    {
        foreach (string mode in new[] { "button", "escape", "caption" })
        {
            var profile = Profile();
            using var window = new SettingsForm(profile, false);
            window.ShowInTaskbar = false; window.Show(); Application.DoEvents();
            int changes = 0; window.SettingsChanged += () => changes++;
            Field<LightingSlider>(window, "_brightness").Value = 42;
            if (mode == "button") AllControls(window).OfType<Button>().Single(b => b.Text == KbLight.Text.Close).PerformClick();
            else if (mode == "escape") typeof(Form).GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [Keys.Escape]);
            else
            {
                object[] message = [Message.Create(window.Handle, 0x0112, 0xF060, 0)];
                typeof(Form).GetMethod("WndProc", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, message);
            }
            Application.DoEvents();
            Check("Закрытие окна " + mode, window.IsDisposed);
            Check("Правка перед закрытием " + mode, changes == 1 && profile.Brightness == 42);
        }
    }

    static IEnumerable<Control> AllControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in AllControls(child)) yield return descendant;
        }
    }

    static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stop = DateTime.UtcNow + timeout;
        do
        {
            Application.DoEvents();
            if (condition()) return;
            Thread.Sleep(20);
        } while (DateTime.UtcNow < stop);
        throw new TimeoutException("Не дождались результата трея");
    }

    static void EditTray(TrayApp app, Action<LightSettings> edit)
    {
        edit(Field<LightSettings>(app, "_settings"));
        Field<System.Windows.Forms.Timer>(app, "_editDelay").Start();
    }

    static void CheckTray()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var initial = Keyboard.Read();
        Check("Клавиатура для проверки трея", initial is { Block: not null, Model: not null });
        var original = new LightSettings();
        original.SetFromBlock(initial.Block!, initial.Model);
        try
        {
            var s = Profile();
            s.QuietHours = new();
            Check("Подготовка профиля трея", Keyboard.Apply(s, true, initial.Block).Status == ApplyStatus.Written);
            var start = DateTime.Now.AddMinutes(1);
            start = new(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);
            var end = start.AddMinutes(1);
            s.QuietHours = new() { Enabled = true, Start = start.ToString("HH:mm"), End = end.ToString("HH:mm") };
            s.Save();
            Console.WriteLine($"Переходы трея: {start:HH:mm:ss} — {end:HH:mm:ss}; журнал {AppFiles.Log}");
            Console.Out.Flush();
            using (var app = new TrayApp(false))
            {
                PumpUntil(() => app.WaitIdle(TimeSpan.Zero) && Field<string>(app, "_status") != KbLight.Text.Applying, TimeSpan.FromSeconds(10));
                Check("Дневной профиль трея", Keyboard.Read().Block is { } day && day[9] == 65 && day[25] == 35);
                PumpUntil(() => DateTime.Now >= start.AddSeconds(3) && app.WaitIdle(TimeSpan.Zero), start - DateTime.Now + TimeSpan.FromSeconds(10));
                PumpUntil(() => Field<string>(app, "_status").Contains(KbLight.Text.QuietHoursActive(TimeOnly.FromDateTime(end))), TimeSpan.FromSeconds(3));
                Check("Наступили тихие часы", Keyboard.Read().Block is { } night && night[9] == 0 && night[25] == 0);
                Check("Профиль не обнулён", LightSettings.Load() is { Brightness: 65, LightBox.Brightness: 35 });
                Console.WriteLine("Ночной статус: " + Field<string>(app, "_status"));
                EditTray(app, settings => settings.Brightness = 80);
                PumpUntil(() => LightSettings.Load().Brightness == 80 && app.WaitIdle(TimeSpan.Zero), TimeSpan.FromSeconds(5));
                Check("Ночная правка не включает свет", Keyboard.Read().Block![9] == 0);
                int logLines = File.ReadAllLines(AppFiles.Log).Length;
                var stableUntil = DateTime.Now.AddSeconds(6);
                PumpUntil(() => DateTime.Now >= stableUntil, TimeSpan.FromSeconds(8));
                Check("Таймер не пишет при стабильном интервале", File.ReadAllLines(AppFiles.Log).Length == logLines);
                PumpUntil(() => DateTime.Now >= end.AddSeconds(3) && app.WaitIdle(TimeSpan.Zero), end - DateTime.Now + TimeSpan.FromSeconds(10));
                Check("Утром новый профиль", Keyboard.Read().Block is { } morning && morning[9] == 80 && morning[25] == 35);
                Check("Обе причины переходов в журнале", File.ReadAllText(AppFiles.Log).Contains(KbLight.Text.ReasonQuietStart + ": Written")
                    && File.ReadAllText(AppFiles.Log).Contains(KbLight.Text.ReasonQuietEnd + ": Written"));
                Console.WriteLine("Утренний статус: " + Field<string>(app, "_status"));
                app.ExitThread();
                app.WaitIdle(TimeSpan.FromSeconds(5));
            }
            s = Profile();
            s.QuietHours = new();
            Keyboard.Apply(s, true, initial.Block);
            s.QuietHours = new() { Enabled = true, Start = DateTime.Now.AddMinutes(-1).ToString("HH:mm"), End = DateTime.Now.AddMinutes(2).ToString("HH:mm") };
            s.LightBox = null;
            s.Save();
            using (var app = new TrayApp(false))
            {
                PumpUntil(() => app.WaitIdle(TimeSpan.Zero) && Field<LightSettings>(app, "_settings").LightBox is not null
                    && Field<string>(app, "_status").Contains(KbLight.Text.QuietHoursActive(TimeOnly.FromDateTime(DateTime.Now.AddMinutes(2)))), TimeSpan.FromSeconds(12));
                Check("Сначала сохранён неизвестный Light box", LightSettings.Load().LightBox?.Brightness == 35);
                Check("Затем погашены обе подсветки", Keyboard.Read().Block is { } night && night[9] == 0 && night[25] == 0);
                EditTray(app, settings => { settings.Enabled = false; settings.QuietHours = settings.QuietHours with { Enabled = false }; });
                PumpUntil(() => !LightSettings.Load().QuietHours.Enabled && app.WaitIdle(TimeSpan.Zero), TimeSpan.FromSeconds(6));
                Check("Ручное выключение после ночи", Keyboard.Read().Block is { } manual && manual[9] == 0 && manual[25] == 35);
                var requestType = typeof(TrayApp).GetNestedType("ApplyRequest", BindingFlags.NonPublic)!;
                long id = Field<long>(app, "_requestId");
                object Request(long number) => Activator.CreateInstance(requestType,
                    [Field<LightSettings>(app, "_settings").Clone(), false, KbLight.Text.ReasonResumed, 1, false, number])!;
                var onApplied = typeof(TrayApp).GetMethod("OnApplied", BindingFlags.Instance | BindingFlags.NonPublic)!;
                onApplied.Invoke(app, [Request(id + 1), new ApplyResult(ApplyStatus.NotFound, KbLight.Text.KeyboardNotFound)]);
                Check("Отсутствие устройства имеет приоритет", Field<string>(app, "_status") == KbLight.Text.StatusNotFound);
                onApplied.Invoke(app, [Request(id), new ApplyResult(ApplyStatus.Written, "", Keyboard.Read().Block, initial.Model)]);
                Check("Старый результат не перекрывает новый статус", Field<string>(app, "_status") == KbLight.Text.StatusNotFound);
                Check("Ограничение подсказки", Field<NotifyIcon>(app, "_tray").Text.Length <= 127);
                app.ExitThread();
                app.WaitIdle(TimeSpan.FromSeconds(5));
            }
            Console.WriteLine(File.ReadAllText(AppFiles.Log));
        }
        finally
        {
            var restored = Keyboard.Apply(original, true, initial.Block);
            Check("После трея исходный блок восстановлен", restored.Block!.SequenceEqual(initial.Block!));
        }
    }

    static void CheckDevice()
    {
        var read = Keyboard.Read();
        Check("Устройство отвечает", read is { Block: not null, Model: not null, Status: ApplyStatus.AlreadySet });
        byte[] initial = read.Block!;
        var original = new LightSettings();
        original.SetFromBlock(initial, read.Model);
        try
        {
            var s = Profile();
            s.QuietHours = new();
            foreach (var (keys, box) in new[] { (true, true), (false, true), (true, false), (false, false), (true, true) })
            {
                s.Enabled = keys; s.LightBoxEnabled = box;
                var result = Keyboard.Apply(s, true, initial);
                Check("Запись выключателей " + keys + "/" + box, result.Status == ApplyStatus.Written);
                var block = Keyboard.Read().Block!;
                Check("Яркости устройства", block[9] == (keys ? 65 : 0) && block[25] == (box ? 35 : 0));
                for (int i = 0; i < 32; i++)
                    if (!(i >= 8 && i <= 16 || i >= 24 && i != 27)) Check("Чужой байт устройства " + i, block[i] == initial[i]);
                Console.WriteLine($"Устройство: клавиши={block[9]}, Light box={block[25]}; блок={Convert.ToHexString(block)}");
            }
            var same = Keyboard.Apply(s, false, initial);
            Check("Сверка без записи", same.Status == ApplyStatus.AlreadySet);
        }
        finally
        {
            var restored = Keyboard.Apply(original, true, initial);
            Console.WriteLine($"Восстановление исходной подсветки: {restored.Status}; блок={Convert.ToHexString(restored.Block ?? [])}");
            Check("Исходный блок восстановлен", restored.Block is { } block && block.SequenceEqual(initial));
        }
    }

    static void CheckWait()
    {
        var initial = Keyboard.Read();
        Check("Клавиатура для проверки ожидания", initial is { Block: not null, Model: not null });
        var original = new LightSettings();
        original.SetFromBlock(initial.Block!, initial.Model);
        try
        {
            var s = Profile();
            s.QuietHours = new();
            Keyboard.Apply(s, true, initial.Block);
            var start = DateTime.Now.AddMinutes(1);
            start = new(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);
            if (start - DateTime.Now < TimeSpan.FromSeconds(8)) start = start.AddMinutes(1);
            s.QuietHours = new() { Enabled = true, Start = start.ToString("HH:mm"), End = start.AddMinutes(1).ToString("HH:mm") };
            Console.WriteLine($"Проверка ожидания через границу {start:HH:mm:ss}");
            Console.Out.Flush();
            while (DateTime.Now < start.AddSeconds(-2)) Thread.Sleep(20);
            using var mutex = new Mutex(false, @"Local\KbLight.Device");
            Check("Взяли мьютекс перед границей", mutex.WaitOne(TimeSpan.FromSeconds(1)));
            Task<ApplyResult> apply;
            try
            {
                apply = Task.Run(() => Keyboard.Apply(s, true, initial.Block));
                while (DateTime.Now < start.AddMilliseconds(150)) Thread.Sleep(20);
                Check("Запрос ждал устройство", !apply.IsCompleted);
            }
            finally { mutex.ReleaseMutex(); }
            var result = apply.GetAwaiter().GetResult();
            Check("Время пересчитано после ожидания", result is { Policy.Schedule.Active: true, Block: { } b } && b[9] == 0 && b[25] == 0);
            Console.WriteLine("После ожидания применены тихие часы, яркости 0/0.");
        }
        finally
        {
            var restored = Keyboard.Apply(original, true, initial.Block);
            Check("После ожидания исходный блок восстановлен", restored.Block!.SequenceEqual(initial.Block!));
        }
    }
}
