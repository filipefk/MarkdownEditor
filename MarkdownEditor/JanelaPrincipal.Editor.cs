using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Windows.Storage.Pickers;
using System.Text;
using System.Text.Json;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        private sealed record EstadoEditor(bool Modificado, bool Vazio);

        private sealed record ConteudoEditor(string Texto, int Versao);

        private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

        // Concluída quando o Monaco termina de carregar (false se o WebView2 não estiver disponível).
        private readonly TaskCompletionSource<bool> _editorPronto = new();

        // Espelho do estado do Monaco (o editor roda no WebView2 e avisa por mensagens).
        private bool _editorModificado;
        private bool _editorVazio = true;

        private void InicializarEditor(CoreWebView2 editor)
        {
            ConfigurarSeguranca(editor.Settings);
            // Sem F5 (recarregaria a página e perderia o texto), Ctrl+P etc.; o Monaco tem os próprios atalhos.
            editor.Settings.AreBrowserAcceleratorKeysEnabled = false;
            editor.SetVirtualHostNameToFolderMapping(HostPreview, PastaWwwroot(), CoreWebView2HostResourceAccessKind.Allow);
            editor.WebMessageReceived += Editor_WebMessageReceived;
            editor.NavigationStarting += (_, e) => PermitirSomente(e, PaginaEditor);
            editor.NewWindowRequested += (_, e) => e.Handled = true;
            editor.Navigate(PaginaEditor);
        }

        private void Editor_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!e.Source.StartsWith(PaginaEditor, StringComparison.OrdinalIgnoreCase))
                return;

            var mensagem = LerMensagem(e);
            if (mensagem == null)
                return;

            if (mensagem.StartsWith(PrefixoMensagemEditorAlterado, StringComparison.Ordinal))
            {
                _previaDesatualizada = true;
                AtualizarEstadoEditor(mensagem[PrefixoMensagemEditorAlterado.Length..]);
                return;
            }

            switch (mensagem)
            {
                case MensagemEditorPronto:
                    _editorPronto.TrySetResult(true);
                    break;

                case MensagemEditorSalvar:
                    if (_modoEdicao && abbSalvar.IsEnabled)
                        Disparar(SalvarAsync());
                    break;

                case MensagemNavegarVoltar:
                    Voltar();
                    break;

                case MensagemNavegarAvancar:
                    Avancar();
                    break;
            }
        }

        private async Task<string?> ExecutarNoEditorAsync(string script)
        {
            if (!await _editorPronto.Task)
                return null;

            return await wvwEditor.ExecuteScriptAsync(script);
        }

        private async Task ExecutarComEstadoAsync(string script)
        {
            var json = await ExecutarNoEditorAsync(script);
            if (json != null)
                AtualizarEstadoEditor(json);
        }

        private void AtualizarEstadoEditor(string json)
        {
            if (JsonSerializer.Deserialize<EstadoEditor>(json, OpcoesJson) is not { } estado)
                return;

            _editorModificado = estado.Modificado;
            _editorVazio = estado.Vazio;
            AtualizarBotaoSalvar();
        }

        // Sem editor disponível, devolve o conteúdo conhecido (não há como ter alterações).
        private async Task<(string Texto, int? Versao)> ObterConteudoEditorAsync()
        {
            var json = await ExecutarNoEditorAsync("obterConteudo()");
            var conteudo = json != null ? JsonSerializer.Deserialize<ConteudoEditor>(json, OpcoesJson) : null;
            return conteudo != null ? (conteudo.Texto, conteudo.Versao) : (_textoOriginal, null);
        }

        private static string Js(object? valor) => JsonSerializer.Serialize(valor);

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
            var tituloPadrao = Textos.Obter("TituloPadrao");
            var titulo = _arquivoAtual != null ? $"{Path.GetFileName(_arquivoAtual)} - {tituloPadrao}" : tituloPadrao;
            if (_editorModificado)
                titulo = "*" + titulo;

            Title = titulo;
            tlbTitulo.Title = titulo;
        }

        private string NomeConteudoAtual() =>
            _arquivoAtual != null ? Path.GetFileName(_arquivoAtual) : Textos.Obter("NomeTextoColado");

        private static string LinguagemEditor(string? caminho) =>
            (caminho != null ? Path.GetExtension(caminho).ToLowerInvariant() : ".md") switch
            {
                ".json" => "json",
                ".xml" => "xml",
                ".txt" => "plaintext",
                _ => "markdown"
            };

        // Todo conteúdo exibido também vai para o editor: zera o desfazer e marca o ponto salvo.
        private async Task CarregarNoEditorAsync(string texto, string? caminho, Encoding? codificacao)
        {
            _textoOriginal = texto;
            _codificacaoAtual = codificacao ?? CodificacaoPadrao;
            _previaDesatualizada = false;
            _editorModificado = false;
            _editorVazio = texto.Length == 0;
            AtualizarBotaoSalvar();

            await ExecutarComEstadoAsync($"carregar({Js(texto)}, {Js(LinguagemEditor(caminho))})");
        }

        // Usado quando o arquivo muda fora do programa: troca o texto mantendo cursor e rolagem.
        private async Task RecarregarNoEditorAsync(string texto, Encoding codificacao)
        {
            _textoOriginal = texto;
            _codificacaoAtual = codificacao;
            _previaDesatualizada = false;
            await ExecutarComEstadoAsync($"recarregar({Js(texto)})");
        }

        private void AtualizarBotaoSalvar()
        {
            // Texto colado (sem arquivo) pode ser salvo mesmo sem edição: abre o "Salvar como".
            abbSalvar.IsEnabled = _editorModificado || (_arquivoAtual == null && !_editorVazio);
            ToolTipService.SetToolTip(abbSalvar, Textos.Obter(_arquivoAtual == null ? "SalvarComoDica" : "SalvarDica"));
            abbDesfazer.IsEnabled = _editorModificado;
            AtualizarTitulo();
        }

        private void sgmModo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sgmModo != null)
                Disparar(AlternarModoAsync(sgmModo.SelectedIndex == 1));
        }

        private async Task AlternarModoAsync(bool editar)
        {
            if (_modoEdicao == editar)
                return;

            _modoEdicao = editar;
            if (sgmModo.SelectedIndex != (editar ? 1 : 0))
                sgmModo.SelectedIndex = editar ? 1 : 0;

            var visibilidadeEdicao = editar ? Visibility.Visible : Visibility.Collapsed;
            absSalvar.Visibility = visibilidadeEdicao;
            abbSalvar.Visibility = visibilidadeEdicao;
            abbDesfazer.Visibility = visibilidadeEdicao;
            wvwEditor.Visibility = visibilidadeEdicao;
            wvwPreview.Visibility = editar ? Visibility.Collapsed : Visibility.Visible;

            if (editar)
            {
                wvwEditor.Focus(FocusState.Programmatic);
                await ExecutarNoEditorAsync("focar()");
                return;
            }

            wvwPreview.Focus(FocusState.Programmatic);
            await AtualizarPreviaAsync();
        }

        private async Task AtualizarPreviaAsync()
        {
            if (!_previaDesatualizada || !_previewPronto)
                return;

            _previaDesatualizada = false;
            var (texto, _) = await ObterConteudoEditorAsync();
            await RenderizarPreservandoRolagemAsync(texto, NomeConteudoAtual());
        }

        private async void abbSalvar_Click(object sender, RoutedEventArgs e) => await SalvarAsync();

        private async void abbDesfazer_Click(object sender, RoutedEventArgs e) => await DesfazerAlteracoesAsync();

        // Volta ao último conteúdo salvo como uma única ação do desfazer do editor,
        // então um Ctrl+Z ainda recupera as edições descartadas.
        private async Task DesfazerAlteracoesAsync()
        {
            if (!_editorModificado)
                return;

            _previaDesatualizada = true;
            await ExecutarComEstadoAsync($"desfazerAlteracoes({Js(_textoOriginal)})");
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

                var (texto, versao) = await ObterConteudoEditorAsync();
                if (!await GravarArquivoAsync(_arquivoAtual, texto, _codificacaoAtual))
                    return false;

                _textoOriginal = texto;
                await MarcarSalvoAsync(versao);
                if (atualizarPrevia)
                    await AtualizarPreviaAsync();
                return true;
            }
            finally
            {
                _salvando = false;
            }
        }

        private async Task MarcarSalvoAsync(int? versao)
        {
            if (versao != null)
                await ExecutarComEstadoAsync($"marcarSalvo({Js(versao)})");
        }

        private async Task<bool> SalvarComoAsync(bool atualizarPrevia)
        {
            var seletor = new FileSavePicker(AppWindow.Id)
            {
                Title = Textos.Obter("SalvarComoTitulo"),
                SuggestedFileName = Textos.Obter("SemTitulo"),
                DefaultFileExtension = ".md",
                ShowOverwritePrompt = true,
                SuggestedFolder = PastaInicialSalvar()
            };
            seletor.FileTypeChoices.Add("Markdown", [".md", ".markdown"]);
            seletor.FileTypeChoices.Add("JSON", [".json"]);
            seletor.FileTypeChoices.Add("XML", [".xml"]);
            seletor.FileTypeChoices.Add(Textos.Obter("TipoTexto"), [".txt"]);

            var resultado = await seletor.PickSaveFileAsync();
            if (resultado == null || string.IsNullOrEmpty(resultado.Path))
                return false;

            var caminho = resultado.Path;
            var (texto, versao) = await ObterConteudoEditorAsync();
            if (!await GravarArquivoAsync(caminho, texto, _codificacaoAtual))
                return false;

            _textoOriginal = texto;
            await ExecutarNoEditorAsync($"definirLinguagem({Js(LinguagemEditor(caminho))})");
            await MarcarSalvoAsync(versao);
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
            if (trvPastas.SelectedNode?.Content is ItemArvore item)
            {
                var pasta = item.EhArquivo ? Path.GetDirectoryName(item.Caminho) : item.Caminho;
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
                await AvisarAsync(Textos.Formatar("ErroSalvar", caminho, ex.Message));
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
        // "Não salvar" descarta as alterações, voltando o editor ao conteúdo original.
        private async Task<bool> ConfirmarDescarteAsync()
        {
            if (!_editorModificado)
                return true;

            switch (await PerguntarSalvarAsync(Textos.Formatar("PerguntaSalvar", NomeConteudoAtual())))
            {
                case RespostaSalvar.Salvar:
                    return await SalvarAsync(atualizarPrevia: false);

                case RespostaSalvar.NaoSalvar:
                    await CarregarNoEditorAsync(_textoOriginal, _arquivoAtual, _codificacaoAtual);
                    _previaDesatualizada = true;
                    return true;

                default:
                    return false;
            }
        }
    }
}
