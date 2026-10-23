using System.Globalization;
using System.Text;
using EstiloEditor = ScintillaNET.Style;

namespace MarkdownEditor
{
    public partial class frmEditor
    {
        private void DefinirArquivoAtual(string? caminho)
        {
            var mesmoArquivo = caminho != null && string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase);
            _arquivoAtual = caminho;
            AtualizarTitulo();
            AtualizarBotaoSalvar();

            if (!mesmoArquivo || _monitorArquivo == null)
                MonitorarArquivo(caminho);
        }

        private void AtualizarTitulo()
        {
            var titulo = _arquivoAtual != null ? $"{Path.GetFileName(_arquivoAtual)} - {TituloPadrao}" : TituloPadrao;
            Text = sciEditor.Modified ? "*" + titulo : titulo;
        }

        private string NomeConteudoAtual() =>
            _arquivoAtual != null ? Path.GetFileName(_arquivoAtual) : NomeTextoColado;

        private void ConfigurarEditor()
        {
            sciEditor.Technology = ScintillaNET.Technology.DirectWrite;
            sciEditor.WrapMode = ScintillaNET.WrapMode.Word;
            sciEditor.ScrollWidthTracking = true;
            sciEditor.ScrollWidth = 1;
            sciEditor.TabWidth = 4;
            sciEditor.Margins[0].Type = ScintillaNET.MarginType.Number;
            for (var i = 1; i < sciEditor.Margins.Count; i++)
                sciEditor.Margins[i].Width = 0;

            DefinirLexer(null);
            AtualizarMargemNumeros();
        }

