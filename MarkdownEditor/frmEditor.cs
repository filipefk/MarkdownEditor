using System.Text;

namespace MarkdownEditor
{
    public partial class frmEditor : Form
    {
        private const string HostPreview = "preview.local";
        private const string PaginaPreview = "https://preview.local/preview-md-json-xml.html";
        private const string UrlDownloadWebView2 = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
        private const string TextoNoFicticio = "...";
        private const string TituloPadrao = "Visualizador e editor de Markdown";
        private const string MensagemArquivoSolto = "arquivoSolto";
        private const string PrefixoMensagemTextoColado = "textoColado:";
        private const string PrefixoMensagemLink = "abrirLink:";
        private const string MensagemNavegarVoltar = "navegar:voltar";
        private const string MensagemNavegarAvancar = "navegar:avancar";
        private const string PrefixoMensagemTema = "tema:";
        private const string MensagemNovo = "novo";
        private const int IntervaloRecargaMs = 300;
        private const int MaximoTentativasRecarga = 5;
        private const string TemaEscuro = "dark";
        private const string NomeTextoColado = "texto colado";
        private const int LimiteHistorico = 10;
        private const int LarguraMinimaArvore = 50;
        private const string TextoEsconderArvore = "Esconder árvore";
        private const string TextoMostrarArvore = "Mostrar árvore";
        private const string FiltroSalvar =
            "Markdown (*.md;*.markdown)|*.md;*.markdown|JSON (*.json)|*.json|XML (*.xml)|*.xml|Texto (*.txt)|*.txt";
        private const string FonteEditor = "Consolas";
        private const int TamanhoFonteEditor = 10;
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
            document.addEventListener('click', (e) => {
                if (e.target instanceof Element && e.target.closest('#newBtn')) window.chrome.webview.postMessage('novo');
            }, true);
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

        private static readonly HashSet<string> ExtensoesSuportadas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".markdown", ".json", ".xml", ".txt"
        };

        private sealed record NoArvore(string Caminho, bool EhArquivo);

        private sealed record EntradaHistorico(string? Caminho, string? Texto);

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
        private TreeNode? _treeNodeMenuContexto;

        private FileSystemWatcher? _monitorArquivo;
        private readonly System.Windows.Forms.Timer _temporizadorRecarga;
        private bool _verificandoArquivo;
        private int _tentativasRecarga;

        private static readonly Encoding CodificacaoPadrao = new UTF8Encoding(false);

        private bool _modoEdicao;
        private bool _carregandoEditor;
        private bool _previaDesatualizada;
        private bool _salvando;
        private bool _fechamentoConfirmado;
        private int _digitosMargem;
        // Último conteúdo conhecido em disco (ou o texto colado original); serve para descartar
        // alterações e para distinguir uma alteração externa do nosso próprio salvamento.
        private string _textoOriginal = string.Empty;
        private Encoding _codificacaoAtual = CodificacaoPadrao;

        private readonly string? _arquivoInicial;

        public frmEditor() : this(null)
        {
        }

        public frmEditor(string? arquivoInicial)
        {
            InitializeComponent();
            spcPrincipal.Panel1MinSize = LarguraMinimaArvore;
            _arquivoInicial = arquivoInicial;
            _temporizadorRecarga = new System.Windows.Forms.Timer { Interval = IntervaloRecargaMs };
            _temporizadorRecarga.Tick += temporizadorRecarga_Tick;
            ConfigurarVisual();
            ConfigurarEditor();
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AplicarTemaBarraTitulo();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            MonitorarArquivo(null);
            _temporizadorRecarga.Dispose();
            base.OnFormClosed(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (wvwPreview.Focused)
                return base.ProcessCmdKey(ref msg, keyData);

            if (keyData == (Keys.Control | Keys.S) && _modoEdicao)
            {
                if (tsbSalvar.Enabled)
                    _ = SalvarAsync();
                return true;
            }

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
    }
}
