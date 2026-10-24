using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics.CodeAnalysis;
using Windows.ApplicationModel.DataTransfer;

namespace MarkdownEditor
{
    public sealed partial class JanelaPrincipal
    {
        private const string ChaveIconePasta = "pasta";

        // Glifos da Segoe Fluent Icons (iguais na Segoe MDL2 Assets do Windows 10).
        private const string GlifoPastaAberta = "\uE838";
        private const string GlifoCopiar = "\uE8C8";
        private const string GlifoAtualizar = "\uE72C";

        private MenuFlyout mfoArvore = null!;
        private MenuFlyoutItem mfiAbrirLocal = null!;
        private MenuFlyoutItem mfiCopiarCaminho = null!;
        private MenuFlyoutItem mfiCopiarNome = null!;
        private MenuFlyoutSeparator mfsAtualizar = null!;
        private MenuFlyoutItem mfiAtualizar = null!;

        // Criado em código porque é aberto manualmente no ContextRequested, para o nó sob o clique
        // (ou o selecionado, pelo teclado) sem alterar a seleção.
        private void ConfigurarMenuArvore()
        {
            static MenuFlyoutItem CriarItem(string glifo, RoutedEventHandler clique)
            {
                var item = new MenuFlyoutItem { Icon = new FontIcon { Glyph = glifo } };
                item.Click += clique;
                return item;
            }

            mfiAbrirLocal = CriarItem(GlifoPastaAberta, (_, _) => ExecutarNoMenu(AbrirLocalAsync));
            mfiCopiarCaminho = CriarItem(GlifoCopiar, (_, _) => ExecutarNoMenu(CopiarCaminhoAsync));
            mfiCopiarNome = CriarItem(GlifoCopiar, (_, _) => ExecutarNoMenu(CopiarNomeAsync));
            mfsAtualizar = new MenuFlyoutSeparator();
            mfiAtualizar = CriarItem(GlifoAtualizar, mfiAtualizar_Click);

            mfoArvore = new MenuFlyout();
            mfoArvore.Items.Add(mfiAbrirLocal);
            mfoArvore.Items.Add(mfiCopiarCaminho);
            mfoArvore.Items.Add(mfiCopiarNome);
            mfoArvore.Items.Add(mfsAtualizar);
            mfoArvore.Items.Add(mfiAtualizar);
        }

        private void ExecutarNoMenu(Func<ItemArvore, Task> acao)
        {
            if (_itemMenuContexto != null)
                Disparar(acao(_itemMenuContexto));
        }

        private void trvPastas_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
        {
            e.Handled = true;

            var container = AncestralDoTipo<TreeViewItem>(e.OriginalSource as DependencyObject);
            var no = container != null ? trvPastas.NodeFromContainer(container) : trvPastas.SelectedNode;
            if (no?.Content is not ItemArvore item)
            {
                _itemMenuContexto = null;
                _noMenuContexto = null;
                return;
            }

            _itemMenuContexto = item;
            _noMenuContexto = no;
            var textos = TextosMenu(item.EhArquivo);
            mfiAbrirLocal.Text = textos.AbrirLocal;
            mfiCopiarCaminho.Text = textos.CopiarCaminho;
            mfiCopiarNome.Text = textos.CopiarNome;
            mfsAtualizar.Visibility = item.EhArquivo ? Visibility.Collapsed : Visibility.Visible;
            mfiAtualizar.Visibility = item.EhArquivo ? Visibility.Collapsed : Visibility.Visible;
            mfiAtualizar.Text = Textos.Obter("Atualizar");

            var alvo = (FrameworkElement?)container ?? trvPastas;
            if (e.TryGetPosition(alvo, out var posicao))
                mfoArvore.ShowAt(alvo, new FlyoutShowOptions { Position = posicao });
            else
                mfoArvore.ShowAt(alvo);
        }

        private static T? AncestralDoTipo<T>(DependencyObject? elemento) where T : DependencyObject
        {
            while (elemento != null && elemento is not T)
                elemento = VisualTreeHelper.GetParent(elemento);
            return elemento as T;
        }

        private static (string AbrirLocal, string CopiarCaminho, string CopiarNome) TextosMenu(bool ehArquivo) =>
            ehArquivo
                ? (Textos.Obter("AbrirLocalArquivo"), Textos.Obter("CopiarCaminho"), Textos.Obter("CopiarNomeArquivo"))
                : (Textos.Obter("AbrirPasta"), Textos.Obter("CopiarCaminho"), Textos.Obter("CopiarNomePasta"));

        private async Task AbrirLocalAsync(ItemArvore item)
        {
            if (item.EhArquivo)
                await AbrirNoExplorerAsync($"/select,\"{item.Caminho}\"", item.Caminho);
            else
                await AbrirComShellAsync(item.Caminho);
        }

