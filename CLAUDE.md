# CLAUDE.md — MarkdownEditor

## O que é

Aplicação Windows Forms (`net10.0-windows`) para visualizar arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt`. Nesta etapa é **somente visualização**; as rotinas de **edição** do Markdown virão depois (ainda não existe nenhum código de edição).

Solução: `MarkdownEditor.slnx` com um único projeto, `MarkdownEditor/MarkdownEditor.csproj`.

## Arquitetura

- **`Program.cs`**: `Main(string[] args)` pega o primeiro argumento que seja um arquivo existente e passa para `new frmEditor(arquivoInicial)`. É assim que funciona o duplo clique no Windows (o Windows passa o caminho como `%1`). O programa **não** grava associação de arquivos no Registro — isso foi decidido explicitamente. Quem registra é o **instalador** (ver "Geração do Instalador"); sem o instalador, a associação é feita manualmente pelo usuário via "Abrir com".
- **`frmEditor`**: `ToolStrip tsrNavegacao` (botões `tsbArvore`, `tsbVoltar`/`tsbAvancar`) no topo e `SplitContainer spcPrincipal` com:
  - `Panel1`: `TreeView trvPastas` com `ImageList imlIcones`.
  - `Panel2`: `WebView2 wvwPreview` (pacote NuGet `Microsoft.Web.WebView2`).
- **Árvore** (`frmEditor.cs`):
  - Raízes: Área de Trabalho, Documentos, Downloads e `DriveInfo.GetDrives()` (somente `IsReady`).
  - `Tag` de cada nó é um `record NoArvore(string Caminho, bool EhArquivo)`.
  - Carregamento lazy: nó de pasta nasce com um filho fictício (`"..."`, sem `Tag`), substituído no `BeforeExpand` por subpastas + arquivos filtrados por `ExtensoesSuportadas`. Pastas ocultas/sistema são ignoradas; `UnauthorizedAccessException`/`IOException` resultam em nó vazio.
  - Ícones: `SystemIcons.GetStockIcon` para pastas/unidades e `Icon.ExtractAssociatedIcon` com cache por extensão para arquivos.
  - `SelecionarNaArvore(caminho)` expande a árvore até o arquivo recebido por parâmetro.
  - Menu de contexto `cmsArvore` (`trvPastas.ContextMenuStrip`): "Abrir local do arquivo"/"Abrir pasta" (`explorer.exe /select,` ou `AbrirComShell`), "Copiar caminho completo" e "Copiar nome do arquivo"/"Copiar nome da pasta" (raiz de unidade copia o caminho). No `cmsArvore_Opening` o nó vem de `GetNodeAt` (mouse) ou `SelectedNode` (teclado); o clique direito não altera a seleção. Os textos vêm de `TextosMenu` e as ações (`AbrirLocal`, `CopiarCaminho`, `CopiarNome`) são compartilhadas com o WebView.
  - Botão `tsbArvore` ("Esconder árvore"/"Mostrar árvore"): `AlternarArvore` alterna `spcPrincipal.Panel1Collapsed`, guarda a largura em `_larguraArvore` ao esconder e a restaura ao mostrar, respeitando `LarguraMinimaArvore` (50 px, também aplicada em `Panel1MinSize`).
- **Visualizador**:
  - `wwwroot/preview-md-json-xml.html` é uma **cópia** do projeto `D:\Fontes\preview-html` (não é link). Copiado para a saída via `<Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />`.
  - Servido por `SetVirtualHostNameToFolderMapping("preview.local", ...)` em `https://preview.local/preview-md-json-xml.html` (contexto seguro para `navigator.clipboard` e `localStorage` persistente).
  - Dados do WebView2 em `%LocalAppData%\MarkdownEditor\WebView2`.
  - Para exibir um arquivo, o C# chama a função global do HTML: `renderContent(texto, nome)` via `ExecuteScriptAsync`, com os argumentos serializados por `JsonSerializer.Serialize`.
  - Enquanto a página não terminou de carregar (`NavigationCompleted`), o arquivo fica em `_arquivoPendente` e é exibido em seguida.
  - Menu de contexto: `CoreWebView2_ContextMenuRequested` acrescenta ao menu nativo (separador + as mesmas 3 opções da árvore) referentes ao `trvPastas.SelectedNode`; sem item selecionado, as opções ficam desabilitadas.
  - Arrastar/soltar, colar (Ctrl+V), tema, copiar e imprimir são recursos nativos do HTML e funcionam sem código C#.
  - `ScriptMonitorarEventos` é injetado via `AddScriptToExecuteOnDocumentCreatedAsync` (sem alterar o HTML) e avisa o C# por `postMessage`, tratado em `CoreWebView2_WebMessageReceived`: `arquivoSolto`, `textoColado:` + texto, `abrirLink:` + destino, `navegar:voltar` / `navegar:avancar`, `tema:light` / `tema:dark`.
