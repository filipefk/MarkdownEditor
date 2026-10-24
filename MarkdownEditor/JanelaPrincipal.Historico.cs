using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
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
            abbVoltar.IsEnabled = _historicoVoltar.Count > 0;
            ToolTipService.SetToolTip(abbVoltar, _historicoVoltar.Last != null
                ? Textos.Formatar("VoltarParaDica", ObterNomeEntrada(_historicoVoltar.Last.Value))
                : Textos.Obter("VoltarDica"));

            abbAvancar.IsEnabled = _historicoAvancar.Count > 0;
            ToolTipService.SetToolTip(abbAvancar, _historicoAvancar.Last != null
                ? Textos.Formatar("AvancarParaDica", ObterNomeEntrada(_historicoAvancar.Last.Value))
                : Textos.Obter("AvancarDica"));
        }

        private static string ObterNomeEntrada(EntradaHistorico entrada) =>
            entrada.Caminho != null ? Path.GetFileName(entrada.Caminho) : Textos.Obter("NomeTextoColado");

        private void abbVoltar_Click(object sender, RoutedEventArgs e) => Voltar();

        private void abbAvancar_Click(object sender, RoutedEventArgs e) => Avancar();

        private void Voltar() => Disparar(NavegarHistoricoAsync(_historicoVoltar, _historicoAvancar));

        private void Avancar() => Disparar(NavegarHistoricoAsync(_historicoAvancar, _historicoVoltar));

        private async Task NavegarHistoricoAsync(LinkedList<EntradaHistorico> origem, LinkedList<EntradaHistorico> destino)
        {
            if (_navegandoHistorico || !_previewPronto || origem.Last == null)
                return;

            _navegandoHistorico = true;
            try
            {
                if (!await ConfirmarDescarteAsync() || origem.Last == null)
                    return;

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
                if (!SelecionarNaArvore(entrada.Caminho))
                    LimparSelecaoArvore();

                return await ExibirArquivoAsync(entrada.Caminho, registrarHistorico: false);
            }

            LimparSelecaoArvore();
            await RenderizarAsync(entrada.Texto ?? string.Empty, Textos.Obter("NomeTextoColado"));
            DefinirArquivoAtual(null);
            await CarregarNoEditorAsync(entrada.Texto ?? string.Empty, null, null);
            return true;
        }
    }
}
