# CLAUDE.md — MarkdownEditor

## O que é

Aplicação Windows Forms (`net10.0-windows`) para visualizar e editar arquivos `.md`, `.markdown`, `.json`, `.xml` e `.txt`.

Solução: `MarkdownEditor.slnx` com um único projeto, `MarkdownEditor/MarkdownEditor.csproj`.

## Arquitetura

- **`Program.cs`**: `Main(string[] args)` pega o primeiro argumento que seja um arquivo existente e passa para `new frmEditor(arquivoInicial)`. É assim que funciona o duplo clique no Windows (o Windows passa o caminho como `%1`). O programa **não** grava associação de arquivos no Registro — isso foi decidido explicitamente. Quem registra é o **instalador** (ver "Geração do Instalador"); sem o instalador, a associação é feita manualmente pelo usuário via "Abrir com".
- **Arquivos do `frmEditor`** (classe `partial`): `frmEditor.cs` (constantes, campos, construtor, `Load`, atalhos e `WndProc`), `frmEditor.Arvore.cs`, `frmEditor.Preview.cs`, `frmEditor.Editor.cs`, `frmEditor.Monitor.cs`, `frmEditor.Historico.cs` e `frmEditor.Tema.cs`; agrupados sob o `frmEditor.cs` no `.csproj` (`DependentUpon` + `SubType=Code`). P/Invoke em `NativeMethods.cs` (`LibraryImport`; só `SHGetFileInfo` continua com `DllImport`, porque a struct usa `ByValTStr`), acessado com `using static`.
- **Exceções**: `Program.cs` usa `SetUnhandledExceptionMode(CatchException)` + `Application.ThreadException` (mostra `MessageBox`) e `AppDomain.UnhandledException` (só `Trace`). `async void` apenas em event handlers; métodos assíncronos disparados de outros pontos são `async Task` e chamados por `Disparar(tarefa)`, cuja exceção chega ao `ThreadException`.
- **`frmEditor`**: `ToolStrip tsrNavegacao` (botões `tsbArvore`, `tsbVoltar`/`tsbAvancar`, `tsbVisualizar`/`tsbEditar` e `tsbSalvar`) no topo e `SplitContainer spcPrincipal` com:
  - `Panel1`: `TreeView trvPastas` com `ImageList imlIcones`.
  - `Panel2`: `WebView2 wvwPreview` (pacote NuGet `Microsoft.Web.WebView2`) e `Scintilla sciEditor` (pacote NuGet `Scintilla5.NET`, namespace `ScintillaNET`, nativos em `runtimes\`); só um dos dois fica visível.
- **Árvore** (`frmEditor.cs`):
  - Raízes: Área de Trabalho, Documentos, Downloads e `DriveInfo.GetDrives()` (somente `IsReady`).
  - `Tag` de cada nó é um `record NoArvore(string Caminho, bool EhArquivo)`.
  - Carregamento lazy: nó de pasta nasce com um filho fictício (`"..."`, sem `Tag`), substituído no `BeforeExpand` por subpastas + arquivos filtrados por `ExtensoesSuportadas`. Pastas ocultas/sistema são ignoradas; `UnauthorizedAccessException`/`IOException` resultam em nó vazio.
  - Ícones em alta DPI: `CarregarIcones` define `imlIcones.ImageSize` com `LogicalToDeviceUnits(TamanhoIconeLogico)` e `AdicionarIcone` redimensiona (bicúbico) os ícones que vierem em outro tamanho; `SHGetFileInfo` pede `SHGFI_LARGEICON` quando o tamanho passa de 16 px.
  - Estilo do Explorer: `ShowLines = false`, `FullRowSelect`, `HotTracking`, `BorderStyle.None` (Designer), `ItemHeight` escalado e, em `AplicarEstiloArvore` (no `HandleCreated` e no `AplicarTema`), `TVS_EX_DOUBLEBUFFER | TVS_EX_FADEINOUTEXPANDOS` + `SetWindowTheme`.
  - Ícones: `SystemIcons.GetStockIcon` para pastas/unidades, `SHGetFileInfo` (shell32) para os atalhos Área de Trabalho/Documentos/Downloads (mesmo ícone do Explorer, com fallback para `"pasta"`) e `Icon.ExtractAssociatedIcon` com cache por extensão para arquivos.
  - `SelecionarNaArvore(caminho)` expande a árvore até o arquivo recebido por parâmetro.
  - Menu de contexto `cmsArvore` (`trvPastas.ContextMenuStrip`): "Abrir local do arquivo"/"Abrir pasta" (`explorer.exe /select,` ou `AbrirComShell`), "Copiar caminho completo" e "Copiar nome do arquivo"/"Copiar nome da pasta" (raiz de unidade copia o caminho). No `cmsArvore_Opening` o nó vem de `GetNodeAt` (mouse) ou `SelectedNode` (teclado); o clique direito não altera a seleção. Os textos vêm de `TextosMenu` e as ações (`AbrirLocal`, `CopiarCaminho`, `CopiarNome`) são compartilhadas com o WebView. Para pastas aparece também "Atualizar" (`tsmAtualizar`, só na árvore): `AtualizarPasta` relê o conteúdo de forma incremental (`AtualizarFilhos`), mantendo os nós existentes, removendo os que sumiram, inserindo os novos na ordem e repetindo recursivamente nas subpastas expandidas; subpastas já carregadas mas recolhidas voltam ao nó fictício (recarregam ao expandir). Pasta ainda não carregada não faz nada.
  - Botão `tsbArvore` ("Esconder árvore"/"Mostrar árvore"): `AlternarArvore` alterna `spcPrincipal.Panel1Collapsed`, guarda a largura em `_larguraArvore` ao esconder e a restaura ao mostrar, respeitando `LarguraMinimaArvore` (50 px, também aplicada em `Panel1MinSize`).
- **Visualizador**:
  - `wwwroot/preview-md-json-xml.html` é uma **cópia** do projeto `D:\Fontes\preview-html` (não é link). Copiado para a saída via `<Content Include="wwwroot\**" CopyToOutputDirectory="PreserveNewest" />`.
  - Servido por `SetVirtualHostNameToFolderMapping("preview.local", ...)` em `https://preview.local/preview-md-json-xml.html` (contexto seguro para `navigator.clipboard` e `localStorage` persistente).
  - Dados do WebView2 em `%LocalAppData%\MarkdownEditor\WebView2`.
  - Para exibir um arquivo, o C# chama a função global do HTML: `renderContent(texto, nome)` via `ExecuteScriptAsync`, com os argumentos serializados por `JsonSerializer.Serialize`.
  - Enquanto a página não terminou de carregar (`NavigationCompleted`), o arquivo fica em `_arquivoPendente` e é exibido em seguida.
  - Menu de contexto: `CoreWebView2_ContextMenuRequested` acrescenta ao menu nativo (separador + as mesmas 3 opções da árvore) referentes ao `trvPastas.SelectedNode`; sem item selecionado, as opções ficam desabilitadas.
  - **Segurança** (o Markdown pode ter HTML/JS embutido, pois o HTML usa `innerHTML` sem sanitizar): `CoreWebView2_WebMessageReceived` ignora mensagens cujo `e.Source` não seja `PaginaPreview`; `AbrirLinkAsync` só abre `http`/`https`/`mailto` no shell, recusa UNC, abre pastas existentes no Explorer, exibe os formatos suportados e, para qualquer outro arquivo, **nunca executa** — só `AbrirNoExplorer("/select,...")`. `CoreWebView2Settings`: `AreHostObjectsAllowed = false`, `IsStatusBarEnabled = false` e `AreDevToolsEnabled = false` fora do Debug. `WebView2RuntimeNotFoundException` oferece abrir `UrlDownloadWebView2`.
  - Arrastar/soltar, colar (Ctrl+V), tema, copiar e imprimir são recursos nativos do HTML e funcionam sem código C#.
  - Leitura de arquivos via `LerArquivoAsync` (`FileShare.ReadWrite | FileShare.Delete`), para não impedir outro processo de gravar/renomear/excluir durante a leitura. Devolve também a codificação (padrão UTF-8 sem BOM; BOM detectado é preservado ao salvar).
- **Edição** (`sciEditor`):
  - "Abas" = botões marcáveis `tsbVisualizar` (padrão) / `tsbEditar` na `tsrNavegacao`; `AlternarModoAsync` alterna a visibilidade de `wvwPreview`/`sciEditor` e mostra `tsbSalvar` só no modo Editar.
  - Todo conteúdo exibido também é carregado no editor por `CarregarNoEditor(texto, caminho?, codificação?)` (arquivo, texto colado, histórico, arquivo solto, "Novo"). Guarda `_textoOriginal` (último conteúdo conhecido em disco ou o texto colado) e `_codificacaoAtual`; zera o undo e marca o save point.
  - `ConfigurarEditor` usa `Technology.DirectWrite` (emojis coloridos e fonte de reserva automática para caracteres que a Consolas não tem).
  - Lexer por extensão (`DefinirLexer`): `markdown` (padrão e texto colado), `json`, `xml`, `null` (.txt). `AplicarEstilosEditor` aplica as cores VS Code Light+/Dark+ do lexer atual (os números de estilo mudam por lexer) e é chamado também por `AplicarTema`.
  - Alterações não salvas: `_previaDesatualizada` (setado no `TextChanged`) faz a prévia re-renderizar o texto do editor ao voltar para Visualizar (`AtualizarPreviaAsync`, preservando rolagem via `RenderizarPreservandoRolagemAsync`). O título ganha `*` quando `sciEditor.Modified`.
  - Gravação atômica: `GravarArquivoAsync` → `GravarComSubstituicaoAsync` grava em `~nome.ext.<guid>.tmp` na mesma pasta e troca com `File.Replace` (ou `File.Move` se o destino não existe); se falhar (sem permissão na pasta, sistema de arquivos sem suporte), grava direto no arquivo como fallback.
  - `tsbSalvar` (e Ctrl+S no modo Editar) habilita com `sciEditor.Modified` ou quando não há arquivo e existe texto (texto colado). `SalvarAsync` grava na mesma codificação; sem arquivo vai para `SalvarComoAsync` (`SaveFileDialog`), que define o arquivo atual, troca `_entradaAtual`, recarrega a pasta na árvore e seleciona o arquivo (`MostrarNaArvore`).
  - `tsbDesfazer` (habilitado com `sciEditor.Modified`): `DesfazerAlteracoes` volta o texto para `_textoOriginal` e marca o save point, sem limpar o undo (Ctrl+Z recupera as edições).
  - `ConfirmarDescarteAsync` (Sim/Não/Cancelar; "Não" volta o editor para `_textoOriginal`) é chamado em `trvPastas_BeforeSelect` (cancela e reseleciona depois), `NavegarHistoricoAsync`, `AbrirLink`, `FormClosing` e nos fluxos que o HTML já exibiu sozinho (colar, soltar, "Novo") — nestes, se cancelado, `RestaurarPreviaAsync` volta a prévia ao texto do editor.
- **Atualização automática do arquivo exibido**:
  - Toda troca do arquivo atual passa por `DefinirArquivoAtual(caminho?)` (atualiza `_arquivoAtual`, título e o monitor). Texto colado ou "Novo" → `null` (sem monitor).
  - `MonitorarArquivo` cria um `FileSystemWatcher` só para o arquivo exibido (pasta + `Filter` = nome, `SynchronizingObject = this`): notificações do Windows, sem polling e sem abrir o arquivo.
  - Os eventos (`Changed`/`Created`/`Deleted`/`Renamed`/`Error`) apenas reiniciam o `_temporizadorRecarga` (debounce de `IntervaloRecargaMs`). No `Tick` o estado real é conferido com `File.Exists` — isso cobre o salvamento atômico dos editores (exclui + renomeia temporário).
  - Existe → `RecarregarArquivoAsync`: se o conteúdo for igual a `_textoOriginal` não faz nada (cobre o próprio salvamento); senão recarrega editor (mantendo cursor/rolagem) e prévia preservando `stage.scrollTop`, sem histórico e sem mexer na árvore. Com alterações pendentes, pergunta antes se descarta. `IOException` (arquivo ainda em gravação) reagenda silenciosamente até `MaximoTentativasRecarga`.
  - Não existe → `TratarArquivoExcluidoAsync`: desliga o monitor, remove o(s) nó(s) da árvore (`LocalizarNosCarregados`, sem expandir; localiza arquivos ou pastas), avisa com uma mensagem só com "OK" e fecha a visualização (`newBtn.click()` no HTML e limpa o arquivo atual). Com alterações pendentes no editor, mantém o texto como não salvo (sem arquivo; Salvar abre o "Salvar como").
  - O clique no botão "Novo" do HTML é avisado pelo `ScriptMonitorarEventos` com a mensagem `novo`, que desliga o monitor.
  - `ScriptMonitorarEventos` é injetado via `AddScriptToExecuteOnDocumentCreatedAsync` (sem alterar o HTML) e avisa o C# por `postMessage`, tratado em `CoreWebView2_WebMessageReceived`: `arquivoSolto`, `textoColado:` + texto, `abrirLink:` + destino, `navegar:voltar` / `navegar:avancar`, `tema:light` / `tema:dark`.
- **Tema do form**:
  - Acompanha o tema do HTML. O script calcula o tema efetivo (`data-theme` em `<html>` ou, se ausente, `prefers-color-scheme`) e envia `tema:` no `DOMContentLoaded`, a cada mudança do atributo (`MutationObserver`) e quando o tema do Windows muda (`matchMedia`).
  - Na abertura, antes de a página carregar, usa o tema do Windows (`SistemaUsaTemaEscuro` → `Application.SystemColorMode`). `Application.SetColorMode` não é usado porque o tema muda em tempo de execução seguindo o HTML.
  - Visual Fluent (Windows 11), em `ConfigurarVisual` (construtor): fonte `Segoe UI Variable Text` 9 pt (fallback `Segoe UI`), botões da barra com ícone + texto e `Padding` escalado. `AtualizarIcones` gera os ícones da barra e do `cmsArvore` com `CriarIconeFluent` (glifos `Glifo*` da `Segoe Fluent Icons`, fallback `Segoe MDL2 Assets`) na cor do tema; é chamado por `AplicarTema` e `AlternarArvore`. `RenderizadorToolStrip` desenha hover/pressionado/marcado com cantos arredondados e, na barra, só uma linha inferior. O `cmsArvore` recebe cantos arredondados (`DWMWA_WINDOW_CORNER_PREFERENCE`) no `Opened`.
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
- `AppId=Markdown Editor` fixo (mesmo valor que o Inno usava por padrão, para não quebrar instalações existentes). `[Code]` → `InitializeSetup` verifica o .NET 10 Desktop Runtime (`{commonpf64}dotnetsharedMicrosoft.WindowsDesktop.App10.*`) e o WebView2 Runtime (valor `pv` em `EdgeUpdateClients{F3017226-...}`, HKLM WOW6432Node ou HKCU); se faltar, avisa e oferece abrir a página de download, sem interromper a instalação.
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
- Prefixos de controles: `frm` (form), `trv` (TreeView), `spc` (SplitContainer), `iml` (ImageList), `wvw` (WebView2), `tsr` (ToolStrip), `tsb` (ToolStripButton), `cms` (ContextMenuStrip), `tsm` (ToolStripMenuItem), `tss` (ToolStripSeparator), `sci` (Scintilla).
- Não adicionar comentários XML `<summary>` em métodos novos.
- P/Invoke novo vai em `NativeMethods.cs`, preferindo `LibraryImport` (`AllowUnsafeBlocks` habilitado no `.csproj` para o código gerado).
- Analisadores: `AnalysisLevel` = `latest-recommended`; o build deve ficar com 0 avisos.
- O `.csproj` remove a referência `Microsoft.Web.WebView2.Wpf` (target `RemoverReferenciaWebView2Wpf`), que o pacote WebView2 adiciona sempre e causava o aviso MSB3277 (conflito de `WindowsBase`).
- Não alterar o HTML em `wwwroot` para integrar com o C#; preferir chamar as funções já existentes. Se o HTML precisar mudar, avaliar se a mudança deve ir também para o projeto `preview-html` original.
- Não há testes automatizados; a verificação é feita executando o programa.
