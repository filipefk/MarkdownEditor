using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace MarkdownEditor
{
    public partial class App : Application
    {
        private JanelaPrincipal? _janela;

        public App()
        {
            // Dados dos WebView2 (os dois compartilham a pasta e o processo do navegador). Sem isso, o padrão
            // de um app sem pacote é ao lado do .exe, em Program Files, onde não há permissão de escrita.
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(Configuracoes.PastaDados, "WebView2"));

            // Idioma escolhido pelo usuário (ou o do Windows), antes de criar qualquer elemento do WinUI:
            // o PrimaryLanguageOverride vale também para os textos internos dos controles.
            var idioma = Configuracoes.Carregar().Idioma ?? Textos.IdiomaDoWindows();
            Textos.DefinirIdioma(idioma);
            DefinirIdiomaDoWinUI(Textos.IdiomaAtual);

            InitializeComponent();
            UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Trace.TraceError($"Erro não tratado: {e.ExceptionObject}");
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Ao abrir pelo duplo clique, o Windows passa o caminho do arquivo como primeiro argumento
            var arquivoInicial = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault(File.Exists);
            _janela = new JanelaPrincipal(arquivoInicial);
            _janela.Activate();
        }

        // Só tem efeito completo na próxima abertura (os controles já criados mantêm os textos internos).
        public static void DefinirIdiomaDoWinUI(string idioma)
        {
            try
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = idioma;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        // Exceções de event handlers e de tarefas disparadas por JanelaPrincipal.Disparar chegam aqui.
        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Trace.TraceError(e.Exception.ToString());
            if (_janela == null)
                return;

            e.Handled = true;
            _janela.MostrarErroInesperado(e.Exception);
        }
    }
}