        private void DefinirLexer(string? caminho)
        {
            var extensao = caminho != null ? Path.GetExtension(caminho).ToLowerInvariant() : ".md";
            var lexer = extensao switch
            {
                ".json" => "json",
                ".xml" => "xml",
                ".txt" => "null",
                _ => "markdown"
            };

            if (sciEditor.LexerName != lexer)
            {
                try
                {
                    sciEditor.LexerName = lexer;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }

            AplicarEstilosEditor();
        }

        private void CarregarNoEditor(string texto, string? caminho, Encoding? codificacao)
        {
            _carregandoEditor = true;
            try
            {
                DefinirLexer(caminho);
                sciEditor.Text = texto;
                sciEditor.EmptyUndoBuffer();
                sciEditor.SetSavePoint();
            }
            finally
            {
                _carregandoEditor = false;
            }

            _textoOriginal = texto;
            _codificacaoAtual = codificacao ?? CodificacaoPadrao;
            _previaDesatualizada = false;
            AtualizarMargemNumeros();
            AtualizarBotaoSalvar();
        }

        // Usado quando o arquivo muda fora do programa: troca o texto mantendo cursor e rolagem.
        private void RecarregarNoEditor(string texto, Encoding codificacao)
        {
            var posicao = sciEditor.CurrentPosition;
            var primeiraLinha = sciEditor.FirstVisibleLine;

            CarregarNoEditor(texto, _arquivoAtual, codificacao);

            sciEditor.GotoPosition(Math.Min(posicao, sciEditor.TextLength));
            sciEditor.FirstVisibleLine = primeiraLinha;
        }

        private void AtualizarMargemNumeros()
        {
            var digitos = Math.Max(sciEditor.Lines.Count.ToString(CultureInfo.InvariantCulture).Length, 2);
            if (digitos == _digitosMargem)
                return;

            _digitosMargem = digitos;
            sciEditor.Margins[0].Width = sciEditor.TextWidth(EstiloEditor.LineNumber, new string('9', digitos + 1));
        }

        private void AtualizarBotaoSalvar()
        {
            // Texto colado (sem arquivo) pode ser salvo mesmo sem edição: abre o "Salvar como".
            tsbSalvar.Enabled = sciEditor.Modified || (_arquivoAtual == null && sciEditor.TextLength > 0);
            tsbSalvar.ToolTipText = _arquivoAtual == null ? "Salvar como... (Ctrl+S)" : "Salvar (Ctrl+S)";
            tsbDesfazer.Enabled = sciEditor.Modified;
            AtualizarTitulo();
        }

        private void sciEditor_TextChanged(object? sender, EventArgs e)
        {
            if (!_carregandoEditor)
                _previaDesatualizada = true;

            AtualizarMargemNumeros();
            AtualizarBotaoSalvar();
        }

        private void sciEditor_SavePointAlterado(object? sender, EventArgs e) => AtualizarBotaoSalvar();

        private async void tsbVisualizar_Click(object? sender, EventArgs e) => await AlternarModoAsync(false);

        private async void tsbEditar_Click(object? sender, EventArgs e) => await AlternarModoAsync(true);

        private async Task AlternarModoAsync(bool editar)
        {
            if (_modoEdicao == editar)
                return;

            _modoEdicao = editar;
            tsbVisualizar.Checked = !editar;
            tsbEditar.Checked = editar;
            tssSalvar.Visible = editar;
            tsbSalvar.Visible = editar;
            tsbDesfazer.Visible = editar;
            sciEditor.Visible = editar;
            wvwPreview.Visible = !editar;

            if (editar)
            {
                sciEditor.Focus();
                return;
            }

            wvwPreview.Focus();
            await AtualizarPreviaAsync();
        }

        private async Task AtualizarPreviaAsync()
        {
            if (!_previaDesatualizada || !_previewPronto)
                return;

            _previaDesatualizada = false;
            await RenderizarPreservandoRolagemAsync(sciEditor.Text, NomeConteudoAtual());
        }

        private async void tsbSalvar_Click(object? sender, EventArgs e) => await SalvarAsync();

        private void tsbDesfazer_Click(object? sender, EventArgs e) => DesfazerAlteracoes();

        // Volta ao último conteúdo salvo como uma única ação do undo do editor,
        // então um Ctrl+Z ainda recupera as edições descartadas.
        private void DesfazerAlteracoes()
        {
            if (!sciEditor.Modified)
                return;

            var posicao = sciEditor.CurrentPosition;
            var primeiraLinha = sciEditor.FirstVisibleLine;

            sciEditor.Text = _textoOriginal;
            sciEditor.SetSavePoint();

            sciEditor.GotoPosition(Math.Min(posicao, sciEditor.TextLength));
            sciEditor.FirstVisibleLine = primeiraLinha;
            sciEditor.Focus();
        }

        private async Task<bool> SalvarAsync(bool atualizarPrevia = true)
        {
            if (_salvando)
                return false;

            _salvando = true;
            try
            {
                if (_arquivoAtual == null)
                    return await SalvarComoAsync(atualizarPrevia);

                var texto = sciEditor.Text;
                if (!await GravarArquivoAsync(_arquivoAtual, texto, _codificacaoAtual))
                    return false;

                _textoOriginal = texto;
                sciEditor.SetSavePoint();
                AtualizarBotaoSalvar();
                if (atualizarPrevia)
                    await AtualizarPreviaAsync();
                return true;
            }
            finally
            {
                _salvando = false;
            }
        }

        private async Task<bool> SalvarComoAsync(bool atualizarPrevia)
        {
            using var dialogo = new SaveFileDialog
            {
                Title = "Salvar como",
                Filter = FiltroSalvar,
                DefaultExt = "md",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = PastaInicialSalvar()
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
                return false;

            var caminho = dialogo.FileName;
            var texto = sciEditor.Text;
            if (!await GravarArquivoAsync(caminho, texto, _codificacaoAtual))
                return false;

            _textoOriginal = texto;
            sciEditor.SetSavePoint();
            DefinirLexer(caminho);
            DefinirArquivoAtual(caminho);
            _entradaAtual = new EntradaHistorico(caminho, null);
            AtualizarBotoesNavegacao();
            MostrarNaArvore(caminho);

            if (atualizarPrevia && _previewPronto)
            {
                _previaDesatualizada = false;
                await RenderizarPreservandoRolagemAsync(texto, Path.GetFileName(caminho));
            }

            return true;
        }

        private string PastaInicialSalvar()
        {
            if (trvPastas.SelectedNode?.Tag is NoArvore info)
            {
                var pasta = info.EhArquivo ? Path.GetDirectoryName(info.Caminho) : info.Caminho;
                if (!string.IsNullOrEmpty(pasta) && Directory.Exists(pasta))
                    return pasta;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private async Task<bool> GravarArquivoAsync(string caminho, string texto, Encoding codificacao)
        {
            try
            {
                try
                {
                    await GravarComSubstituicaoAsync(caminho, texto, codificacao);
                }
                catch (Exception ex) when (EhErroDeGravacao(ex))
                {
                    // Pasta sem permissão para criar o temporário ou sistema de arquivos sem suporte
                    // à substituição: grava direto no arquivo, como antes.
                    await File.WriteAllTextAsync(caminho, texto, codificacao);
                }
                return true;
            }
            catch (Exception ex) when (EhErroDeGravacao(ex))
            {
                MessageBox.Show(this, $"Não foi possível salvar o arquivo:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        // Grava num temporário na mesma pasta e só então troca pelo arquivo final: uma falha no meio
        // da gravação (disco cheio, queda de energia) não deixa o arquivo original truncado.
        private static async Task GravarComSubstituicaoAsync(string caminho, string texto, Encoding codificacao)
        {
            var pasta = Path.GetDirectoryName(Path.GetFullPath(caminho))!;
            var temporario = Path.Combine(pasta, $"~{Path.GetFileName(caminho)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(temporario, texto, codificacao);

                if (File.Exists(caminho))
                    File.Replace(temporario, caminho, null, ignoreMetadataErrors: true);
                else
                    File.Move(temporario, caminho);
            }
            finally
            {
                try
                {
                    File.Delete(temporario);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        private static bool EhErroDeGravacao(Exception ex) =>
            ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or PlatformNotSupportedException;

        // Retorna false se o usuário cancelar (ou se o salvamento não acontecer).
        // "Não" descarta as alterações, voltando o editor ao conteúdo original.
        private async Task<bool> ConfirmarDescarteAsync()
        {
            if (!sciEditor.Modified)
                return true;

            var resposta = MessageBox.Show(this,
                $"Deseja salvar as alterações em \"{NomeConteudoAtual()}\"?",
                Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            switch (resposta)
            {
                case DialogResult.Yes:
                    return await SalvarAsync(atualizarPrevia: false);

                case DialogResult.No:
                    CarregarNoEditor(_textoOriginal, _arquivoAtual, _codificacaoAtual);
                    _previaDesatualizada = true;
                    return true;

                default:
                    return false;
            }
        }

        private async void frmEditor_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_fechamentoConfirmado || !sciEditor.Modified)
                return;

            e.Cancel = true;
            if (!await ConfirmarDescarteAsync())
                return;

            _fechamentoConfirmado = true;
            Close();
        }
    }
}