- **Tema do form**:
  - Acompanha o tema do HTML. O script calcula o tema efetivo (`data-theme` em `<html>` ou, se ausente, `prefers-color-scheme`) e envia `tema:` no `DOMContentLoaded`, a cada mudança do atributo (`MutationObserver`) e quando o tema do Windows muda (`matchMedia`).
  - Na abertura, antes de a página carregar, usa o tema do Windows (`SistemaUsaTemaEscuro`, que lê `AppsUseLightTheme` em `HKCU\...\Themes\Personalize`).
  - `AplicarTema(bool escuro)` usa as cores VS Code Light+/Dark+ do CSS no form, `spcPrincipal` (o divisor é o `BackColor`), `trvPastas`, `tsrNavegacao` e `cmsArvore` (`RenderizadorToolStrip` + `TabelaCoresToolStrip`) e no `DefaultBackgroundColor` do WebView2. A barra de título usa `DwmSetWindowAttribute` (`DWMWA_USE_IMMERSIVE_DARK_MODE`, reaplicado em `OnHandleCreated`) e as barras de rolagem da árvore usam `SetWindowTheme` (`DarkMode_Explorer`/`Explorer`).
- **Histórico Voltar/Avançar**:
  - `record EntradaHistorico(string? Caminho, string? Texto)`: um arquivo ou um texto colado (exibido como "texto colado").
  - Pilhas `_historicoVoltar` / `_historicoAvancar` (`LinkedList`, limitadas a `LimiteHistorico = 10`) e `_entradaAtual`. `RegistrarNoHistorico` ignora a entrada igual à atual (`MesmaEntrada`) e limpa o "avançar".
  - `NavegarHistoricoAsync` exibe a entrada (`ExibirEntradaAsync`: reseleciona o arquivo na árvore com `SelecionarNaArvore` e chama `ExibirArquivoAsync(..., registrarHistorico: false)`, ou re-renderiza o texto com `RenderizarAsync`). Se o arquivo não puder ser exibido, a entrada é descartada.
  - Botões ficam desabilitados quando a pilha está vazia; o tooltip mostra o nome do destino (`AtualizarBotoesNavegacao`).
  - Atalhos: Alt+← / Alt+→ e teclas BrowserBack/BrowserForward em `ProcessCmdKey` (só quando o foco não está no WebView2); botões laterais do mouse via `WM_APPCOMMAND` no `WndProc` e, dentro do WebView2, pelo `mouseup` dos botões 3/4 no `ScriptMonitorarEventos`.

## Geração do Instalador

Gerado com **Inno Setup** (`iscc` no PATH). Arquivos: `MarkdownEditor\Instalador\Instalador.iss` e `Release.cmd` (raiz), baseados no projeto `D:\Growdev\Refere\AutomacaoBrowser`.

- Build **framework-dependent**: a máquina precisa do .NET 10 Desktop Runtime. O instalador copia tudo de `MarkdownEditor\bin\Release\net10.0-windows\` (inclui `wwwroot\` e `runtimes\`) para `{commonpf}\MarkdownEditor`.
- Registro (root `HKA`, removido na desinstalação):
  - ProgID `MarkdownEditor.Arquivo` (`shell\open\command` = `"{app}\MarkdownEditor.exe" "%1"`) e `Applications\MarkdownEditor.exe\SupportedTypes`.
  - Sempre: `OpenWithProgids` de `.md`, `.markdown`, `.json`, `.xml` e `.txt` → opção no "Abrir com".
  - Tarefa opcional `associarmd` (desmarcada): grava `MarkdownEditor.Arquivo` como valor padrão de `.md` e `.markdown`.
  - **Comportamento esperado:** no Windows 10/11 um instalador não consegue definir o app padrão — a escolha do usuário fica em `UserChoice` (protegida por hash) e prevalece. Por isso, no primeiro duplo clique após a instalação, o Windows pergunta qual aplicativo usar, mesmo com `associarmd` marcada; o usuário escolhe o Markdown Editor e marca "Sempre". Isso é normal e foi decidido manter assim (não usar ferramentas que contornam o hash do `UserChoice`).
- Antes de gerar, atualizar a versão em:
  1. `Instalador.iss`: `AppVersion` e `OutputBaseFilename` (`MarkdownEditor-X.Y.Z`).
  2. `MarkdownEditor.csproj`: `AssemblyVersion`, `FileVersion` e `InformationalVersion`.
- `Release.cmd`: `dotnet build MarkdownEditor.slnx -c Release` → apaga o `MarkdownEditor-*.exe` anterior → `iscc MarkdownEditor\Instalador\Instalador.iss`. O `.exe` é gerado em `MarkdownEditor\Instalador\` (ignorado no git).

## Convenções

- Textos de interface, nomes de variáveis, métodos e controles em português (pt-BR).
- Prefixos de controles: `frm` (form), `trv` (TreeView), `spc` (SplitContainer), `iml` (ImageList), `wvw` (WebView2), `tsr` (ToolStrip), `tsb` (ToolStripButton), `cms` (ContextMenuStrip), `tsm` (ToolStripMenuItem).
- Não adicionar comentários XML `<summary>` em métodos novos.
- Não alterar o HTML em `wwwroot` para integrar com o C#; preferir chamar as funções já existentes. Se o HTML precisar mudar, avaliar se a mudança deve ir também para o projeto `preview-html` original.
- Não há testes automatizados; a verificação é feita executando o programa.
