using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        // Fundo do VS Code Light+/Dark+ (o mesmo do HTML): evita o "flash" branco/preto dos WebView2.
        private static readonly Color CorFundoEscuro = ColorHelper.FromArgb(255, 0x1E, 0x1E, 0x1E);
        private static readonly Color CorFundoClaro = ColorHelper.FromArgb(255, 0xFF, 0xFF, 0xFF);

        // Tema do Windows (o App.xaml não fixa RequestedTheme), usado até o HTML avisar o tema dele.
        private static bool SistemaUsaTemaEscuro() => Application.Current.RequestedTheme == ApplicationTheme.Dark;

        // O tema acompanha o do HTML: o conteúdo da janela (e com ele o Mica, a TitleBar, a árvore,
        // a CommandBar e os diálogos), os botões de legenda e o Monaco.
        private void AplicarTema(bool escuro)
        {
            if (_temaEscuro == escuro)
                return;

            _temaEscuro = escuro;

            grdRaiz.RequestedTheme = escuro ? ElementTheme.Dark : ElementTheme.Light;
            AppWindow.TitleBar.PreferredTheme = escuro ? TitleBarTheme.Dark : TitleBarTheme.Light;

            var fundo = escuro ? CorFundoEscuro : CorFundoClaro;
            wvwPreview.DefaultBackgroundColor = fundo;
            wvwEditor.DefaultBackgroundColor = fundo;

            Disparar(ExecutarNoEditorAsync($"definirTema({Js(escuro)})"));
        }
    }
}
