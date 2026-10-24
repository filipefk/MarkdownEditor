using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Windows.Graphics;

namespace MarkdownEditor
{
    // O monitor de arquivo é liberado no Closed (a fila de diálogos vive com o processo); Window não é IDisposable.
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
    public sealed partial class JanelaPrincipal : Window
    {
        private const string HostPreview = "preview.local";
        private const string PaginaPreview = "https://preview.local/preview-md-json-xml.html";
        private const string PaginaEditor = "https://preview.local/editor.html";
        private const string UrlDownloadWebView2 = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
        private const string TituloPadrao = "Visualizador e editor de Markdown";
        private const string NomeAplicativo = "Markdown Editor";
        private const string MensagemArquivoSolto = "arquivoSolto";
        private const string PrefixoMensagemTextoColado = "textoColado:";
        private const string PrefixoMensagemLink = "abrirLink:";
        private const string MensagemNavegarVoltar = "navegar:voltar";
        private const string MensagemNavegarAvancar = "navegar:avancar";
        private const string PrefixoMensagemTema = "tema:";
        private const string MensagemNovo = "novo";
        private const string MensagemEditorPronto = "pronto";
        private const string PrefixoMensagemEditorAlterado = "alterado:";
        private const string MensagemEditorSalvar = "salvar";
        private const int IntervaloRecargaMs = 300;
        private const int MaximoTentativasRecarga = 5;
        private const string TemaEscuro = "dark";
        private const string NomeTextoColado = "texto colado";
        private const int LimiteHistorico = 10;
        private const int LarguraMinimaArvore = 50;
        private const int LarguraInicial = 1100;
        private const int AlturaInicial = 680;
        private const int LarguraMinima = 573;
        private const int AlturaMinima = 276;

        private const string ScriptMonitorarEventos = """
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

        private static readonly HashSet<string> ExtensoesSuportadas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".markdown", ".json", ".xml", ".txt"
        };

        private sealed record EntradaHistorico(string? Caminho, string? Texto);

        private bool _previewPronto;
        private string? _arquivoPendente;
        private string? _arquivoAtual;
        private bool _ignorarSelecao;
        private TreeViewNode? _noSelecionado;

        private readonly LinkedList<EntradaHistorico> _historicoVoltar = new();
        private readonly LinkedList<EntradaHistorico> _historicoAvancar = new();
        private EntradaHistorico? _entradaAtual;
        private bool _navegandoHistorico;

        private double _larguraArvore;
        private bool _arvoreOculta;

        private bool? _temaEscuro;

        private ItemArvore? _itemMenuContexto;
        private TreeViewNode? _noMenuContexto;

        private FileSystemWatcher? _monitorArquivo;
        // Guardada no construtor: a propriedade DispatcherQueue da janela só pode ser lida na thread da interface.
        private readonly DispatcherQueue _filaInterface;
        private readonly DispatcherQueueTimer _temporizadorRecarga;
        private bool _verificandoArquivo;
        private int _tentativasRecarga;

        private static readonly Encoding CodificacaoPadrao = new UTF8Encoding(false);

        private bool _modoEdicao;
        private bool _previaDesatualizada;
        private bool _salvando;
        private bool _fechamentoConfirmado;
        // Último conteúdo conhecido em disco (ou o texto colado original); serve para descartar
        // alterações e para distinguir uma alteração externa do nosso próprio salvamento.
        private string _textoOriginal = string.Empty;
        private Encoding _codificacaoAtual = CodificacaoPadrao;

        private readonly string? _arquivoInicial;

        public JanelaPrincipal(string? arquivoInicial)
        {
            InitializeComponent();
            _arquivoInicial = arquivoInicial;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(tlbTitulo);
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Markdown.ico"));
            AppWindow.Closing += AppWindow_Closing;
            Closed += JanelaPrincipal_Closed;

            _filaInterface = DispatcherQueue;
            _temporizadorRecarga = _filaInterface.CreateTimer();
            _temporizadorRecarga.Interval = TimeSpan.FromMilliseconds(IntervaloRecargaMs);
            _temporizadorRecarga.IsRepeating = false;
            _temporizadorRecarga.Tick += temporizadorRecarga_Tick;

            ConfigurarMenuArvore();
            // Voltar/Avançar do Windows (teclas e botões laterais do mouse, que o Windows converte em GoBack/GoForward).
            // O parser de XAML não converte "GoBack"/"GoForward" em VirtualKey.
            abbVoltar.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.GoBack });
            abbAvancar.KeyboardAccelerators.Add(new KeyboardAccelerator { Key = Windows.System.VirtualKey.GoForward });
            grdRaiz.Loaded += grdRaiz_Loaded;
            ConfigurarArrastarSoltar();

            AplicarTema(SistemaUsaTemaEscuro());
        }

        private async void grdRaiz_Loaded(object sender, RoutedEventArgs e)
        {
            grdRaiz.Loaded -= grdRaiz_Loaded;
            AjustarTamanhoJanela();
            IconesShell.Escala = grdRaiz.XamlRoot?.RasterizationScale ?? 1.0;
            CarregarRaizes();
            AtualizarBotoesNavegacao();
            AtualizarTitulo();

            if (_arquivoInicial != null)
            {
                _arquivoPendente = _arquivoInicial;
                SelecionarNaArvore(_arquivoInicial);
            }

            await InicializarWebViewsAsync();
        }

        // O AppWindow trabalha em pixels físicos; os tamanhos são definidos em pixels lógicos (100%).
        private void AjustarTamanhoJanela()
        {
            var escala = grdRaiz.XamlRoot?.RasterizationScale ?? 1.0;
            AppWindow.Resize(new SizeInt32((int)(LarguraInicial * escala), (int)(AlturaInicial * escala)));
            if (AppWindow.Presenter is OverlappedPresenter apresentador)
            {
                apresentador.PreferredMinimumWidth = (int)(LarguraMinima * escala);
                apresentador.PreferredMinimumHeight = (int)(AlturaMinima * escala);
            }
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (_fechamentoConfirmado || !_editorModificado)
                return;

            args.Cancel = true;
            Disparar(FecharAposConfirmarAsync());
        }

        private async Task FecharAposConfirmarAsync()
        {
            if (!await ConfirmarDescarteAsync())
                return;

            _fechamentoConfirmado = true;
            Close();
        }

        private void JanelaPrincipal_Closed(object sender, WindowEventArgs args)
        {
            MonitorarArquivo(null);
            _temporizadorRecarga.Stop();
        }

        // Ponto único de "dispara e esquece": uma exceção da tarefa chega ao Application.UnhandledException
        // (tratado em App.xaml.cs) em vez de ser perdida.
        private static async void Disparar(Task tarefa) => await tarefa;
    }
}
