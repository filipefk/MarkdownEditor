using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        // Atualizar junto com o Monaco (wwwroot/monaco/VERSAO.txt).
        private const string VersaoMonaco = "0.52.2";
        private const string UrlFontes = "https://github.com/filipefk/MarkdownEditor";

        private async void abbSobre_Click(object sender, RoutedEventArgs e) => await MostrarSobreAsync();

        private async Task MostrarSobreAsync()
        {
            var assembly = typeof(App).Assembly;
            var produto = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? NomeAplicativo;
            var copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

            var informacoes = new (string Rotulo, string Valor)[]
            {
                (Textos.Obter("SobreVersao"), VersaoAplicativo(assembly)),
                (Textos.Obter("SobreArquitetura"), RuntimeInformation.ProcessArchitecture.ToString()),
                (".NET", RuntimeInformation.FrameworkDescription),
                ("Windows App SDK", VersaoWindowsAppSdk()),
                ("WebView2 Runtime", VersaoWebView2()),
                (Textos.Obter("SobreEditorMonaco"), VersaoMonaco),
                ("Windows", Environment.OSVersion.Version.ToString()),
                (Textos.Obter("SobreInstaladoEm"), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))
            };

            var conteudo = new StackPanel { Spacing = 16 };

            var cabecalho = new StackPanel { Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal, Spacing = 12 };
            cabecalho.Children.Add(new Image
            {
                Width = 48,
                Height = 48,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new BitmapImage(new Uri("ms-appx:///Assets/Markdown.ico"))
            });
            cabecalho.Children.Add(new TextBlock
            {
                Text = produto,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
            });
            conteudo.Children.Add(cabecalho);

            conteudo.Children.Add(new TextBlock
            {
                Text = Textos.Obter("DescricaoSobre"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });

            var grade = new Grid { ColumnSpacing = 16, RowSpacing = 4 };
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var i = 0; i < informacoes.Length; i++)
            {
                grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var rotulo = CriarTextoSecundario();
                rotulo.Text = informacoes[i].Rotulo;
                Grid.SetRow(rotulo, i);
                grade.Children.Add(rotulo);

                var valor = new TextBlock
                {
                    Text = informacoes[i].Valor,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                };
                Grid.SetRow(valor, i);
                Grid.SetColumn(valor, 1);
                grade.Children.Add(valor);
            }

            grade.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rotuloFontes = CriarTextoSecundario();
            rotuloFontes.Text = Textos.Obter("SobreFontes");
            rotuloFontes.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(rotuloFontes, informacoes.Length);
            grade.Children.Add(rotuloFontes);

            var linkFontes = new HyperlinkButton
            {
                Content = UrlFontes,
                NavigateUri = new Uri(UrlFontes),
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(linkFontes, UrlFontes);
            Grid.SetRow(linkFontes, informacoes.Length);
            Grid.SetColumn(linkFontes, 1);
            grade.Children.Add(linkFontes);

            conteudo.Children.Add(grade);

            if (!string.IsNullOrEmpty(copyright))
            {
                var textoCopyright = CriarTextoSecundario();
                textoCopyright.Text = copyright;
                textoCopyright.Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
                conteudo.Children.Add(textoCopyright);
            }

            var texto = new StringBuilder();
            texto.AppendLine(produto);
            foreach (var (rotulo, valor) in informacoes)
                texto.AppendLine($"{rotulo}: {valor}");
            texto.AppendLine($"{Textos.Obter("SobreFontes")}: {UrlFontes}");

            await _filaDialogos.WaitAsync();
            try
            {
                var dialogo = new ContentDialog
                {
                    XamlRoot = grdRaiz.XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    RequestedTheme = grdRaiz.RequestedTheme,
                    Title = Textos.Obter("Sobre"),
                    Content = conteudo,
                    PrimaryButtonText = Textos.Obter("SobreCopiarInformacoes"),
                    CloseButtonText = Textos.Obter("Ok"),
                    DefaultButton = ContentDialogButton.Close
                };
                // Copiar não fecha o diálogo; o próprio botão indica o resultado
                // (AvisarAsync aqui travaria, pois a fila de diálogos está ocupada).
                dialogo.PrimaryButtonClick += (s, args) =>
                {
                    args.Cancel = true;
                    s.PrimaryButtonText = Textos.Obter(CopiarInformacoes(texto.ToString()) ? "SobreCopiado" : "SobreNaoFoiPossivelCopiar");
                };
                await dialogo.ShowAsync();
            }
            finally
            {
                _filaDialogos.Release();
            }
        }

        // Application.Current.Resources[...] devolve o pincel do tema do aplicativo (o do Windows), fixo;
        // o {ThemeResource} é resolvido pelo tema efetivo do elemento (RequestedTheme do diálogo).
        private static TextBlock CriarTextoSecundario() => (TextBlock)XamlReader.Load(
            "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
            + "Foreground=\"{ThemeResource TextFillColorSecondaryBrush}\"/>");

        private static string VersaoAplicativo(Assembly assembly)
        {
            var versao = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? Textos.Obter("SobreDesconhecida");
            // O SDK pode acrescentar "+<hash do commit>" à versão informativa.
            var sufixo = versao.IndexOf('+', StringComparison.Ordinal);
            return sufixo >= 0 ? versao[..sufixo] : versao;
        }

        // Constantes geradas pelo pacote NuGet (WindowsAppSdkIncludeVersionInfo): como o Windows App SDK
        // vai junto do programa (self-contained), é a versão em uso. O ReleaseInfo (WinRT) falhava aqui.
        private static string VersaoWindowsAppSdk() =>
            $"{Microsoft.WindowsAppSDK.Release.Major}.{Microsoft.WindowsAppSDK.Release.Minor}.{Microsoft.WindowsAppSDK.Release.Patch}"
            + Microsoft.WindowsAppSDK.Release.FormattedVersionTag;

        private static string VersaoWebView2()
        {
            try
            {
                var versao = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return string.IsNullOrEmpty(versao) ? Textos.Obter("SobreNaoInstalado") : versao;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return Textos.Obter("SobreNaoInstalado");
            }
        }

        private static bool CopiarInformacoes(string texto)
        {
            try
            {
                var pacote = new DataPackage();
                pacote.SetText(texto);
                Clipboard.SetContent(pacote);
                Clipboard.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                return false;
            }
        }
    }
}
