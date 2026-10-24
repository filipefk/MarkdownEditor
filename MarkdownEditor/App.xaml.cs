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
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownEditor", "WebView2"));

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
