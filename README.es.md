# KbLight — conserva la iluminación de los teclados sbarda

![KbLight](docs/images/banner.jpg)

*[English](README.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [Português (Brasil)](README.pt-BR.md)*

La interfaz del propio programa solo existe en inglés y en ruso; esta página traduce únicamente la descripción.

Una pequeña aplicación de bandeja para Windows que conserva la iluminación RGB que elegiste en un teclado configurado con la aplicación **sbarda**. Solo la iluminación: la asignación de teclas, las macros y el punto de actuación siguen en manos de sbarda.

Probado en el **ZORNER ZH99 HE** (Hall Effect, USB `19F5:FB2A`). Se pueden añadir otros teclados sbarda mediante un archivo de modelo; consulta [docs/ADDING-A-KEYBOARD.md](docs/ADDING-A-KEYBOARD.md). Sin ese archivo, KbLight no envía nada al teclado.

## El problema que resuelve

- La iluminación del teclado se restablece tras reiniciar, tras suspender el equipo o tras desconectar el teclado: configuras un efecto en sbarda y la próxima vez el teclado muestra el predeterminado (en el ZH99 HE, una "Wave" rápida).
- sbarda no vuelve a escribir la iluminación al iniciarse: solo lee el teclado ([docs/PROTOCOL.md §6](docs/PROTOCOL.md#6-что-делает-sbardaexe-при-запуске)).
- El autoinicio propio de sbarda está roto: añade una entrada de inicio para `G68 Ultra.exe`, un archivo que no existe, así que Windows avisa de un programa que falta en cada inicio de sesión.

KbLight escribe tu iluminación en el teclado cuando inicias sesión en Windows, cuando conectas el teclado, tras la suspensión y cada vez que la cambias en su ventana. Cada escritura se vuelve a leer y se comprueba.

**Light box (caja de luz).** El ZH99 HE también tiene una caja de luz de neón RGB que sbarda no puede configurar en absoluto: solo la cambian Fn+Home (modo), Fn+PgUp (brillo) y Fn+PgDn (color), y se restablece al cortarse la alimentación, igual que la iluminación principal. KbLight 1.3.0 también la conserva: el grupo **Light box** de la ventana elige el modo (líneas en movimiento, destellos, color fijo, respiración, apagada), el brillo, la velocidad y cualquier color RGB, y se escribe junto con la iluminación principal. Las teclas Fn siguen funcionando, pero KbLight restablece su propia caja de luz en la siguiente escritura (tras la suspensión, al conectar el teclado), así que haz los cambios en la ventana. Al actualizar desde la 1.2.0 se conserva lo que muestre ahora la caja de luz.

<p align="center"><img src="docs/images/window-en.png" alt="KbLight settings window" width="416"></p>

## Instalación

1. Instala el [.NET Desktop Runtime 10 (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) si no lo tienes; si no, Windows ofrece el enlace de descarga en el primer inicio.
2. Descarga `KbLight.exe` desde [Releases](https://github.com/lostintired/sbarda-kblight/releases/latest) y colócalo en `%LOCALAPPDATA%\Programs\KbLight\` (crea la carpeta). Sirve cualquier carpeta, pero el autoinicio apunta a donde estuviera el exe cuando lo activaste.
3. Ejecútalo. El exe no está firmado, así que SmartScreen puede mostrar "Windows protected your PC" (Windows protegió tu PC); haz clic en **More info → Run anyway** (más información → ejecutar de todas formas).
4. En la ventana, marca **Start with Windows** (iniciar con Windows).

En el primer inicio KbLight no escribe nada: lee la iluminación que tiene el teclado en ese momento y la guarda como tuya. Elige un efecto en la ventana y, a partir de entonces, KbLight conservará ese. Justo después de un ciclo de apagado, el teclado muestra su propio valor predeterminado, así que si el primer inicio de KbLight llega después de un reinicio, tomará ese valor; simplemente vuelve a elegir tu efecto.

KbLight consulta GitHub una vez al día para ver si hay una versión más reciente; consulta [Actualizaciones](#actualizaciones).

La ventana, el menú y el registro están en inglés, o en ruso si Windows está en ruso. Para elegir el idioma manualmente, define la variable de entorno `KBLIGHT_LANG=en` o `ru` y reinicia KbLight.

## Eliminar la entrada de autoinicio rota de sbarda

Si Windows se queja de `G68 Ultra.exe` al iniciar sesión:

```
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v sbarda /f
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" /v sbarda /f
```

No necesitas tener sbarda en ejecución para la iluminación. Si lo abres para otros ajustes, no cambies ahí la iluminación: KbLight restablece la suya en el siguiente inicio de sesión. No actives el autoinicio de sbarda.

## Actualizaciones

La ventana muestra la versión ("KbLight 1.3.0"); `KbLight.exe --version | Write-Output` la imprime en PowerShell.

Mientras la bandeja está en ejecución, KbLight consulta a GitHub la última versión un minuto después de iniciarse y luego una vez al día. Si hay una más reciente, la ventana muestra un enlace "version N is available" (hay una versión N disponible) y Windows muestra una notificación una sola vez; ambos abren la página de la versión. No se descarga ni se instala nada automáticamente: para actualizar, cierra KbLight, reemplaza `KbLight.exe` por el nuevo y vuelve a iniciarlo.

Qué viaja por la red: una solicitud HTTPS a `api.github.com/repos/lostintired/sbarda-kblight/releases/latest` con el encabezado `User-Agent: KbLight/<version>`; nada sobre ti, tu PC o tu teclado. Para desactivarlo, desmarca **Check for updates** (buscar actualizaciones) en la ventana (`"CheckUpdates": false` en `settings.json`). `--apply`, `--check`, `--autostart` y `--version` nunca se conectan a internet.

## Dónde está cada cosa

| Qué | Dónde |
|---|---|
| Programa | `%LOCALAPPDATA%\Programs\KbLight\KbLight.exe` |
| Ajustes y registro | `%LOCALAPPDATA%\KbLight\settings.json`, `kblight.log` (el enlace **Log** (registro) de la ventana) |
| Tus modelos de teclado | `%LOCALAPPDATA%\KbLight\models\*.json` (el enlace **Models** (modelos) de la ventana) |
| Autoinicio | Tarea `KbLight` del Programador de tareas (al iniciar sesión, `--tray`) |

## Línea de comandos

- sin argumentos: icono de bandeja y ventana de ajustes (si KbLight ya está en ejecución, solo muestra su ventana);
- `--tray`: solo el icono de bandeja; así lo ejecuta el autoinicio;
- `--apply`: escribe la iluminación guardada y termina;
- `--check`: la escribe solo si el teclado tiene otra cosa y termina;
- `--autostart on|off`: activa o desactiva el autoinicio;
- `--version`: imprime `KbLight <version>` y termina (KbLight es una aplicación con ventana, así que en PowerShell hay que canalizarlo: `KbLight.exe --version | Write-Output`).

Códigos de salida de `--apply` y `--check`: 0: la iluminación está aplicada, 1: no se encontró el teclado, 2: falló la escritura, 3: aún no existe `settings.json` (teclado intacto), 4: solo se encontraron teclados sin archivo de modelo.

## Desinstalación

1. Clic derecho en el icono de la bandeja → **Exit** (salir).
2. `KbLight.exe --autostart off`.
3. Elimina `%LOCALAPPDATA%\Programs\KbLight` y `%LOCALAPPDATA%\KbLight`.

## Compilación

```
dotnet build src -c Release
dotnet publish src -c Release -o publish    # publish\KbLight.exe, a single file
```

Requiere el SDK de .NET 10. Las versiones se compilan con GitHub Actions a partir de la etiqueta (`.github/workflows/release.yml`). Sin paquetes NuGet y sin derechos de administrador. El icono se regenera con `python tools/make_icon.py` (requiere Pillow).

## Para desarrolladores

Los documentos del proyecto están en ruso:

- `openspec/specs/`: lo que debe hacer el programa, un archivo por funcionalidad;
- [docs/PROTOCOL.md](docs/PROTOCOL.md): el protocolo HID del teclado, con cada hallazgo marcado como probado, deducido o desconocido;
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ADR.md](docs/ADR.md), [docs/CHANGELOG.md](docs/CHANGELOG.md);
- `tools/kbtool.py`: lee y escribe directamente el bloque de ajustes del teclado (Python, solo biblioteca estándar);
- un fork que publique sus propias versiones debe cambiar la constante del repositorio en `src/UpdateCheck.cs`.

Los cambios de comportamiento pasan por [OpenSpec](https://github.com/Fission-AI/OpenSpec): `/opsx:propose` → `/opsx:apply` → `/opsx:archive`. `CLAUDE.md` y `AGENTS.md` son las reglas para los agentes de programación.

## Aviso legal

KbLight no está afiliado a sbarda, ZORNER ni a ningún fabricante de teclados; todas las marcas comerciales pertenecen a sus respectivos propietarios. El protocolo se reconstruyó con fines de interoperabilidad analizando sbarda.exe y su tráfico con el teclado; este repositorio no incluye ningún archivo de sbarda. KbLight escribe únicamente los bytes de iluminación del bloque de ajustes del teclado y deja el resto tal como lo informó el teclado, pero lo usas bajo tu propio riesgo.

## Licencia

[MIT](LICENSE)