        private Task CopiarCaminhoAsync(ItemArvore item) => CopiarTextoAsync(item.Caminho);

        private Task CopiarNomeAsync(ItemArvore item)
        {
            var nome = Path.GetFileName(item.Caminho.TrimEnd(Path.DirectorySeparatorChar));
            return CopiarTextoAsync(string.IsNullOrEmpty(nome) ? item.Caminho : nome);
        }

        private async Task CopiarTextoAsync(string texto)
        {
            try
            {
                var pacote = new DataPackage();
                pacote.SetText(texto);
                Clipboard.SetContent(pacote);
                // Mantém o texto na área de transferência mesmo depois que o programa fechar.
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                await AvisarAsync(Textos.Formatar("ErroCopiar", ex.Message));
            }
        }

        private void mfiAtualizar_Click(object sender, RoutedEventArgs e)
        {
            if (_noMenuContexto != null && PertenceAArvore(_noMenuContexto))
                AtualizarPasta(_noMenuContexto);
        }

        private void CarregarRaizes()
        {
            trvPastas.RootNodes.Clear();

            var atalhos = new (string ChaveNome, string Caminho)[]
            {
                ("AreaTrabalho", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)),
                ("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                ("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
            };

            // Pastas especiais e unidades usam o próprio ícone do Explorer (chave = caminho).
            foreach (var (chaveNome, caminho) in atalhos)
            {
                if (!string.IsNullOrEmpty(caminho) && Directory.Exists(caminho))
                    trvPastas.RootNodes.Add(CriarNoPasta(Textos.Obter(chaveNome), caminho, chaveIcone: caminho, chaveNome: chaveNome));
            }

            foreach (var unidade in DriveInfo.GetDrives())
            {
                if (!unidade.IsReady)
                    continue;

                var rotulo = string.IsNullOrWhiteSpace(unidade.VolumeLabel)
                    ? unidade.Name
                    : $"{unidade.VolumeLabel} ({unidade.Name.TrimEnd('\\')})";

                var raiz = unidade.RootDirectory.FullName;
                trvPastas.RootNodes.Add(CriarNoPasta(rotulo, raiz, chaveIcone: raiz));
            }
        }

        // Pastas especiais e unidades (chaveIcone = caminho) usam o próprio ícone; as demais, o ícone genérico de pasta.
        private static TreeViewNode CriarNoPasta(string nome, string caminho, string? chaveIcone = null, string? chaveNome = null)
        {
            var item = new ItemArvore(nome, caminho, false)
            {
                ChaveNome = chaveNome,
                Icone = chaveIcone != null
                    ? IconesShell.DoCaminho(chaveIcone, caminho) ?? IconesShell.DoTipo(ChaveIconePasta, ChaveIconePasta, ehPasta: true)
                    : IconesShell.DoTipo(ChaveIconePasta, ChaveIconePasta, ehPasta: true)
            };
            return new TreeViewNode { Content = item, HasUnrealizedChildren = true };
        }

        private static TreeViewNode CriarNoArquivo(string caminho)
        {
            var extensao = Path.GetExtension(caminho).ToLowerInvariant();
            var item = new ItemArvore(Path.GetFileName(caminho), caminho, true)
            {
                Icone = IconesShell.DoTipo("ext" + extensao, extensao, ehPasta: false)
            };
            return new TreeViewNode { Content = item };
        }

        // Handler ligado no XAML: precisa ser de instância.
        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        private void trvPastas_Expanding(TreeView sender, TreeViewExpandingEventArgs args) => CarregarFilhos(args.Node);

        // Carregamento sob demanda: a pasta nasce com HasUnrealizedChildren e é lida na primeira expansão.
        private static void CarregarFilhos(TreeViewNode no)
        {
            if (!no.HasUnrealizedChildren || no.Content is not ItemArvore { EhArquivo: false } item)
                return;

            no.HasUnrealizedChildren = false;
            var diretorio = new DirectoryInfo(item.Caminho);

            foreach (var subpasta in ListarSubpastas(diretorio))
                no.Children.Add(CriarNoPasta(subpasta.Name, subpasta.FullName));

            foreach (var arquivo in ListarArquivos(diretorio))
                no.Children.Add(CriarNoArquivo(arquivo.FullName));
        }

