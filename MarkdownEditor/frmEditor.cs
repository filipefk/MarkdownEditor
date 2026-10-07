using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace MarkdownEditor
{
    public partial class frmEditor : Form
    {
        private const string HostPreview = "preview.local";
        private const string PaginaPreview = "https://preview.local/preview-md-json-xml.html";
        private const string TextoNoFicticio = "...";
        private const string TituloPadrao = "Visualizador e editor de Markdown";
        private const string MensagemArquivoSolto = "arquivoSolto";
        private const string MensagemTextoColado = "textoColado";

        private const string ScriptMonitorarEventos = """
            window.addEventListener('drop', (e) => {
                const arquivo = e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0];
                if (arquivo) window.chrome.webview.postMessageWithAdditionalObjects('arquivoSolto', [arquivo]);
            });
            window.addEventListener('paste', (e) => {
                const texto = (e.clipboardData || window.clipboardData).getData('text');
                if (texto && texto.trim().length > 0) window.chrome.webview.postMessage('textoColado');
            });
            """;

        private static readonly HashSet<string> ExtensoesSuportadas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".markdown", ".json", ".xml", ".txt"
        };

        private record NoArvore(string Caminho, bool EhArquivo);

        private bool _previewPronto;
        private string? _arquivoPendente;
        private bool _ignorarSelecao;

        private readonly string? _arquivoInicial;

        public frmEditor() : this(null)
        {
        }

        public frmEditor(string? arquivoInicial)
        {
            InitializeComponent();
            _arquivoInicial = arquivoInicial;
        }

        private async void frmEditor_Load(object? sender, EventArgs e)
        {
            CarregarIcones();
            CarregarRaizes();

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

                var atalhos = new (string Nome, string Caminho)[]
                {
                    ("Área de Trabalho", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                    ("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                    ("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
                };

                foreach (var (nome, caminho) in atalhos)
                {
                    if (!string.IsNullOrEmpty(caminho) && Directory.Exists(caminho))
                        trvPastas.Nodes.Add(CriarNoPasta(nome, caminho, "pasta"));
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

            switch (mensagem)
            {
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

                case MensagemTextoColado:
                    trvPastas.SelectedNode = null;
                    Text = TituloPadrao;
                    break;
            }
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

            Text = $"{Path.GetFileName(caminho)} - {TituloPadrao}";
        }

        private async Task ExibirArquivoAsync(string caminho)
        {
            try
            {
                var texto = await File.ReadAllTextAsync(caminho);
                var nome = Path.GetFileName(caminho);
                var script = $"renderContent({JsonSerializer.Serialize(texto)}, {JsonSerializer.Serialize(nome)});";
                await wvwPreview.ExecuteScriptAsync(script);
                Text = $"{nome} - {TituloPadrao}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir o arquivo:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
