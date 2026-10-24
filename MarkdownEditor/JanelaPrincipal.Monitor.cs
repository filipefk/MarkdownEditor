using Microsoft.UI.Dispatching;
using System.Text;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
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
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
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

        // Os eventos chegam numa thread do pool; o tratamento volta para a thread da interface.
        // Editores costumam gerar vários eventos por gravação (ou excluir e renomear um temporário),
        // por isso o tratamento espera os eventos cessarem e só então confere o estado do arquivo.
        private void MonitorArquivo_Evento(object? sender, EventArgs e)
        {
            _filaInterface.TryEnqueue(() =>
            {
                if (sender == _monitorArquivo)
                    AgendarVerificacao();
            });
        }

        private void AgendarVerificacao()
        {
            _temporizadorRecarga.Stop();
            _temporizadorRecarga.Start();
        }

        private async void temporizadorRecarga_Tick(DispatcherQueueTimer sender, object args)
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

            if (_editorModificado)
            {
                var recarregar = await PerguntarSimNaoAsync(Textos.Formatar("ArquivoAlteradoFora", Path.GetFileName(caminho)));

                if (!string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!recarregar)
                {
                    _textoOriginal = texto;
                    _codificacaoAtual = codificacao;
                    return;
                }
            }

            await RecarregarNoEditorAsync(texto, codificacao);
            await RenderizarPreservandoRolagemAsync(texto, Path.GetFileName(caminho));
        }

        private async Task TratarArquivoExcluidoAsync(string caminho)
        {
            MonitorarArquivo(null);
            RemoverDaArvore(caminho);

            var arquivoExibido = string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase);
            var manterAlteracoes = arquivoExibido && _editorModificado;

            await AvisarAsync(
                Textos.Formatar("ArquivoExcluido", Path.GetFileName(caminho), caminho) +
                (manterAlteracoes ? "\n\n" + Textos.Obter("ArquivoExcluidoAlteracoes") : string.Empty));

            if (!arquivoExibido || !string.Equals(caminho, _arquivoAtual, StringComparison.OrdinalIgnoreCase))
                return;

            if (manterAlteracoes && _editorModificado)
            {
                DefinirArquivoAtual(null);
                _entradaAtual = null;
                AtualizarBotoesNavegacao();
                return;
            }

            await CarregarNoEditorAsync(string.Empty, null, null);
            await wvwPreview.ExecuteScriptAsync("newBtn.click();");
            DefinirArquivoAtual(null);
            _entradaAtual = null;
            AtualizarBotoesNavegacao();
        }
    }
}
