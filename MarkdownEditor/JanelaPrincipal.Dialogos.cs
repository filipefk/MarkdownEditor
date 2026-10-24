using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        private enum RespostaSalvar
        {
            Salvar,
            NaoSalvar,
            Cancelar
        }

        // O WinUI só permite um ContentDialog aberto por vez na janela: os pedidos esperam a vez.
        private readonly SemaphoreSlim _filaDialogos = new(1, 1);

        private async Task<ContentDialogResult> MostrarDialogoAsync(string mensagem, string? primario, string? secundario,
            string fechar, ContentDialogButton padrao)
        {
            await _filaDialogos.WaitAsync();
            try
            {
                var dialogo = new ContentDialog
                {
                    XamlRoot = grdRaiz.XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    RequestedTheme = grdRaiz.RequestedTheme,
                    Title = NomeAplicativo,
                    Content = new TextBlock
                    {
                        Text = mensagem,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    },
                    PrimaryButtonText = primario ?? string.Empty,
                    SecondaryButtonText = secundario ?? string.Empty,
                    CloseButtonText = fechar,
                    DefaultButton = padrao
                };
                return await dialogo.ShowAsync();
            }
            finally
            {
                _filaDialogos.Release();
            }
        }

        private async Task AvisarAsync(string mensagem) =>
            await MostrarDialogoAsync(mensagem, null, null, Textos.Obter("Ok"), ContentDialogButton.Close);

        private async Task<bool> PerguntarSimNaoAsync(string mensagem) =>
            await MostrarDialogoAsync(mensagem, Textos.Obter("Sim"), null, Textos.Obter("Nao"), ContentDialogButton.Primary)
                == ContentDialogResult.Primary;

        private async Task<RespostaSalvar> PerguntarSalvarAsync(string mensagem) =>
            await MostrarDialogoAsync(mensagem, Textos.Obter("Salvar"), Textos.Obter("NaoSalvar"), Textos.Obter("Cancelar"),
                ContentDialogButton.Primary) switch
            {
                ContentDialogResult.Primary => RespostaSalvar.Salvar,
                ContentDialogResult.Secondary => RespostaSalvar.NaoSalvar,
                _ => RespostaSalvar.Cancelar
            };

        public void MostrarErroInesperado(Exception ex) => Disparar(MostrarErroInesperadoAsync(ex));

        private async Task MostrarErroInesperadoAsync(Exception ex)
        {
            try
            {
                await AvisarAsync(Textos.Formatar("ErroInesperado", ex.Message));
            }
            catch (Exception erroDialogo)
            {
                // Sem janela para mostrar o erro (ex.: fechando): fica só no Trace, para não entrar em laço.
                Trace.TraceError(erroDialogo.ToString());
            }
        }
    }
}
