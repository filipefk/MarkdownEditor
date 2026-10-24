using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using DragEventArgs = Microsoft.UI.Xaml.DragEventArgs;
using DragEventHandler = Microsoft.UI.Xaml.DragEventHandler;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        private async Task InicializarWebViewsAsync()
        {
            if (!WebView2Instalado())
            {
                _editorPronto.TrySetResult(false);
                if (await PerguntarSimNaoAsync(Textos.Obter("WebView2Ausente")))
                    await AbrirComShellAsync(UrlDownloadWebView2);
                return;
            }

            try
            {
                // A pasta de dados (WEBVIEW2_USER_DATA_FOLDER) é definida no App, antes de existir qualquer WebView2.
                await Task.WhenAll(
                    wvwPreview.EnsureCoreWebView2Async().AsTask(),
                    wvwEditor.EnsureCoreWebView2Async().AsTask());

                var preview = wvwPreview.CoreWebView2;
                ConfigurarSeguranca(preview.Settings);
                preview.SetVirtualHostNameToFolderMapping(HostPreview, PastaWwwroot(), CoreWebView2HostResourceAccessKind.Allow);
                await preview.AddScriptToExecuteOnDocumentCreatedAsync(ScriptMonitorarEventos);
                await preview.AddScriptToExecuteOnDocumentCreatedAsync(ScriptIdiomaPrevia);
                preview.WebMessageReceived += CoreWebView2_WebMessageReceived;
                preview.NavigationCompleted += CoreWebView2_NavigationCompleted;
                preview.NavigationStarting += (_, e) => PermitirSomente(e, PaginaPreview);
                preview.NewWindowRequested += (_, e) => e.Handled = true;
                preview.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                preview.Navigate(PaginaPreview);

                InicializarEditor(wvwEditor.CoreWebView2);
            }
            catch (Exception ex)
            {
                _editorPronto.TrySetResult(false);
                await AvisarAsync(Textos.Formatar("ErroWebView2", ex.Message));
            }
        }

        private static bool WebView2Instalado()
        {
            try
            {
                return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string PastaWwwroot() => Path.Combine(AppContext.BaseDirectory, "wwwroot");

        // Desliga o que as páginas não usam (recomendação de segurança do WebView2).
        private static void ConfigurarSeguranca(CoreWebView2Settings configuracoes)
        {
            configuracoes.AreHostObjectsAllowed = false;
            configuracoes.IsStatusBarEnabled = false;
#if !DEBUG
            configuracoes.AreDevToolsEnabled = false;
#endif
        }

        private static void PermitirSomente(CoreWebView2NavigationStartingEventArgs e, string pagina)
        {
            if (!e.Uri.StartsWith(pagina, StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        }

        private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
                return;

            _previewPronto = true;
            await AplicarIdiomaNaPreviaAsync();

            if (_arquivoPendente != null)
            {
                var caminho = _arquivoPendente;
                _arquivoPendente = null;
                await ExibirArquivoAsync(caminho);
            }
        }

        // O conteúdo exibido (Markdown com HTML embutido) não é confiável: só aceita mensagens da
        // página do visualizador e nunca executa nada a partir delas (ver AbrirLinkAsync).
        private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!e.Source.StartsWith(PaginaPreview, StringComparison.OrdinalIgnoreCase))
                return;

            var mensagem = LerMensagem(e);

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemLink, StringComparison.Ordinal))
            {
                Disparar(AbrirLinkAsync(mensagem[PrefixoMensagemLink.Length..]));
                return;
            }

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemTextoColado, StringComparison.Ordinal))
            {
                Disparar(RegistrarTextoColadoAsync(mensagem[PrefixoMensagemTextoColado.Length..]));
                return;
            }

            if (mensagem != null && mensagem.StartsWith(PrefixoMensagemTema, StringComparison.Ordinal))
            {
                AplicarTema(mensagem[PrefixoMensagemTema.Length..] == TemaEscuro);
                return;
            }

            switch (mensagem)
            {
                case MensagemNavegarVoltar:
                    Voltar();
                    break;

                case MensagemNavegarAvancar:
                    Avancar();
                    break;

                case MensagemNovo:
                    Disparar(TratarNovoAsync());
                    break;

                case MensagemArquivoSolto:
                    var caminho = (e.AdditionalObjects.Count > 0 ? e.AdditionalObjects[0] : null) is CoreWebView2File arquivo
                        ? arquivo.Path
                        : null;
                    if (!string.IsNullOrEmpty(caminho))
                        Disparar(PosicionarArquivoSoltoAsync(caminho));
                    break;
            }
        }

        private static string? LerMensagem(CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                return e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private async Task RegistrarTextoColadoAsync(string texto)
        {
            if (!await ConfirmarDescarteAsync())
            {
                await RestaurarPreviaAsync();
                return;
            }

            LimparSelecaoArvore();
            DefinirArquivoAtual(null);
            await CarregarNoEditorAsync(texto, null, null);
            RegistrarNoHistorico(new EntradaHistorico(null, texto));
        }

        private async Task TratarNovoAsync()
        {
            if (!await ConfirmarDescarteAsync())
            {
                await RestaurarPreviaAsync();
                return;
            }

            LimparSelecaoArvore();
            DefinirArquivoAtual(null);
            await CarregarNoEditorAsync(string.Empty, null, null);
        }

        // O HTML já exibiu o novo conteúdo por conta própria (colar, soltar, "Novo"); se o usuário
        // desistiu de descartar as alterações, a prévia volta a mostrar o texto do editor.
        private async Task RestaurarPreviaAsync()
        {
            if (!_previewPronto)
                return;

            var (texto, _) = await ObterConteudoEditorAsync();
            await RenderizarAsync(texto, NomeConteudoAtual());
            _previaDesatualizada = false;
        }

        // Links vêm de conteúdo não confiável: web/e-mail abrem no aplicativo padrão; arquivos locais
        // nunca são executados. Formatos suportados abrem aqui; os demais só são mostrados no Explorer.
        private async Task AbrirLinkAsync(string href)
        {
            string caminho;
            if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile)
                {
                    if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
                        await AbrirComShellAsync(uri.AbsoluteUri);
                    return;
                }

                if (uri.IsUnc)
                {
                    await AvisarAsync(Textos.Formatar("LinkRede", href));
                    return;
                }

                caminho = uri.LocalPath;
            }
            else
            {
                if (_arquivoAtual == null)
                {
                    await AvisarAsync(Textos.Obter("LinkRelativoSemArquivo"));
                    return;
                }

                var relativo = href;
                var indiceCorte = relativo.IndexOfAny(['#', '?']);
                if (indiceCorte >= 0)
                    relativo = relativo[..indiceCorte];
                if (relativo.Length == 0)
                    return;

                relativo = Uri.UnescapeDataString(relativo).Replace('/', Path.DirectorySeparatorChar);

                try
                {
                    caminho = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(_arquivoAtual)!, relativo));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    await AvisarAsync(Textos.Formatar("LinkInvalido", href));
                    return;
                }
            }

            if (Directory.Exists(caminho))
            {
                // Uma pasta existente: o shell só abre a pasta no Explorer.
                await AbrirComShellAsync(caminho);
                return;
            }

            if (!File.Exists(caminho))
            {
                await AvisarAsync(Textos.Formatar("ArquivoNaoEncontrado", caminho));
                return;
            }

            if (!ExtensoesSuportadas.Contains(Path.GetExtension(caminho)))
            {
                await AbrirNoExplorerAsync($"/select,\"{caminho}\"", caminho);
                return;
            }

            if (!await ConfirmarDescarteAsync())
                return;

            if (!SelecionarNaArvore(caminho))
                LimparSelecaoArvore();
            await ExibirArquivoAsync(caminho);
        }

        private async Task AbrirComShellAsync(string destino)
        {
            try
            {
                Process.Start(new ProcessStartInfo(destino) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                await AvisarAsync(Textos.Formatar("ErroAbrir", destino, ex.Message));
            }
        }

        // Abre o Explorer explicitamente (nunca o ShellExecute do próprio caminho, que executaria arquivos).
        private async Task AbrirNoExplorerAsync(string argumentos, string caminho)
        {
            try
            {
                Process.Start("explorer.exe", argumentos);
            }
            catch (Exception ex)
            {
                await AvisarAsync(Textos.Formatar("ErroAbrir", caminho, ex.Message));
            }
        }

        private void CoreWebView2_ContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            var ambiente = sender.Environment;
            var item = trvPastas.SelectedNode?.Content as ItemArvore;
            var textos = TextosMenu(item?.EhArquivo ?? true);

            CoreWebView2ContextMenuItem CriarItem(string texto, Func<ItemArvore, Task> acao)
            {
                var itemMenu = ambiente.CreateContextMenuItem(texto, null, CoreWebView2ContextMenuItemKind.Command);
                itemMenu.IsEnabled = item != null;
                itemMenu.CustomItemSelected += (_, _) => DispatcherQueue.TryEnqueue(() => Disparar(acao(item!)));
                return itemMenu;
            }

            e.MenuItems.Add(ambiente.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
            e.MenuItems.Add(CriarItem(textos.AbrirLocal, AbrirLocalAsync));
            e.MenuItems.Add(CriarItem(textos.CopiarCaminho, CopiarCaminhoAsync));
            e.MenuItems.Add(CriarItem(textos.CopiarNome, CopiarNomeAsync));
        }

        // Arrastar arquivos do Explorer é tratado pelo WinUI (o WebView2 não repassa o arquivo à página de
        // forma confiável). handledEventsToo: funciona mesmo que o WebView2 marque o evento como tratado.
        private void ConfigurarArrastarSoltar()
        {
            grdRaiz.AllowDrop = true;
            grdRaiz.AddHandler(UIElement.DragOverEvent, new DragEventHandler(grdRaiz_DragOver), true);
            grdRaiz.AddHandler(UIElement.DropEvent, new DragEventHandler(grdRaiz_Drop), true);
        }

        private void grdRaiz_DragOver(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;

            e.AcceptedOperation = DataPackageOperation.Copy;
            if (e.DragUIOverride != null)
                e.DragUIOverride.Caption = Textos.Obter("ArrastarAbrir");
        }

        private async void grdRaiz_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
                return;

            // A lista de itens só pode ser lida durante o evento; o resto (diálogos) vem depois.
            IReadOnlyList<IStorageItem> itens;
            var adiamento = e.GetDeferral();
            try
            {
                itens = await e.DataView.GetStorageItemsAsync();
            }
            finally
            {
                adiamento.Complete();
            }

            if (itens.Count > 0 && itens[0] is StorageFile arquivo && !string.IsNullOrEmpty(arquivo.Path))
                await AbrirArquivoArrastadoAsync(arquivo.Path);
        }

        private async Task AbrirArquivoArrastadoAsync(string caminho)
        {
            if (!ExtensoesSuportadas.Contains(Path.GetExtension(caminho)))
            {
                await AvisarAsync(Textos.Formatar("FormatoNaoSuportado", caminho));
                await RestaurarPreviaAsync();
                return;
            }

            if (!await ConfirmarDescarteAsync())
            {
                await RestaurarPreviaAsync();
                return;
            }

            if (!SelecionarNaArvore(caminho))
                LimparSelecaoArvore();

            if (!_previewPronto)
            {
                _arquivoPendente = caminho;
                return;
            }

            await ExibirArquivoAsync(caminho);
        }

        // Arquivo escolhido pelo botão "Escolher arquivo" do HTML (que já o exibiu).
        private async Task PosicionarArquivoSoltoAsync(string caminho)
        {
            if (!await ConfirmarDescarteAsync())
            {
                await RestaurarPreviaAsync();
                return;
            }

            string texto;
            Encoding codificacao;
            try
            {
                (texto, codificacao) = await LerArquivoAsync(caminho);
            }
            catch (Exception ex)
            {
                await AvisarAsync(Textos.Formatar("ErroAbrirArquivo", caminho, ex.Message));
                await RestaurarPreviaAsync();
                return;
            }

            if (!SelecionarNaArvore(caminho))
                LimparSelecaoArvore();

            DefinirArquivoAtual(caminho);
            await CarregarNoEditorAsync(texto, caminho, codificacao);
            RegistrarNoHistorico(new EntradaHistorico(caminho, null));
        }

        private async Task<bool> ExibirArquivoAsync(string caminho, bool registrarHistorico = true)
        {
            try
            {
                var (texto, codificacao) = await LerArquivoAsync(caminho);
                await RenderizarAsync(texto, Path.GetFileName(caminho));
                DefinirArquivoAtual(caminho);
                await CarregarNoEditorAsync(texto, caminho, codificacao);
            }
            catch (Exception ex)
            {
                await AvisarAsync(Textos.Formatar("ErroAbrirArquivo", caminho, ex.Message));
                return false;
            }

            if (registrarHistorico)
                RegistrarNoHistorico(new EntradaHistorico(caminho, null));
            return true;
        }

        private async Task RenderizarAsync(string texto, string nome)
        {
            var script = $"renderContent({JsonSerializer.Serialize(texto)}, {JsonSerializer.Serialize(nome)});";
            await wvwPreview.ExecuteScriptAsync(script);
        }

        private async Task RenderizarPreservandoRolagemAsync(string texto, string nome)
        {
            var script = $$"""
                (() => {
                    const rolagem = stage.scrollTop;
                    renderContent({{JsonSerializer.Serialize(texto)}}, {{JsonSerializer.Serialize(nome)}});
                    requestAnimationFrame(() => { stage.scrollTop = rolagem; });
                })();
                """;
            await wvwPreview.ExecuteScriptAsync(script);
        }

        // Lê sem impedir que outro processo grave, renomeie ou exclua o arquivo durante a leitura.
        // A codificação parte de UTF-8 sem BOM; se houver BOM, a detectada é devolvida para regravar igual.
        private static async Task<(string Texto, Encoding Codificacao)> LerArquivoAsync(string caminho)
        {
            await using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var leitor = new StreamReader(fluxo, CodificacaoPadrao, detectEncodingFromByteOrderMarks: true);
            var texto = await leitor.ReadToEndAsync();
            return (texto, leitor.CurrentEncoding);
        }
    }
}
