using System.Text;

namespace MarkdownEditor
{
    public partial class frmEditor
    {
        // Monitora apenas o arquivo exibido. O FileSystemWatcher usa notificações do Windows
        // (sem polling) e não abre o arquivo, portanto não o bloqueia para outros processos.
        private void MonitorarArquivo(string? caminho)
        {
            _temporizadorRecarga.Stop();
            _tentativasRecarga = 0;

            if (_monitorArquivo != null)
            {
                _monitorArquivo.EnableRaisingEvents = false;
                _monitorArquivo.Dispose();
                _monitorArquivo = null;
            }

            var pasta = caminho != null ? Path.GetDirectoryName(caminho) : null;
            if (string.IsNullOrEmpty(pasta))
                return;

            FileSystemWatcher? monitor = null;
            try
            {
                monitor = new FileSystemWatcher(pasta, Path.GetFileName(caminho!))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    SynchronizingObject = this
                };
                monitor.Changed += MonitorArquivo_Evento;
                monitor.Created += MonitorArquivo_Evento;
                monitor.Deleted += MonitorArquivo_Evento;
                monitor.Renamed += MonitorArquivo_Evento;
                monitor.Error += MonitorArquivo_Evento;
                monitor.EnableRaisingEvents = true;
                _monitorArquivo = monitor;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                monitor?.Dispose();
            }
        }

        // Editores costumam gerar vários eventos por gravação (ou excluir e renomear um temporário),
        // por isso o tratamento espera os eventos cessarem e só então confere o estado do arquivo.
        private void MonitorArquivo_Evento(object? sender, EventArgs e)
        {
            if (sender == _monitorArquivo)
                AgendarVerificacao();
        }

        private void AgendarVerificacao()
        {
            _temporizadorRecarga.Stop();
            _temporizadorRecarga.Start();
        }

        private async void temporizadorRecarga_Tick(object? sender, EventArgs e)
        {
            _temporizadorRecarga.Stop();
            if (_arquivoAtual == null || _monitorArquivo == null || !_previewPronto)
                return;

            if (_verificandoArquivo)
            {
                AgendarVerificacao();
                return;
            }

            var caminho = _arquivoAtual;
            _verificandoArquivo = true;
            try
            {
                if (File.Exists(caminho))
                    await RecarregarArquivoAsync(caminho);
                else
                    await TratarArquivoExcluidoAsync(caminho);
            }
            finally
            {
                _verificandoArquivo = false;
            }
        }

        private async Task RecarregarArquivoAsync(string caminho)
        {
            string texto;
            Encoding codificacao;
            try
            {
                (texto, codificacao) = await LerArquivoAsync(caminho);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (++_tentativasRecarga <= MaximoTentativasRecarga)
                    AgendarVerificacao();
                return;
            }

            _tentativasRecarga = 0;
            if (!string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase))
                return;

            // Conteúdo igual ao conhecido: nada mudou de fato (inclui o nosso próprio salvamento).
            if (string.Equals(texto, _textoOriginal, StringComparison.Ordinal))
                return;

            if (sciEditor.Modified)
            {
                var resposta = MessageBox.Show(this,
                    $"O arquivo \"{Path.GetFileName(caminho)}\" foi alterado fora do editor.\n\n" +
                    "Deseja recarregá-lo e descartar as suas alterações?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (!string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase))
                    return;

                if (resposta != DialogResult.Yes)
                {
                    _textoOriginal = texto;
                    _codificacaoAtual = codificacao;
                    return;
                }
            }

            RecarregarNoEditor(texto, codificacao);
            await RenderizarPreservandoRolagemAsync(texto, Path.GetFileName(caminho));
        }

        private async Task TratarArquivoExcluidoAsync(string caminho)
        {
            MonitorarArquivo(null);
            RemoverDaArvore(caminho);

            var arquivoExibido = string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase);
            var manterAlteracoes = arquivoExibido && sciEditor.Modified;

            MessageBox.Show(this,
                $"O arquivo \"{Path.GetFileName(caminho)}\" foi excluído ou movido:\n{caminho}" +
                (manterAlteracoes ? "\n\nAs alterações continuam no editor; use Salvar para gravá-las em um novo arquivo." : string.Empty),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

            if (!arquivoExibido || !string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase))
                return;

            if (manterAlteracoes && sciEditor.Modified)
            {
                DefinirArquivoAtual(null);
                _entradaAtual = null;
                AtualizarBotoesNavegacao();
                return;
            }

            CarregarNoEditor(string.Empty, null, null);
            await wvwPreview.ExecuteScriptAsync("newBtn.click();");
            DefinirArquivoAtual(null);
            _entradaAtual = null;
            AtualizarBotoesNavegacao();
        }
    }
}
