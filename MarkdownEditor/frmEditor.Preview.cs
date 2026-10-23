using Microsoft.Web.WebView2.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MarkdownEditor
{
    public partial class frmEditor
    {
        private async Task InicializarPreviewAsync()
        {
            try
            {
                var pastaDados = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkdownEditor", "WebView2");
                var ambiente = await CoreWebView2Environment.CreateAsync(userDataFolder: pastaDados);
                await wvwPreview.EnsureCoreWebView2Async(ambiente);

                // Desliga o que a página não usa (recomendação de segurança do WebView2).
                var configuracoes = wvwPreview.CoreWebView2.Settings;
                configuracoes.AreHostObjectsAllowed = false;
                configuracoes.IsStatusBarEnabled = false;
#if !DEBUG
                configuracoes.AreDevToolsEnabled = false;
#endif

                wvwPreview.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    HostPreview,
                    Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                    CoreWebView2HostResourceAccessKind.Allow);

                await wvwPreview.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(ScriptMonitorarEventos);
                wvwPreview.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                wvwPreview.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                wvwPreview.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                wvwPreview.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                wvwPreview.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                wvwPreview.CoreWebView2.Navigate(PaginaPreview);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                var resposta = MessageBox.Show(this,
                    "O Microsoft Edge WebView2 Runtime não está instalado, por isso a visualização não está disponível.\n\n" +
                    "Deseja abrir a página de download agora?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (resposta == DialogResult.Yes)
                    AbrirComShell(UrlDownloadWebView2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Não foi possível inicializar o visualizador (WebView2).\n" +
                    "Verifique se o Microsoft Edge WebView2 Runtime está instalado.\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
                return;

            _previewPronto = true;

            if (_arquivoPendente != null)
            {
                var caminho = _arquivoPendente;
                _arquivoPendente = null;
                await ExibirArquivoAsync(caminho);
            }
        }

        // O conteúdo exibido (Markdown com HTML embutido) não é confiável: só aceita mensagens da
        // página do visualizador e nunca executa nada a partir delas (ver AbrirLinkAsync).
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!e.Source.StartsWith(PaginaPreview, StringComparison.OrdinalIgnoreCase))
                return;

            string? mensagem;
            try
            {
                mensagem = e.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                return;
            }

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
                    var caminho = (e.AdditionalObjects.Count > 0 ? e.AdditionalObjects[0] : null) switch
                    {
                        FileInfo info => info.FullName,
                        CoreWebView2File arquivo => arquivo.Path,
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(caminho))
                        Disparar(PosicionarArquivoSoltoAsync(caminho));
                    break;

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
            CarregarNoEditor(texto, null, null);
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
            CarregarNoEditor(string.Empty, null, null);
        }

        // O HTML já exibiu o novo conteúdo por conta própria (colar, soltar, "Novo"); se o usuário
        // desistiu de descartar as alterações, a prévia volta a mostrar o texto do editor.
        private async Task RestaurarPreviaAsync()
        {
            if (!_previewPronto)
                return;

            await RenderizarAsync(sciEditor.Text, NomeConteudoAtual());
            _previaDesatualizada = false;
        }

        private void LimparSelecaoArvore()
        {
            _ignorarSelecao = true;
            try
            {
                trvPastas.SelectedNode = null;
            }
            finally
            {
                _ignorarSelecao = false;
            }
        }

        private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (!e.Uri.StartsWith(PaginaPreview, StringComparison.OrdinalIgnoreCase))
                e.Cancel = true;
        }

        private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
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
                        AbrirComShell(uri.AbsoluteUri);
                    return;
                }

                if (uri.IsUnc)
                {
                    MessageBox.Show(this, $"Links para caminhos de rede não são abertos:\n{href}",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                caminho = uri.LocalPath;
            }
            else
            {
                if (_arquivoAtual == null)
                {
                    MessageBox.Show(this, "Não é possível abrir links relativos de um conteúdo sem arquivo de origem.",
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, $"Link inválido:\n{href}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (Directory.Exists(caminho))
            {
                // Uma pasta existente: o shell só abre a pasta no Explorer.
                AbrirComShell(caminho);
                return;
            }

            if (!File.Exists(caminho))
            {
                MessageBox.Show(this, $"Arquivo não encontrado:\n{caminho}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ExtensoesSuportadas.Contains(Path.GetExtension(caminho)))
            {
                AbrirNoExplorer($"/select,\"{caminho}\"", caminho);
                return;
            }

            if (!await ConfirmarDescarteAsync())
                return;

            if (SelecionarNaArvore(caminho))
                return;

            LimparSelecaoArvore();
            await ExibirArquivoAsync(caminho);
        }

        private void AbrirComShell(string destino)
        {
            try
            {
                Process.Start(new ProcessStartInfo(destino) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir:\n{destino}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Abre o Explorer explicitamente (nunca o ShellExecute do próprio caminho, que executaria arquivos).
        private void AbrirNoExplorer(string argumentos, string caminho)
        {
            try
            {
                Process.Start("explorer.exe", argumentos);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Ponto único de "dispara e esquece": uma exceção da tarefa chega ao Application.ThreadException
        // (tratado em Program.cs) em vez de ser perdida.
        private static async void Disparar(Task tarefa) => await tarefa;

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            var ambiente = wvwPreview.CoreWebView2.Environment;
            var no = trvPastas.SelectedNode?.Tag as NoArvore;
            var textos = TextosMenu(no?.EhArquivo ?? true);

            CoreWebView2ContextMenuItem CriarItem(string texto, Action<NoArvore> acao)
            {
                var item = ambiente.CreateContextMenuItem(texto, null, CoreWebView2ContextMenuItemKind.Command);
                item.IsEnabled = no != null;
                item.CustomItemSelected += (_, _) => BeginInvoke(() => acao(no!));
                return item;
            }

            e.MenuItems.Add(ambiente.CreateContextMenuItem(string.Empty, null, CoreWebView2ContextMenuItemKind.Separator));
            e.MenuItems.Add(CriarItem(textos.AbrirLocal, AbrirLocal));
            e.MenuItems.Add(CriarItem(textos.CopiarCaminho, CopiarCaminho));
            e.MenuItems.Add(CriarItem(textos.CopiarNome, CopiarNome));
        }

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
                MessageBox.Show(this, $"Não foi possível abrir o arquivo:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await RestaurarPreviaAsync();
                return;
            }

            _ignorarSelecao = true;
            try
            {
                if (!SelecionarNaArvore(caminho))
                    trvPastas.SelectedNode = null;
            }
            finally
            {
                _ignorarSelecao = false;
            }

            DefinirArquivoAtual(caminho);
            CarregarNoEditor(texto, caminho, codificacao);
            RegistrarNoHistorico(new EntradaHistorico(caminho, null));
        }

        private async Task<bool> ExibirArquivoAsync(string caminho, bool registrarHistorico = true)
        {
            try
            {
                var (texto, codificacao) = await LerArquivoAsync(caminho);
                await RenderizarAsync(texto, Path.GetFileName(caminho));
                DefinirArquivoAtual(caminho);
                CarregarNoEditor(texto, caminho, codificacao);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Não foi possível abrir o arquivo:\n{caminho}\n\n{ex.Message}",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
