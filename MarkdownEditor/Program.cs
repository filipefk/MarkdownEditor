using System.Diagnostics;

namespace MarkdownEditor
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => MostrarErro(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Trace.TraceError($"Erro não tratado: {e.ExceptionObject}");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Ao abrir pelo duplo clique, o Windows passa o caminho do arquivo como primeiro argumento
            var arquivoInicial = args.FirstOrDefault(File.Exists);
            Application.Run(new frmEditor(arquivoInicial));
        }

        private static void MostrarErro(Exception ex)
        {
            Trace.TraceError(ex.ToString());
            MessageBox.Show($"Ocorreu um erro inesperado:\n\n{ex.Message}",
                "Markdown Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
