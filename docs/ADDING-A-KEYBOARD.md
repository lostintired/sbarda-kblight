# Adding your keyboard to KbLight

KbLight only writes to keyboards it has a **model file** for. Out of the box it knows one: ZORNER ZH99 HE (USB `19F5:FB2A`). If your keyboard is configured with the sbarda app (its USB vendor ID is `19F5`) but is a different model, KbLight leaves it alone and says so:

> Клавиатура 19F5:XXXX не знакома программе. Нужен файл модели — ссылка «Модели» в окне.
> *(Keyboard 19F5:XXXX is unknown to the program. A model file is needed.)*

This guide shows how to describe your model, check that it works, and share it so that it becomes built-in for everyone.

What a model file can and cannot do: it tells KbLight which keyboard to talk to and which effects it has. The protocol itself (commands `01`/`02`/`05`/`06`, the 32-byte settings block, lighting bytes 8–16) is the same for every model, because sbarda builds it the same way for all of them. If your keyboard does not follow it, a model file will not help — please open an issue instead.

## What you need

- Your keyboard, connected by cable.
- [Python 3](https://www.python.org/downloads/) for the helper tools in `tools/` (standard library only, nothing to install).
- sbarda installed, or at least its database `%LOCALAPPDATA%\sbarda Files\db\*.db` left after uninstalling — optional, it only saves typing.
- KbLight closed while you experiment: right-click the tray icon → «Выход» (Exit). The tools do not coordinate with a running KbLight. Close sbarda too.

## 1. Find VID:PID

```
python tools/kbtool.py list
```

Every line is one USB interface of a `VID 19F5` device, for example:

```
\\?\hid#vid_19f5&pid_fb2a&mi_01#...  page=0001 usage=0000 in=65 out=65 feat=0
```

The interface with `in=65 out=65` is the settings channel. Note the PID (`fb2a`) and the interface number after `mi_` (`01` → `1`). Without Python: Device Manager → your keyboard → Properties → Details → Hardware Ids shows `VID_19F5&PID_XXXX&MI_NN`.

## 2. Check that the keyboard speaks the same protocol

```
python tools/kbtool.py --pid XXXX read
```

Expected — a reply starting with `aa 05`, and the block (after `|`) with `aa bb` in bytes 2–3:

```
raw : aa 05 00 98 20 00 00 00 | 50 00 aa bb 01 00 00 04 0d 64 04 00 01 00 ff 00 00 ...
mode=13 bright=100 speed(fw)=4 (ui 0) dir=0 colorful=1 colorindex=0 rgb=ff0000
```

Does `mode` match the effect your keyboard shows now? If there is no reply or no `aa bb`, stop here and open an issue with the output.

## 3. Find which effects your keyboard has

Write an effect and look at the keyboard:

```
python tools/kbtool.py --pid XXXX setmode <code> [brightness 0-100] [speed 0-4] [direction 0|1] [multicolor 0|1] [color index] [RRGGBB]
python tools/kbtool.py --pid XXXX setmode 1 50       # "Spectrum" at 50 % brightness
python tools/kbtool.py --pid XXXX setmode 3 100 0 0 0 0 00FF00   # static green
```

`setmode` reads the block, changes only bytes 8–16 and reads it back — the same safe sequence KbLight uses. Effect codes known from sbarda are listed in [PROTOCOL.md §5](PROTOCOL.md#5-эффекты): 1 Spectrum, 2 Staircase, 3 Static, 4 Breathing, 5 Hundred Flowers, 6 Wave, 7 Up and down wave, 8 Fountain, 9 Galaxy, 10 Rotation, 11 Tide, 12 Sea wave, 13 Ripple, 14 Constant Ripple, 15 Single point, 16 Grid, 17 Piano, 18 Flowing light, 19 Falling rain, 20 Starlight, 21 Fireworks, 22 Wave Band. Your model may have only some of them.

Note: on ZH99 HE what you write this way does not survive unplugging the keyboard — it falls back to its own lighting; yours probably behaves the same. Writing it again on every plug-in is exactly KbLight's job.

## 4. Make the model file

**With the sbarda database** (recommended) — one command, reads only:

```
python tools/make_model.py --name "Your keyboard name"
```

It takes VID, PID and the interface from the connected keyboard and the effect list with options from sbarda's active profile, and writes `%LOCALAPPDATA%\KbLight\models\19f5-xxxx.json`. Use `--pid` if several `19F5` keyboards are connected, `--db` if the database is elsewhere, `--out` to write somewhere else.

**By hand** — copy [src/models/zorner-zh99-he.json](../src/models/zorner-zh99-he.json) and edit it:

```json
{
  "name": "Your keyboard name",
  "vid": "19F5",
  "pid": "XXXX",
  "interface": 1,
  "effects": [
    { "id": 1, "options": ["brightness", "speed"] },
    { "id": 3, "options": ["brightness", "color", "multicolor"] },
    { "id": 6, "options": ["brightness", "speed", "color", "multicolor", "direction-horizontal"] }
  ]
}
```

| Field | Meaning |
|---|---|
| `name` | shown in KbLight's window as «Клавиатура: …» |
| `vid`, `pid` | four hex digits each |
| `interface` | the number after `mi_` of the `in=65 out=65` interface |
| `effects[].id` | effect code written to byte 8 |
| `effects[].options` | which controls the window enables: `brightness`, `speed`, `color`, `multicolor`, and one of `direction-horizontal`, `direction-vertical`, `direction-radial`, `direction-rotation` |
| `effects[].name` | optional; without it the name comes from the code table above |

A file with the same VID:PID as a built-in model replaces it — handy to fix a built-in model too.

## 5. Try it in KbLight

1. Put the file into `%LOCALAPPDATA%\KbLight\models\` — the «Модели» (Models) link in KbLight's window opens that folder.
2. Start KbLight. The window shows «Клавиатура: <your name>» and your effects. No restart is needed when you edit the file later: it is re-read on every write and when the window opens.
3. The log («Журнал» link, `%LOCALAPPDATA%\KbLight\kblight.log`) shows what happened:
   - `запуск программы: Written — было: …; стало: effect=…` — written and read back;
   - `файл модели <file> не прочитан: <reason>` — the file has an error, the reason says which;
   - `модель 19F5:XXXX: <file> заменяет встроенное описание` — your file replaced a built-in model.
4. Go through the effects in the window and check each one on the keyboard: speed, color, multicolor, direction.

## 6. Share it

So that the next owner of your model does not have to repeat this:

- **Pull request:** add the file as `src/models/<brand>-<model>.json` (lowercase, dashes). Mention the keyboard's exact model name, where it is sold, and attach the output of `python tools/kbtool.py --pid XXXX info` (firmware date) and `read`.
- **Or an issue** with the same: the file, the model name and the outputs of `info` and `read`.

Built-in models are embedded into `KbLight.exe` at build time and appear in the next release.
