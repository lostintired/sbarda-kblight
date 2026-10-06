# KbLight — 保持 sbarda 键盘的灯光设置

![KbLight](docs/images/banner.jpg)

*[English](README.md) · [Русский](README.ru.md) · [Español](README.es.md) · [Português (Brasil)](README.pt-BR.md)*

程序本身的界面只有英文或俄文；本页仅翻译说明文字。

一个小巧的 Windows 托盘程序，用来保持你在 **sbarda** 应用中为键盘选定的 RGB 灯光。只管灯光——按键映射、宏和触发行程仍由 sbarda 负责。

已在 **ZORNER ZH99 HE**（磁轴 Hall Effect，USB `19F5:FB2A`）上测试。其他 sbarda 键盘可以通过型号文件添加——见 [docs/ADDING-A-KEYBOARD.md](docs/ADDING-A-KEYBOARD.md)（英文）。没有型号文件时，KbLight 不会向键盘发送任何数据。

## 它解决的问题

- 键盘的灯光会在重启、睡眠或拔插后重置：你在 sbarda 里设置了灯效，下次键盘却显示默认灯效（ZH99 HE 上是快速的“Wave”）。
- sbarda 启动时不会把灯光写回键盘——它只读取键盘的状态（[docs/PROTOCOL.md §6](docs/PROTOCOL.md#6-что-делает-sbardaexe-при-запуске)，该文档为俄文）。
- sbarda 自带的开机自启是坏的：它添加了一个指向 `G68 Ultra.exe` 的启动项，而这个文件并不存在，所以每次登录 Windows 都会提示找不到程序。

KbLight 会在你登录 Windows、插入键盘、睡眠唤醒后，以及你在它的窗口中修改设置时，把你的灯光写入键盘。每次写入后都会回读并校验。

**灯箱（Light box）。** ZH99 HE 还带有一个 RGB 霓虹灯箱，sbarda 完全无法设置——只能通过 Fn+Home（模式）、Fn+PgUp（亮度）和 Fn+PgDn（颜色）调节，并且和主灯光一样，断电后会重置。KbLight 1.3.0 也会保持它：窗口中的 **Light box**（灯箱）分组可以选择模式（流动线条、闪烁、常亮单色、呼吸、关闭）、亮度、速度和任意 RGB 颜色，并与主灯光一起写入。Fn 组合键仍然可用，但 KbLight 会在下一次写入时（睡眠后、插入键盘时）把它自己的灯箱设置恢复回去，所以请在窗口中修改。从 1.2.0 升级时，会保留灯箱当前显示的状态。

<p align="center"><img src="docs/images/window-en.png" alt="KbLight settings window" width="416"></p>

## 安装

1. 如果还没有，请安装 [.NET Desktop Runtime 10 (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)——否则 Windows 会在首次启动时提供下载链接。
2. 从 [Releases](https://github.com/lostintired/sbarda-kblight/releases/latest) 下载 `KbLight.exe`，放到 `%LOCALAPPDATA%\Programs\KbLight\`（需新建该文件夹）。放在任何文件夹都可以，但开机自启指向的是你启用它时 exe 所在的位置。
3. 运行它。该 exe 未签名，所以 SmartScreen 可能会提示“Windows 已保护你的电脑”——点击 **More info → Run anyway**（更多信息 → 仍要运行）。
4. 在窗口中勾选 **Start with Windows**（随 Windows 启动）。

首次启动时，KbLight 不会写入任何内容：它读取键盘当前的灯光并将其记为你的设置。在窗口中选择一个灯效，此后 KbLight 就会一直保持它。断电重启后键盘显示的是它自己的默认灯效，所以如果 KbLight 第一次启动是在重启之后，它记下的就是那个默认灯效——重新选择你的灯效即可。

KbLight 每天向 GitHub 检查一次是否有新版本——见[更新](#更新)。

窗口、菜单和日志使用英文；如果 Windows 是俄文，则使用俄文。要手动选择，请设置环境变量 `KBLIGHT_LANG=en` 或 `ru`，然后重启 KbLight。

## 删除 sbarda 损坏的开机自启项

如果 Windows 在登录时提示 `G68 Ultra.exe` 的问题：

```
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v sbarda /f
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" /v sbarda /f
```

灯光不需要 sbarda 运行。如果你为了其他设置打开了 sbarda，请不要在里面修改灯光——KbLight 会在下次登录时把它自己的设置写回去。也不要启用 sbarda 的开机自启。

## 更新

窗口中会显示版本号（“KbLight 1.3.0”）；在 PowerShell 中，`KbLight.exe --version | Write-Output` 也会打印版本号。

托盘运行期间，KbLight 在启动一分钟后向 GitHub 查询最新版本，之后每天一次。如果有更新的版本，窗口中会显示“version N is available”链接，Windows 也会弹出一次通知；两者都会打开发布页面。程序不会自动下载或安装任何东西：要更新，请退出 KbLight，用新的 `KbLight.exe` 替换旧文件，然后再启动。

涉及的网络请求：向 `api.github.com/repos/lostintired/sbarda-kblight/releases/latest` 发起一次 HTTPS 请求，带有请求头 `User-Agent: KbLight/<version>`——不包含任何关于你、你的电脑或你的键盘的信息。要关闭此功能，请在窗口中取消勾选 **Check for updates**（检查更新）（即 `settings.json` 中的 `"CheckUpdates": false`）。`--apply`、`--check`、`--autostart` 和 `--version` 从不联网。

## 文件位置

| 内容 | 位置 |
|---|---|
| 程序 | `%LOCALAPPDATA%\Programs\KbLight\KbLight.exe` |
| 设置和日志 | `%LOCALAPPDATA%\KbLight\settings.json`、`kblight.log`（窗口中的 **Log**（日志）链接） |
| 你的键盘型号 | `%LOCALAPPDATA%\KbLight\models\*.json`（窗口中的 **Models**（型号）链接） |
| 开机自启 | 任务计划程序任务 `KbLight`（登录时运行，`--tray`） |

## 命令行

- 无参数——托盘图标加设置窗口（如果 KbLight 已在运行，则只显示它的窗口）；
- `--tray`——只有托盘图标，开机自启就是这样运行它的；
- `--apply`——写入已保存的灯光并退出；
- `--check`——仅当键盘上的灯光与保存的不同时才写入，然后退出；
- `--autostart on|off`——开启或关闭开机自启；
- `--version`——打印 `KbLight <version>` 并退出（KbLight 是窗口程序，所以在 PowerShell 中需要用管道：`KbLight.exe --version | Write-Output`）。

`--apply` 和 `--check` 的退出码：0——灯光已设置，1——未找到键盘，2——写入失败，3——还没有 `settings.json`（未改动键盘），4——只找到没有型号文件的键盘。

## 卸载

1. 右键单击托盘图标 → **Exit**（退出）。
2. 运行 `KbLight.exe --autostart off`。
3. 删除 `%LOCALAPPDATA%\Programs\KbLight` 和 `%LOCALAPPDATA%\KbLight`。

## 构建

```
dotnet build src -c Release
dotnet publish src -c Release -o publish    # publish\KbLight.exe, a single file
```

需要 .NET 10 SDK。发行版由 GitHub Actions 根据标签构建（`.github/workflows/release.yml`）。无 NuGet 包，无需管理员权限。图标通过 `python tools/make_icon.py` 重新生成（需要 Pillow）。

## 开发者须知

项目文档为俄文：

- `openspec/specs/`——程序必须做什么，每个功能一个文件；
- [docs/PROTOCOL.md](docs/PROTOCOL.md)——键盘的 HID 协议，每项发现都标注为已测试、推导得出或未知；
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)、[docs/ADR.md](docs/ADR.md)、[docs/CHANGELOG.md](docs/CHANGELOG.md)；
- `tools/kbtool.py`——直接读写键盘的设置块（Python，仅使用标准库）；
- 发布自己版本的 fork 需要修改 `src/UpdateCheck.cs` 中的仓库常量。

行为变更通过 [OpenSpec](https://github.com/Fission-AI/OpenSpec) 进行：`/opsx:propose` → `/opsx:apply` → `/opsx:archive`。`CLAUDE.md` 和 `AGENTS.md` 是给编码代理的规则。

## 免责声明

KbLight 与 sbarda、ZORNER 或任何键盘厂商均无关联；所有商标归其各自所有者所有。该协议是为实现互操作性，通过分析 sbarda.exe 及其与键盘之间的通信重建而成；本仓库不包含任何 sbarda 文件。KbLight 只写入键盘设置块中的灯光字节，其余部分保持键盘报告的原样，但使用风险由你自行承担。

## 许可证

[MIT](LICENSE)
