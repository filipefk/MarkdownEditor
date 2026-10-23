namespace MarkdownEditor
{
    public partial class frmEditor
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
            tsbVoltar.Enabled = _historicoVoltar.Count > 0;
            tsbVoltar.ToolTipText = _historicoVoltar.Last != null
                ? $"Voltar para {ObterNomeEntrada(_historicoVoltar.Last.Value)} (Alt+←)"
                : "Voltar (Alt+←)";

            tsbAvancar.Enabled = _historicoAvancar.Count > 0;
            tsbAvancar.ToolTipText = _historicoAvancar.Last != null
                ? $"Avançar para {ObterNomeEntrada(_historicoAvancar.Last.Value)} (Alt+→)"
                : "Avançar (Alt+→)";
        }

        private static string ObterNomeEntrada(EntradaHistorico entrada) =>
            entrada.Caminho != null ? Path.GetFileName(entrada.Caminho) : NomeTextoColado;

        private void tsbVoltar_Click(object? sender, EventArgs e) => Voltar();

        private void tsbAvancar_Click(object? sender, EventArgs e) => Avancar();

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
                _ignorarSelecao = true;
                try
                {
                    if (!SelecionarNaArvore(entrada.Caminho))
                        trvPastas.SelectedNode = null;
                }
                finally
                {
                    _ignorarSelecao = false;
                }

                return await ExibirArquivoAsync(entrada.Caminho, registrarHistorico: false);
            }

            LimparSelecaoArvore();
            await RenderizarAsync(entrada.Texto ?? string.Empty, NomeTextoColado);
            DefinirArquivoAtual(null);
            CarregarNoEditor(entrada.Texto ?? string.Empty, null, null);
            return true;
        }
    }
}