        private static List<DirectoryInfo> ListarSubpastas(DirectoryInfo diretorio)
        {
            try
            {
                return diretorio.GetDirectories()
                    .Where(d => (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return [];
            }
        }

        private static List<FileInfo> ListarArquivos(DirectoryInfo diretorio)
        {
            try
            {
                return diretorio.GetFiles()
                    .Where(f => ExtensoesSuportadas.Contains(f.Extension)
                                && (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                    .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return [];
            }
        }

        // Toda seleção feita pelo programa passa por aqui; o SelectionChanged só trata as do usuário.
        private void SelecionarNo(TreeViewNode? no)
        {
            _noSelecionado = no;
            _ignorarSelecao = true;
            try
            {
                trvPastas.SelectedNode = no;
            }
            finally
            {
                _ignorarSelecao = false;
            }
        }

        private void LimparSelecaoArvore() => SelecionarNo(null);

        // O TreeView não tem um "BeforeSelect" cancelável: com alterações pendentes, a seleção volta ao
        // nó anterior e só é refeita depois da confirmação (que é assíncrona e pode salvar).
        private async void trvPastas_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
        {
            var no = trvPastas.SelectedNode;
            if (_ignorarSelecao || no == _noSelecionado)
                return;

            if (no?.Content is not ItemArvore { EhArquivo: true } item)
            {
                _noSelecionado = no;
                return;
            }

            if (_editorModificado)
            {
                SelecionarNo(_noSelecionado);
                if (!await ConfirmarDescarteAsync() || !PertenceAArvore(no))
                    return;
            }

            SelecionarNo(no);

            if (!_previewPronto)
            {
                _arquivoPendente = item.Caminho;
                return;
            }

            await ExibirArquivoAsync(item.Caminho);
        }

        // Só seleciona (não exibe): expande a árvore até o arquivo ou pasta, carregando o que faltar.
        private bool SelecionarNaArvore(string caminho)
        {
            var caminhoCompleto = Path.GetFullPath(caminho);
            var raiz = Path.GetPathRoot(caminhoCompleto);
            if (string.IsNullOrEmpty(raiz))
                return false;

            var no = trvPastas.RootNodes.FirstOrDefault(n =>
                n.Content is ItemArvore item && string.Equals(item.Caminho, raiz, StringComparison.OrdinalIgnoreCase));
            if (no == null)
                return false;

            var partes = caminhoCompleto[raiz.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var parte in partes)
            {
                CarregarFilhos(no);
                no.IsExpanded = true;
                var filho = no.Children.FirstOrDefault(n =>
                    n.Content is ItemArvore item && string.Equals(Path.GetFileName(item.Caminho), parte, StringComparison.OrdinalIgnoreCase));
                if (filho == null)
                    return false;
                no = filho;
            }

            SelecionarNo(no);
            TrazerParaVisao(no);
            return true;
        }

        // O TreeView é virtualizado: um nó longe da área visível não tem contêiner (ContainerFromNode = null).
        // A lista interna (TreeViewList, um ListView com os nós "achatados") rola até o nó e cria o contêiner;
        // depois disso o StartBringIntoView ajusta a posição.
        private void TrazerParaVisao(TreeViewNode no)
        {
            trvPastas.UpdateLayout();
            if (DescendenteDoTipo<ListView>(trvPastas) is ListView lista)
            {
                lista.ScrollIntoView(no);
                lista.UpdateLayout();
            }

            if (trvPastas.ContainerFromNode(no) is UIElement container)
            {
                container.StartBringIntoView();
                return;
            }

            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                (trvPastas.ContainerFromNode(no) as UIElement)?.StartBringIntoView());
        }

        private static T? DescendenteDoTipo<T>(DependencyObject raiz) where T : DependencyObject
        {
            var pendentes = new Queue<DependencyObject>();
            pendentes.Enqueue(raiz);
            while (pendentes.Count > 0)
            {
                var atual = pendentes.Dequeue();
                if (atual != raiz && atual is T encontrado)
                    return encontrado;

                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(atual); i++)
                    pendentes.Enqueue(VisualTreeHelper.GetChild(atual, i));
            }

            return null;
        }

        private bool PertenceAArvore(TreeViewNode no)
        {
            var atual = no;
            while (atual.Parent != null && !trvPastas.RootNodes.Contains(atual))
                atual = atual.Parent;
            return trvPastas.RootNodes.Contains(atual);
        }

        private IList<TreeViewNode> ColecaoDoNo(TreeViewNode no) =>
            trvPastas.RootNodes.Contains(no) ? trvPastas.RootNodes : no.Parent!.Children;

        private void AtualizarPasta(TreeViewNode no)
        {
            if (no.Content is not ItemArvore { EhArquivo: false } || no.HasUnrealizedChildren)
                return;

            _ignorarSelecao = true;
            try
            {
                AtualizarFilhos(no);
            }
            finally
            {
                _ignorarSelecao = false;
            }

            if (_noSelecionado != null && !PertenceAArvore(_noSelecionado))
                LimparSelecaoArvore();
        }

        // Relê a pasta mantendo os nós existentes (e o que está expandido), removendo os que sumiram e
        // inserindo os novos na ordem. Subpastas carregadas mas recolhidas voltam a ser lidas ao expandir.
        private static void AtualizarFilhos(TreeViewNode no)
        {
            var diretorio = new DirectoryInfo(((ItemArvore)no.Content).Caminho);
            var existentes = no.Children
                .Where(n => n.Content is ItemArvore)
                .ToDictionary(n => ((ItemArvore)n.Content).Chave);

            var atualizados = new List<TreeViewNode>();

            foreach (var subpasta in ListarSubpastas(diretorio))
            {
                if (existentes.TryGetValue((subpasta.FullName, false), out var filho))
                {
                    if (filho.IsExpanded)
                        AtualizarFilhos(filho);
                    else if (!filho.HasUnrealizedChildren)
                    {
                        filho.Children.Clear();
                        filho.HasUnrealizedChildren = true;
                    }
                }
                else
                    filho = CriarNoPasta(subpasta.Name, subpasta.FullName);

                atualizados.Add(filho);
            }

            foreach (var arquivo in ListarArquivos(diretorio))
                atualizados.Add(existentes.GetValueOrDefault((arquivo.FullName, true)) ?? CriarNoArquivo(arquivo.FullName));

            var manter = atualizados.ToHashSet();
            foreach (var filho in no.Children.Where(n => !manter.Contains(n)).ToList())
                no.Children.Remove(filho);

            // Os nós mantidos já estão na ordem certa; basta inserir os novos nas posições corretas,
            // sem remover os existentes (o que perderia o estado de expansão).
            for (var i = 0; i < atualizados.Count; i++)
                if (i >= no.Children.Count || no.Children[i] != atualizados[i])
                {
                    if (atualizados[i].Parent == no)
                        no.Children.Remove(atualizados[i]);
                    no.Children.Insert(i, atualizados[i]);
                }
        }

        // Recarrega a pasta do arquivo recém-criado nas raízes em que ela já está carregada
        // e seleciona o arquivo na árvore, sem reexibi-lo.
        private void MostrarNaArvore(string caminho)
        {
            var pasta = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrEmpty(pasta))
            {
                foreach (var no in LocalizarNosCarregados(pasta))
                    AtualizarPasta(no);
            }

            if (!SelecionarNaArvore(caminho))
                LimparSelecaoArvore();
        }

        private void RemoverDaArvore(string caminho)
        {
            foreach (var no in LocalizarNosCarregados(caminho))
            {
                if (_noSelecionado == no)
                    LimparSelecaoArvore();
                ColecaoDoNo(no).Remove(no);
            }
        }

        // Percorre só os nós já carregados (sem expandir) e devolve todos os nós do arquivo ou pasta,
        // já que ele pode aparecer em mais de uma raiz (ex.: Área de Trabalho e a unidade C:).
        private List<TreeViewNode> LocalizarNosCarregados(string caminho)
        {
            var caminhoCompleto = Path.TrimEndingDirectorySeparator(Path.GetFullPath(caminho));
            var encontrados = new List<TreeViewNode>();
            var pendentes = new Stack<TreeViewNode>(trvPastas.RootNodes);

            while (pendentes.Count > 0)
            {
                var no = pendentes.Pop();
                if (no.Content is not ItemArvore item)
                    continue;

                if (string.Equals(Path.TrimEndingDirectorySeparator(item.Caminho), caminhoCompleto, StringComparison.OrdinalIgnoreCase))
                {
                    encontrados.Add(no);
                    continue;
                }

                if (item.EhArquivo)
                    continue;

                // Raiz de unidade ("C:\") já termina com separador; não pode virar "C:\\".
                var prefixo = Path.EndsInDirectorySeparator(item.Caminho)
                    ? item.Caminho
                    : item.Caminho + Path.DirectorySeparatorChar;
                if (caminhoCompleto.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var filho in no.Children)
                        pendentes.Push(filho);
                }
            }

            return encontrados;
        }

        private void tlbTitulo_PaneToggleRequested(TitleBar sender, object args) => AlternarArvore();

        // Esconde a coluna da árvore guardando a largura (ajustável pelo GridSplitter) para restaurá-la depois.
        private void AlternarArvore()
        {
            _arvoreOculta = !_arvoreOculta;
            var visibilidade = _arvoreOculta ? Visibility.Collapsed : Visibility.Visible;

            if (_arvoreOculta)
            {
                _larguraArvore = colArvore.ActualWidth;
                colArvore.MinWidth = 0;
                colArvore.Width = new GridLength(0);
            }
            else
            {
                colArvore.MinWidth = LarguraMinimaArvore;
                colArvore.Width = new GridLength(Math.Max(_larguraArvore, LarguraMinimaArvore));
            }

            trvPastas.Visibility = visibilidade;
            gspDivisoria.Visibility = visibilidade;
            grdConteudo.Margin = _arvoreOculta ? new Thickness(8, 0, 0, 0) : new Thickness(0);
        }
    }
}
