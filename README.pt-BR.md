# KbLight — mantém a iluminação dos teclados sbarda

![KbLight](docs/images/banner.jpg)

*[English](README.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [Español](README.es.md)*

A interface do próprio programa existe apenas em inglês ou russo; esta página traduz somente a descrição.

Um pequeno aplicativo de bandeja do Windows que mantém a iluminação RGB escolhida em um teclado configurado com o aplicativo **sbarda**. Somente iluminação — o mapeamento de teclas, as macros e o acionamento continuam com o sbarda.

Testado no **ZORNER ZH99 HE** (Hall Effect, USB `19F5:FB2A`). Outros teclados sbarda podem ser adicionados com um arquivo de modelo — veja [docs/ADDING-A-KEYBOARD.md](docs/ADDING-A-KEYBOARD.md). Sem ele, o KbLight não envia nada ao teclado.

## O problema que ele resolve

- A iluminação do teclado é redefinida após reiniciar, após a suspensão ou após desconectá-lo: você define um efeito no sbarda e, da próxima vez, o teclado mostra o padrão (no ZH99 HE — uma "Onda" rápida).
- O sbarda não grava a iluminação de volta ao iniciar — ele apenas lê o teclado ([docs/PROTOCOL.md §6](docs/PROTOCOL.md#6-что-делает-sbardaexe-при-запуске)).
- A inicialização automática do próprio sbarda está quebrada: ela adiciona uma entrada de inicialização para `G68 Ultra.exe`, um arquivo que não existe, então o Windows informa um programa ausente a cada logon.

O KbLight grava a sua iluminação no teclado quando você entra no Windows, quando o teclado é conectado, após a suspensão e sempre que você a altera na janela dele. Cada gravação é relida e conferida.

**Caixa de luz.** O ZH99 HE também tem uma caixa de luz neon RGB que o sbarda não consegue configurar de forma alguma — só Fn+Home (modo), Fn+PgUp (brilho) e Fn+PgDn (cor) a alteram, e ela é redefinida na queda de energia, como a iluminação principal. O KbLight 1.3.0 também a mantém: o grupo **Light box** (caixa de luz) na janela escolhe o modo (linhas fluindo, piscando, cor fixa, respiração, desligada), o brilho, a velocidade e qualquer cor RGB, e ela é gravada junto com a iluminação principal. As teclas Fn continuam funcionando, mas o KbLight restaura a sua própria caixa de luz na próxima gravação (após a suspensão, ao conectar), então altere-a pela janela. Ao atualizar da versão 1.2.0, a caixa de luz mantém o que mostra agora.

<p align="center"><img src="docs/images/window-en.png" alt="KbLight settings window" width="416"></p>

## Instalação

1. Instale o [.NET Desktop Runtime 10 (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) se ainda não o tiver — caso contrário, o Windows oferece o link de download na primeira execução.
2. Baixe o `KbLight.exe` em [Releases](https://github.com/lostintired/sbarda-kblight/releases/latest) e coloque-o em `%LOCALAPPDATA%\Programs\KbLight\` (crie a pasta). Qualquer pasta serve, mas a inicialização automática aponta para onde o exe estava quando você a ativou.
3. Execute-o. O exe não é assinado, então o SmartScreen pode exibir "Windows protected your PC" — clique em **More info → Run anyway** (mais informações → executar assim mesmo).
4. Na janela, marque **Start with Windows** (iniciar com o Windows).

Na primeira execução o KbLight não grava nada: ele lê a iluminação que o teclado tem agora e a guarda como sua. Escolha um efeito na janela e, daí em diante, o KbLight mantém esse. Logo após um ciclo de energia o teclado mostra o próprio padrão, então, se a primeira execução do KbLight acontecer depois de uma reinicialização, esse padrão é o que ele assume — basta escolher seu efeito novamente.

O KbLight consulta o GitHub por uma versão mais nova uma vez por dia — veja [Atualizações](#atualizações).

A janela, o menu e o log ficam em inglês, ou em russo se o Windows estiver em russo. Para escolher manualmente, defina a variável de ambiente `KBLIGHT_LANG=en` ou `ru` e reinicie o KbLight.

## Remover a entrada de inicialização automática quebrada do sbarda

Se o Windows reclamar de `G68 Ultra.exe` no logon:

```
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v sbarda /f
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" /v sbarda /f
```

Você não precisa do sbarda em execução para a iluminação. Se o abrir para outras configurações, não altere a iluminação por ele — o KbLight restaura a sua no próximo logon. Não ative a inicialização automática do sbarda.

## Atualizações

A janela mostra a versão ("KbLight 1.3.0"); `KbLight.exe --version | Write-Output` a imprime no PowerShell.

Enquanto a bandeja está em execução, o KbLight consulta o GitHub pela última versão um minuto após iniciar e depois uma vez por dia. Se houver uma mais nova, a janela mostra um link "version N is available" (a versão N está disponível) e o Windows exibe uma notificação uma vez; ambos abrem a página da versão. Nada é baixado nem instalado automaticamente: para atualizar, feche o KbLight, substitua o `KbLight.exe` pelo novo e inicie-o.

O que passa pela rede: uma requisição HTTPS para `api.github.com/repos/lostintired/sbarda-kblight/releases/latest` com o cabeçalho `User-Agent: KbLight/<version>` — nada sobre você, seu PC ou seu teclado. Para desativar, desmarque **Check for updates** (verificar atualizações) na janela (`"CheckUpdates": false` em `settings.json`). `--apply`, `--check`, `--autostart` e `--version` nunca acessam a internet.

## Onde ficam as coisas

| O quê | Onde |
|---|---|
| Programa | `%LOCALAPPDATA%\Programs\KbLight\KbLight.exe` |
| Configurações e log | `%LOCALAPPDATA%\KbLight\settings.json`, `kblight.log` (o link **Log** na janela) |
| Seus modelos de teclado | `%LOCALAPPDATA%\KbLight\models\*.json` (o link **Models** (modelos) na janela) |
| Inicialização automática | tarefa `KbLight` do Agendador de Tarefas (no logon, `--tray`) |

## Linha de comando

- sem argumentos — ícone na bandeja e a janela de configurações (se o KbLight já estiver em execução, apenas mostra a janela);
- `--tray` — somente o ícone na bandeja, é assim que a inicialização automática o executa;
- `--apply` — grava a iluminação salva e sai;
- `--check` — grava somente se o teclado tiver outra coisa, depois sai;
- `--autostart on|off` — ativa ou desativa a inicialização automática;
- `--version` — imprime `KbLight <version>` e sai (o KbLight é um aplicativo com janela, então no PowerShell use um pipe: `KbLight.exe --version | Write-Output`).

Códigos de saída de `--apply` e `--check`: 0 — a iluminação está definida, 1 — teclado não encontrado, 2 — falha na gravação, 3 — ainda não há `settings.json` (teclado intocado), 4 — só foram encontrados teclados sem arquivo de modelo.

## Desinstalação

1. Clique com o botão direito no ícone da bandeja → **Exit** (sair).
2. `KbLight.exe --autostart off`.
3. Exclua `%LOCALAPPDATA%\Programs\KbLight` e `%LOCALAPPDATA%\KbLight`.

## Compilação

```
dotnet build src -c Release
dotnet publish src -c Release -o publish    # publish\KbLight.exe, a single file
```

Requer o .NET 10 SDK. As versões são compiladas pelo GitHub Actions a partir da tag (`.github/workflows/release.yml`). Sem pacotes NuGet, sem direitos de administrador. O ícone é refeito com `python tools/make_icon.py` (requer Pillow).

## Para desenvolvedores

Os documentos do projeto estão em russo:

- `openspec/specs/` — o que o programa deve fazer, um arquivo por capacidade;
- [docs/PROTOCOL.md](docs/PROTOCOL.md) — o protocolo HID do teclado, cada descoberta marcada como testada, derivada ou desconhecida;
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ADR.md](docs/ADR.md), [docs/CHANGELOG.md](docs/CHANGELOG.md);
- `tools/kbtool.py` — lê e grava diretamente o bloco de configurações do teclado (Python, somente biblioteca padrão);
- um fork que publica suas próprias versões altera a constante do repositório em `src/UpdateCheck.cs`.

As mudanças de comportamento passam pelo [OpenSpec](https://github.com/Fission-AI/OpenSpec): `/opsx:propose` → `/opsx:apply` → `/opsx:archive`. `CLAUDE.md` e `AGENTS.md` são as regras para agentes de código.

## Aviso legal

O KbLight não é afiliado ao sbarda, à ZORNER nem a qualquer fabricante de teclados; todas as marcas pertencem aos seus respectivos proprietários. O protocolo foi reconstruído para fins de interoperabilidade por meio da análise do sbarda.exe e do seu tráfego com o teclado; nenhum arquivo do sbarda está incluído neste repositório. O KbLight grava apenas os bytes de iluminação do bloco de configurações do teclado e deixa o restante como o teclado informou, mas você o usa por sua conta e risco.

## Licença

[MIT](LICENSE)
