using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MarkdownEditor
{
    public partial class frmEditor : Form
    {
        private const string HostPreview = "preview.local";
        private const string PaginaPreview = "https://preview.local/preview-md-json-xml.html";
        private const string TextoNoFicticio = "...";
        private const string TituloPadrao = "Visualizador e editor de Markdown";
        private const string MensagemArquivoSolto = "arquivoSolto";
        private const string PrefixoMensagemTextoColado = "textoColado:";
        private const string PrefixoMensagemLink = "abrirLink:";
        private const string MensagemNavegarVoltar = "navegar:voltar";
        private const string MensagemNavegarAvancar = "navegar:avancar";
        private const string PrefixoMensagemTema = "tema:";
        private const string TemaEscuro = "dark";
        private const string NomeTextoColado = "texto colado";
        private const int LimiteHistorico = 10;
        private const int LarguraMinimaArvore = 50;
        private const string TextoEsconderArvore = "◧ Esconder árvore";
        private const string TextoMostrarArvore = "◨ Mostrar árvore";
        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_BROWSER_BACKWARD = 1;
        private const int APPCOMMAND_BROWSER_FORWARD = 2;

        private const string ScriptMonitorarEventos = """
            window.addEventListener('drop', (e) => {
                const arquivo = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
                if (arquivo) window.chrome.webview.postMessageWithAdditionalObjects('arquivoSolto', [arquivo]);
            });
            window.addEventListener('paste', (e) => {
                const texto = (e.clipboardData || window.clipboardData).getData('text');
                if (texto && texto.trim().length > 0) window.chrome.webview.postMessage('textoColado:' + texto);
            });
            const botaoNavegacao = (e) => e.button === 3 ? 'navegar:voltar' : e.button === 4 ? 'navegar:avancar' : null;
            window.addEventListener('mousedown', (e) => {
                if (botaoNavegacao(e)) e.preventDefault();
            }, true);
            window.addEventListener('mouseup', (e) => {
                const mensagem = botaoNavegacao(e);
                if (!mensagem) return;
                e.preventDefault();
                window.chrome.webview.postMessage(mensagem);
            }, true);
            window.addEventListener('keydown', (e) => {
                let mensagem = null;
                if (e.key === 'BrowserBack' || (e.altKey && e.key === 'ArrowLeft')) mensagem = 'navegar:voltar';
                else if (e.key === 'BrowserForward' || (e.altKey && e.key === 'ArrowRight')) mensagem = 'navegar:avancar';
                if (!mensagem) return;
                e.preventDefault();
                window.chrome.webview.postMessage(mensagem);
            }, true);
            document.addEventListener('change', (e) => {
                const alvo = e.target;
                if (!(alvo instanceof HTMLInputElement) || alvo.type !== 'file') return;
                const arquivo = alvo.files && alvo.files[0];
                if (arquivo) window.chrome.webview.postMessageWithAdditionalObjects('arquivoSolto', [arquivo]);
            }, true);
            const interceptarLink = (e) => {
                const link = e.target instanceof Element ? e.target.closest('a[href]') : null;
                if (!link) return;
                const href = link.getAttribute('href');
                if (!href || href.startsWith('#')) return;
                e.preventDefault();
                if (e.type === 'auxclick' && e.button !== 1) return;
                window.chrome.webview.postMessage('abrirLink:' + href);
            };
            document.addEventListener('click', interceptarLink, true);
            document.addEventListener('auxclick', interceptarLink, true);
            const midiaEscura = window.matchMedia('(prefers-color-scheme: dark)');
            let temaAvisado = null;
            const avisarTema = () => {
                const tema = document.documentElement.getAttribute('data-theme') || (midiaEscura.matches ? 'dark' : 'light');
                if (tema === temaAvisado) return;
                temaAvisado = tema;
                window.chrome.webview.postMessage('tema:' + tema);
            };
            document.addEventListener('DOMContentLoaded', () => {
                avisarTema();
                new MutationObserver(avisarTema).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
            });
            midiaEscura.addEventListener('change', avisarTema);
            """;

        private static readonly Color CorFundoEscuro = ColorTranslator.FromHtml("#1E1E1E");
        private static readonly Color CorPainelEscuro = ColorTranslator.FromHtml("#252526");
        private static readonly Color CorTextoEscuro = ColorTranslator.FromHtml("#D4D4D4");
        private static readonly Color CorBordaEscuro = ColorTranslator.FromHtml("#3C3C3C");
        private static readonly Color CorDestaqueEscuro = ColorTranslator.FromHtml("#37373D");
        private static readonly Color CorPressionadoEscuro = ColorTranslator.FromHtml("#45454B");
        private static readonly Color CorDesabilitadoEscuro = ColorTranslator.FromHtml("#6E6E6E");
        private static readonly Color CorFundoClaro = ColorTranslator.FromHtml("#FFFFFF");
        private static readonly Color CorPainelClaro = ColorTranslator.FromHtml("#F3F3F3");
        private static readonly Color CorTextoClaro = ColorTranslator.FromHtml("#1E1E1E");
        private static readonly Color CorBordaClaro = ColorTranslator.FromHtml("#D4D4D4");
        private static readonly Color CorDestaqueClaro = ColorTranslator.FromHtml("#E4E4E4");
        private static readonly Color CorPressionadoClaro = ColorTranslator.FromHtml("#D4D4D4");
        private static readonly Color CorDesabilitadoClaro = ColorTranslator.FromHtml("#A0A0A0");

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? nomeSubApp, string? listaSubId);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static readonly HashSet<string> ExtensoesSuportadas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".markdown", ".json", ".xml", ".txt"
        };

        private record NoArvore(string Caminho, bool EhArquivo);

        private record EntradaHistorico(string? Caminho, string? Texto);

        private bool _previewPronto;
        private string? _arquivoPendente;
        private string? _arquivoAtual;
        private bool _ignorarSelecao;

        private readonly LinkedList<EntradaHistorico> _historicoVoltar = new();
        private readonly LinkedList<EntradaHistorico> _historicoAvancar = new();
        private EntradaHistorico? _entradaAtual;
        private bool _navegandoHistorico;

        private int _larguraArvore;
        private int? _deslocamentoArraste;

        private bool? _temaEscuro;

        private NoArvore? _noMenuContexto;

        private readonly string? _arquivoInicial;

        public frmEditor() : this(null)
        {
        }

        public frmEditor(string? arquivoInicial)
        {
            InitializeComponent();
            spcPrincipal.Panel1MinSize = LarguraMinimaArvore;
            _arquivoInicial = arquivoInicial;
        }

        private async void frmEditor_Load(object? sender, EventArgs e)
        {
            AplicarTema(SistemaUsaTemaEscuro());
            CarregarIcones();
            CarregarRaizes();
            AtualizarBotoesNavegacao();

            if (_arquivoInicial != null)
            {
                _arquivoPendente = _arquivoInicial;
                SelecionarNaArvore(_arquivoInicial);
            }

            await InicializarPreviewAsync();
        }

        private bool SelecionarNaArvore(string caminho)
        {
            var caminhoCompleto = Path.GetFullPath(caminho);
            var raiz = Path.GetPathRoot(caminhoCompleto);
            if (string.IsNullOrEmpty(raiz))
                return false;

            var no = trvPastas.Nodes.Cast<TreeNode>().FirstOrDefault(n =>
                n.Tag is NoArvore info && string.Equals(info.Caminho, raiz, StringComparison.OrdinalIgnoreCase));
            if (no == null)
                return false;

            var partes = caminhoCompleto[raiz.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var parte in partes)
            {
                no.Expand();
                var filho = no.Nodes.Cast<TreeNode>().FirstOrDefault(n =>
                    n.Tag is NoArvore info && string.Equals(Path.GetFileName(info.Caminho), parte, StringComparison.OrdinalIgnoreCase));
                if (filho == null)
                    return false;
                no = filho;
            }

            trvPastas.SelectedNode = no;
            no.EnsureVisible();
            return true;
        }

        private void CarregarIcones()
        {
            AdicionarIconeStock("pasta", StockIconId.Folder);
            AdicionarIconeStock("pastaAberta", StockIconId.FolderOpen);
            AdicionarIconeStock("arquivo", StockIconId.DocumentNoAssociation);
            AdicionarIconeStock("unidadeFixa", StockIconId.DriveFixed);
            AdicionarIconeStock("unidadeRemovivel", StockIconId.DriveRemovable);
            AdicionarIconeStock("unidadeRede", StockIconId.DriveNet);
            AdicionarIconeStock("unidadeCD", StockIconId.DriveCD);
        }

        private void AdicionarIconeStock(string chave, StockIconId id)
        {
            using var icone = SystemIcons.GetStockIcon(id, StockIconOptions.SmallIcon);
            imlIcones.Images.Add(chave, icone);
        }

        private string ObterChaveIconePasta(string caminho, string chave)
        {
            if (imlIcones.Images.ContainsKey(chave))
                return chave;

            var info = new SHFILEINFO();
            SHGetFileInfo(caminho, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_SMALLICON);
            if (info.hIcon == IntPtr.Zero)
                return "pasta";

            try
            {
                using var icone = Icon.FromHandle(info.hIcon);
                imlIcones.Images.Add(chave, icone);
                return chave;
            }
            catch (Exception)
            {
                return "pasta";
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        private string ObterChaveIconeArquivo(string caminho)
        {
            var chave = "ext" + Path.GetExtension(caminho).ToLowerInvariant();
            if (imlIcones.Images.ContainsKey(chave))
                return chave;

            try
            {
                using var icone = Icon.ExtractAssociatedIcon(caminho);
                if (icone == null)
                    return "arquivo";

                imlIcones.Images.Add(chave, icone);
                return chave;
            }
            catch (Exception)
            {
                return "arquivo";
            }
        }

        private void CarregarRaizes()
        {
            trvPastas.BeginUpdate();
            try
            {
                trvPastas.Nodes.Clear();

                var atalhos = new (string Nome, string Caminho, string ChaveIcone)[]
                {
                    ("Área de Trabalho", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pastaDesktop"),
                    ("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pastaDocumentos"),
                    ("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "pastaDownloads")
                };

                foreach (var (nome, caminho, chaveIcone) in atalhos)
                {
                    if (!string.IsNullOrEmpty(caminho) && Directory.Exists(caminho))
                        trvPastas.Nodes.Add(CriarNoPasta(nome, caminho, ObterChaveIconePasta(caminho, chaveIcone)));
                }

                foreach (var unidade in DriveInfo.GetDrives())
                {
                    if (!unidade.IsReady)
                        continue;

                    var rotulo = string.IsNullOrWhiteSpace(unidade.VolumeLabel)
                        ? unidade.Name
                        : $"{unidade.VolumeLabel} ({unidade.Name.TrimEnd('\\')})";

                    var icone = unidade.DriveType switch
                    {
                        DriveType.Removable => "unidadeRemovivel",
                        DriveType.Network => "unidadeRede",
                        DriveType.CDRom => "unidadeCD",
                        _ => "unidadeFixa"
                    };

                    trvPastas.Nodes.Add(CriarNoPasta(rotulo, unidade.RootDirectory.FullName, icone));
                }
            }
            finally
            {
                trvPastas.EndUpdate();
            }
        }

        private static TreeNode CriarNoPasta(string texto, string caminho, string chaveIcone)
        {
            var no = new TreeNode(texto)
            {
                Tag = new NoArvore(caminho, false),
                ImageKey = chaveIcone,
                SelectedImageKey = chaveIcone
            };
            no.Nodes.Add(new TreeNode(TextoNoFicticio));
            return no;
        }

        private TreeNode CriarNoArquivo(string caminho)
        {
            var chaveIcone = ObterChaveIconeArquivo(caminho);
            return new TreeNode(Path.GetFileName(caminho))
            {
                Tag = new NoArvore(caminho, true),
                ImageKey = chaveIcone,
                SelectedImageKey = chaveIcone
            };
        }

        private void trvPastas_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var no = e.Node;
            if (no == null || no.Tag is not NoArvore { EhArquivo: false } info)
                return;

            if (no.Nodes.Count != 1 || no.Nodes[0].Tag != null)
                return;

            trvPastas.BeginUpdate();
            Cursor = Cursors.WaitCursor;
            try
            {
                no.Nodes.Clear();
                var diretorio = new DirectoryInfo(info.Caminho);

                foreach (var subpasta in ListarSubpastas(diretorio))
                    no.Nodes.Add(CriarNoPasta(subpasta.Name, subpasta.FullName, "pasta"));

                foreach (var arquivo in ListarArquivos(diretorio))
                    no.Nodes.Add(CriarNoArquivo(arquivo.FullName));
            }
            finally
            {
                Cursor = Cursors.Default;
                trvPastas.EndUpdate();
            }
        }

        private static IEnumerable<DirectoryInfo> ListarSubpastas(DirectoryInfo diretorio)
        {
            try
            {
                return diretorio.GetDirectories()
                    .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return [];
            }
        }

        private static IEnumerable<FileInfo> ListarArquivos(DirectoryInfo diretorio)
        {
            try
            {
                return diretorio.GetFiles()
                    .Where(f => ExtensoesSuportadas.Contains(f.Extension)
                                && (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return [];
            }
        }

        private async void trvPastas_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_ignorarSelecao || e.Node?.Tag is not NoArvore { EhArquivo: true } info)
                return;

            if (!_previewPronto)
            {
                _arquivoPendente = info.Caminho;
                return;
            }

            await ExibirArquivoAsync(info.Caminho);
        }

        private async Task InicializarPreviewAsync()
        {
            try
            {
                var pastaDados = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkdownEditor", "WebView2");
                var ambiente = await CoreWebView2Environment.CreateAsync(userDataFolder: pastaDados);
                await wvwPreview.EnsureCoreWebView2Async(ambiente);

                wvwPreview.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    HostPreview,
                    Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                    CoreWebView2HostResourceAccessKind.Allow);

                await wvwPreview.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ScriptMonitorarEventos);
                wvwPreview.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                wvwPreview.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                wvwPreview.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                wvwPreview.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                wvwPreview.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                wvwPreview.CoreWebView2.Navigate(PaginaPreview);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Não foi possível inicializar o visualizador (WebView2).\n" +
                    "Verifique se o Microsoft Edge WebView2 Runtime está instalado.\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
                return;

            _previewPronto = true;

            if (_arquivoPendente != null)
            {
                var caminho = _arquivoPendente;
                _arquivoPendente = null;
                await ExibirArquivoAsync(caminho);
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string? mensagem;
            try
            {
                mensagem = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                return;
            }

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemLink, StringComparison.Ordinal))
            {
                AbrirLink(mensagem[PrefixoMensagemLink.Length..]);
                return;
            }

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemTextoColado, StringComparison.Ordinal))
            {
                RegistrarTextoColado(mensagem[PrefixoMensagemTextoColado.Length..]);
                return;
            }

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemTema, StringComparison.Ordinal))
            {
                AplicarTema(mensagem[PrefixoMensagemTema.Length..] == TemaEscuro);
                return;
            }

            switch (mensagem)
            {
                case MensagemNavegarVoltar:
                    Voltar();
                    break;

                case MensagemNavegarAvancar:
                    Avancar();
                    break;

                case MensagemArquivoSolto:
                    var caminho = e.AdditionalObjects.FirstOrDefault() switch
                    {
                        FileInfo info => info.FullName,
                        CoreWebView2File arquivo => arquivo.Path,
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(caminho))
                        PosicionarArquivoSolto(caminho);
                    break;

            }
        }

        private void RegistrarTextoColado(string texto)
        {
            LimparSelecaoArvore();
            _arquivoAtual = null;
            Text = TituloPadrao;
            RegistrarNoHistorico(new EntradaHistorico(null, texto));
        }

        private void LimparSelecaoArvore()
        {
            _ignorarSelecao = true;
            try
            {
                trvPastas.SelectedNode = null;
            }
            finally
            {
                _ignorarSelecao = false;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.Uri.StartsWith(PaginaPreview, StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
        }

        private async void AbrirLink(string href)
        {
            string caminho;
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile)
                {
                    if (uri.Scheme is "http" or "https" or "mailto")
                        AbrirComShell(href);
                    return;
                }

                caminho = uri.LocalPath;
            }
            else
            {
                if (_arquivoAtual == null)
                {
                    MessageBox.Show(this, "Não é possível abrir links relativos de um conteúdo sem arquivo de origem.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var relativo = href;
                var indiceCorte = relativo.IndexOfAny(['#', '?']);
                if (indiceCorte >= 0)
                    relativo = relativo[..indiceCorte];
                if (relativo.Length == 0)
                    return;

                relativo = Uri.UnescapeDataString(relativo).Replace('/', Path.DirectorySeparatorChar);

                try
                {
                    caminho = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_arquivoAtual)!, relativo));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    MessageBox.Show(this, $"Link inválido:\n{href}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (Directory.Exists(caminho))
            {
                AbrirComShell(caminho);
                return;
            }

            if (!File.Exists(caminho))
            {
                MessageBox.Show(this, $"Arquivo não encontrado:\n{caminho}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ExtensoesSuportadas.Contains(Path.GetExtension(caminho)))
            {
                AbrirComShell(caminho);
                return;
            }

            if (SelecionarNaArvore(caminho))
                return;

            LimparSelecaoArvore();
            await ExibirArquivoAsync(caminho);
        }

        private void AbrirComShell(string destino)
        {
            try
            {
                Process.Start(new ProcessStartInfo(destino) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir:\n{destino}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static (string AbrirLocal, string CopiarCaminho, string CopiarNome) TextosMenu(bool ehArquivo) =>
            ehArquivo
                ? ("Abrir local do arquivo", "Copiar caminho completo", "Copiar nome do arquivo")
                : ("Abrir pasta", "Copiar caminho completo", "Copiar nome da pasta");

        private void AbrirLocal(NoArvore no)
        {
            if (!no.EhArquivo)
            {
                AbrirComShell(no.Caminho);
                return;
            }

            try
            {
                Process.Start("explorer.exe", $"/select,\"{no.Caminho}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir:\n{no.Caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void CopiarCaminho(NoArvore no) => Clipboard.SetText(no.Caminho);

        private static void CopiarNome(NoArvore no)
        {
            var nome = Path.GetFileName(no.Caminho.TrimEnd(Path.DirectorySeparatorChar));
            Clipboard.SetText(string.IsNullOrEmpty(nome) ? no.Caminho : nome);
        }

        private void cmsArvore_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var no = cmsArvore.SourceControl == trvPastas && trvPastas.ClientRectangle.Contains(trvPastas.PointToClient(Cursor.Position))
                ? trvPastas.GetNodeAt(trvPastas.PointToClient(Cursor.Position))
                : trvPastas.SelectedNode;

            if (no?.Tag is not NoArvore info)
            {
                _noMenuContexto = null;
                e.Cancel = true;
                return;
            }

            _noMenuContexto = info;
            var textos = TextosMenu(info.EhArquivo);
            tsmAbrirLocal.Text = textos.AbrirLocal;
            tsmCopiarCaminho.Text = textos.CopiarCaminho;
            tsmCopiarNome.Text = textos.CopiarNome;
        }

        private void tsmAbrirLocal_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                AbrirLocal(_noMenuContexto);
        }

        private void tsmCopiarCaminho_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                CopiarCaminho(_noMenuContexto);
        }

        private void tsmCopiarNome_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                CopiarNome(_noMenuContexto);
        }

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            var ambiente = wvwPreview.CoreWebView2.Environment;
            var no = trvPastas.SelectedNode?.Tag as NoArvore;
            var textos = TextosMenu(no?.EhArquivo ?? true);

            CoreWebView2ContextMenuItem CriarItem(string texto, Action<NoArvore> acao)
            {
                var item = ambiente.CreateContextMenuItem(texto, null, CoreWebView2ContextMenuItemKind.Command);
                item.IsEnabled = no != null;
                item.CustomItemSelected += (_, _) => BeginInvoke(() => acao(no!));
                return item;
            }

            e.MenuItems.Add(ambiente.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
            e.MenuItems.Add(CriarItem(textos.AbrirLocal, AbrirLocal));
            e.MenuItems.Add(CriarItem(textos.CopiarCaminho, CopiarCaminho));
            e.MenuItems.Add(CriarItem(textos.CopiarNome, CopiarNome));
        }

        private void PosicionarArquivoSolto(string caminho)
        {
            _ignorarSelecao = true;
            try
            {
                if (!SelecionarNaArvore(caminho))
                    trvPastas.SelectedNode = null;
            }
            finally
            {
                _ignorarSelecao = false;
            }

            _arquivoAtual = caminho;
            Text = $"{Path.GetFileName(caminho)} - {TituloPadrao}";
            RegistrarNoHistorico(new EntradaHistorico(caminho, null));
        }

        private async Task<bool> ExibirArquivoAsync(string caminho, bool registrarHistorico = true)
        {
            try
            {
                var texto = await File.ReadAllTextAsync(caminho);
                var nome = Path.GetFileName(caminho);
                await RenderizarAsync(texto, nome);
                _arquivoAtual = caminho;
                Text = $"{nome} - {TituloPadrao}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir o arquivo:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (registrarHistorico)
                RegistrarNoHistorico(new EntradaHistorico(caminho, null));
            return true;
        }

        private async Task RenderizarAsync(string texto, string nome)
        {
            var script = $"renderContent({JsonSerializer.Serialize(texto)}, {JsonSerializer.Serialize(nome)});";
            await wvwPreview.ExecuteScriptAsync(script);
        }

        private static bool MesmaEntrada(EntradaHistorico? a, EntradaHistorico? b)
        {
            if (a == null || b == null)
                return false;

            if (a.Caminho != null || b.Caminho != null)
                return string.Equals(a.Caminho, b.Caminho, StringComparison.OrdinalIgnoreCase);

            return string.Equals(a.Texto, b.Texto, StringComparison.Ordinal);
        }

        private void RegistrarNoHistorico(EntradaHistorico entrada)
        {
            if (MesmaEntrada(entrada, _entradaAtual))
                return;

            if (_entradaAtual != null)
                Empilhar(_historicoVoltar, _entradaAtual);

            _historicoAvancar.Clear();
            _entradaAtual = entrada;
            AtualizarBotoesNavegacao();
        }

        private static void Empilhar(LinkedList<EntradaHistorico> pilha, EntradaHistorico entrada)
        {
            pilha.AddLast(entrada);
            while (pilha.Count > LimiteHistorico)
                pilha.RemoveFirst();
        }

        private void AtualizarBotoesNavegacao()
        {
            tsbVoltar.Enabled = _historicoVoltar.Count > 0;
            tsbVoltar.ToolTipText = _historicoVoltar.Last != null
                ? $"Voltar para {ObterNomeEntrada(_historicoVoltar.Last.Value)} (Alt+←)"
                : "Voltar (Alt+←)";

            tsbAvancar.Enabled = _historicoAvancar.Count > 0;
            tsbAvancar.ToolTipText = _historicoAvancar.Last != null
                ? $"Avançar para {ObterNomeEntrada(_historicoAvancar.Last.Value)} (Alt+→)"
                : "Avançar (Alt+→)";
        }

        private static string ObterNomeEntrada(EntradaHistorico entrada) =>
            entrada.Caminho != null ? Path.GetFileName(entrada.Caminho) : NomeTextoColado;

        private void tsbArvore_Click(object? sender, EventArgs e) => AlternarArvore();

        private void AlternarArvore()
        {
            if (spcPrincipal.Panel1Collapsed)
            {
                spcPrincipal.Panel1Collapsed = false;
                spcPrincipal.SplitterDistance = Math.Max(_larguraArvore, LarguraMinimaArvore);
                tsbArvore.Text = TextoEsconderArvore;
            }
            else
            {
                _larguraArvore = spcPrincipal.SplitterDistance;
                spcPrincipal.Panel1Collapsed = true;
                tsbArvore.Text = TextoMostrarArvore;
            }
        }

        // O arraste nativo do SplitContainer desenha a barra via GDI (XOR), que não aparece
        // sobre o WebView2; por isso o arraste é tratado aqui, redimensionando ao vivo.
        private void spcPrincipal_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && spcPrincipal.SplitterRectangle.Contains(e.Location))
                _deslocamentoArraste = e.X - spcPrincipal.SplitterDistance;
        }

        private void spcPrincipal_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_deslocamentoArraste is int deslocamento)
            {
                var maximo = spcPrincipal.Width - spcPrincipal.SplitterWidth - spcPrincipal.Panel2MinSize;
                var novaDistancia = Math.Clamp(e.X - deslocamento, spcPrincipal.Panel1MinSize, Math.Max(maximo, spcPrincipal.Panel1MinSize));

                if (novaDistancia != spcPrincipal.SplitterDistance)
                    spcPrincipal.SplitterDistance = novaDistancia;
            }

            AtualizarCursorDivisoria(e.Location);
        }

        private void spcPrincipal_MouseUp(object? sender, MouseEventArgs e)
        {
            _deslocamentoArraste = null;
            AtualizarCursorDivisoria(e.Location);
        }

        // O Cursor do SplitContainer é herdado pelos painéis filhos; ao sair para a árvore ou o
        // preview ele precisa voltar ao padrão, senão o VSplit continua aparecendo neles.
        private void spcPrincipal_MouseLeave(object? sender, EventArgs e)
        {
            if (_deslocamentoArraste == null)
                spcPrincipal.Cursor = Cursors.Default;
        }

        private void AtualizarCursorDivisoria(Point posicao)
        {
            spcPrincipal.Cursor = _deslocamentoArraste != null || spcPrincipal.SplitterRectangle.Contains(posicao)
                ? Cursors.VSplit
                : Cursors.Default;
        }

        private void spcPrincipal_MouseCaptureChanged(object? sender, EventArgs e) => _deslocamentoArraste = null;

        private void tsbVoltar_Click(object? sender, EventArgs e) => Voltar();

        private void tsbAvancar_Click(object? sender, EventArgs e) => Avancar();

        private async void Voltar() => await NavegarHistoricoAsync(_historicoVoltar, _historicoAvancar);

        private async void Avancar() => await NavegarHistoricoAsync(_historicoAvancar, _historicoVoltar);

        private async Task NavegarHistoricoAsync(LinkedList<EntradaHistorico> origem, LinkedList<EntradaHistorico> destino)
        {
            if (_navegandoHistorico || !_previewPronto || origem.Last == null)
                return;

            _navegandoHistorico = true;
            try
            {
                var entrada = origem.Last.Value;
                origem.RemoveLast();

                if (await ExibirEntradaAsync(entrada))
                {
                    if (_entradaAtual != null)
                        Empilhar(destino, _entradaAtual);
                    _entradaAtual = entrada;
                }
            }
            finally
            {
                _navegandoHistorico = false;
                AtualizarBotoesNavegacao();
            }
        }

        private async Task<bool> ExibirEntradaAsync(EntradaHistorico entrada)
        {
            if (entrada.Caminho != null)
            {
                _ignorarSelecao = true;
                try
                {
                    if (!SelecionarNaArvore(entrada.Caminho))
                        trvPastas.SelectedNode = null;
                }
                finally
                {
                    _ignorarSelecao = false;
                }

                return await ExibirArquivoAsync(entrada.Caminho, registrarHistorico: false);
            }

            LimparSelecaoArvore();
            await RenderizarAsync(entrada.Texto ?? string.Empty, NomeTextoColado);
            _arquivoAtual = null;
            Text = TituloPadrao;
            return true;
        }

        private static bool SistemaUsaTemaEscuro()
        {
            try
            {
                using var chave = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return chave?.GetValue("AppsUseLightTheme") is int valor && valor == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AplicarTema(bool escuro)
        {
            if (_temaEscuro == escuro)
                return;

            _temaEscuro = escuro;

            var corFundo = escuro ? CorFundoEscuro : CorFundoClaro;
            var corPainel = escuro ? CorPainelEscuro : CorPainelClaro;
            var corTexto = escuro ? CorTextoEscuro : CorTextoClaro;
            var corBorda = escuro ? CorBordaEscuro : CorBordaClaro;

            BackColor = corFundo;
            spcPrincipal.BackColor = corBorda;
            spcPrincipal.Panel1.BackColor = corPainel;
            spcPrincipal.Panel2.BackColor = corFundo;

            trvPastas.BackColor = corPainel;
            trvPastas.ForeColor = corTexto;
            trvPastas.LineColor = corTexto;

            tsrNavegacao.Renderer = new RenderizadorToolStrip(escuro);
            tsrNavegacao.BackColor = corPainel;
            tsrNavegacao.ForeColor = corTexto;

            cmsArvore.Renderer = new RenderizadorToolStrip(escuro);
            cmsArvore.BackColor = corPainel;
            cmsArvore.ForeColor = corTexto;

            wvwPreview.DefaultBackgroundColor = corFundo;

            AplicarTemaBarraTitulo();
            if (trvPastas.IsHandleCreated)
                SetWindowTheme(trvPastas.Handle, escuro ? "DarkMode_Explorer" : "Explorer", null);
        }

        private void AplicarTemaBarraTitulo()
        {
            if (!IsHandleCreated || _temaEscuro == null)
                return;

            var valor = _temaEscuro.Value ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref valor, sizeof(int));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AplicarTemaBarraTitulo();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (wvwPreview.Focused)
                return base.ProcessCmdKey(ref msg, keyData);

            switch (keyData)
            {
                case Keys.Alt | Keys.Left:
                case Keys.BrowserBack:
                    Voltar();
                    return true;

                case Keys.Alt | Keys.Right:
                case Keys.BrowserForward:
                    Avancar();
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_APPCOMMAND)
            {
                var comando = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF) & ~0xF000;
                if (comando == APPCOMMAND_BROWSER_BACKWARD)
                {
                    Voltar();
                    m.Result = 1;
                    return;
                }

                if (comando == APPCOMMAND_BROWSER_FORWARD)
                {
                    Avancar();
                    m.Result = 1;
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private sealed class RenderizadorToolStrip(bool escuro) : ToolStripProfessionalRenderer(new TabelaCoresToolStrip(escuro))
        {
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!e.Item.Enabled)
                {
                    TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle,
                        escuro ? CorDesabilitadoEscuro : CorDesabilitadoClaro, e.TextFormat);
                    return;
                }

                base.OnRenderItemText(e);
            }
        }

        private sealed class TabelaCoresToolStrip(bool escuro) : ProfessionalColorTable
        {
            private Color Painel => escuro ? CorPainelEscuro : CorPainelClaro;
            private Color Borda => escuro ? CorBordaEscuro : CorBordaClaro;
            private Color Destaque => escuro ? CorDestaqueEscuro : CorDestaqueClaro;
            private Color Pressionado => escuro ? CorPressionadoEscuro : CorPressionadoClaro;

            public override Color ToolStripGradientBegin => Painel;
            public override Color ToolStripGradientMiddle => Painel;
            public override Color ToolStripGradientEnd => Painel;
            public override Color ToolStripBorder => Borda;
            public override Color ButtonSelectedHighlight => Destaque;
            public override Color ButtonSelectedGradientBegin => Destaque;
            public override Color ButtonSelectedGradientMiddle => Destaque;
            public override Color ButtonSelectedGradientEnd => Destaque;
            public override Color ButtonSelectedBorder => Borda;
            public override Color ButtonPressedHighlight => Pressionado;
            public override Color ButtonPressedGradientBegin => Pressionado;
            public override Color ButtonPressedGradientMiddle => Pressionado;
            public override Color ButtonPressedGradientEnd => Pressionado;
            public override Color ButtonPressedBorder => Borda;
            public override Color SeparatorDark => Borda;
            public override Color SeparatorLight => Painel;
            public override Color ToolStripDropDownBackground => Painel;
            public override Color ImageMarginGradientBegin => Painel;
            public override Color ImageMarginGradientMiddle => Painel;
            public override Color ImageMarginGradientEnd => Painel;
            public override Color MenuBorder => Borda;
            public override Color MenuItemBorder => Borda;
            public override Color MenuItemSelected => Destaque;
            public override Color MenuItemSelectedGradientBegin => Destaque;
            public override Color MenuItemSelectedGradientEnd => Destaque;
        }
    }
}
