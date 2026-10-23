using System.Runtime.InteropServices;
using static MarkdownEditor.NativeMethods;

namespace MarkdownEditor
{
    public partial class frmEditor
    {
        private bool SelecionarNaArvore(string caminho)
        {
            var caminhoCompleto = Path.GetFullPath(caminho);
            var raiz = Path.GetPathRoot(caminhoCompleto);
            if (string.IsNullOrEmpty(raiz))
                return false;

            var no = trvPastas.Nodes.Cast<TreeNode>().FirstOrDefault(n =>
                n.Tag is NoArvore info && string.Equals(info.Caminho, raiz, StringComparison.OrdinalIgnoreCase));
            if (no == null)
                return false;

            var partes = caminhoCompleto[raiz.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            foreach (var parte in partes)
            {
                no.Expand();
                var filho = no.Nodes.Cast<TreeNode>().FirstOrDefault(n =>
                    n.Tag is NoArvore info && string.Equals(Path.GetFileName(info.Caminho), parte, StringComparison.OrdinalIgnoreCase));
                if (filho == null)
                    return false;
                no = filho;
            }

            trvPastas.SelectedNode = no;
            no.EnsureVisible();
            return true;
        }

        private void CarregarIcones()
        {
            // O ImageList não escala sozinho: o tamanho acompanha o DPI (16 px a 100%, 24 px a 150%...).
            var tamanho = LogicalToDeviceUnits(TamanhoIconeLogico);
            imlIcones.ImageSize = new Size(tamanho, tamanho);

            AdicionarIconeStock("pasta", StockIconId.Folder);
            AdicionarIconeStock("pastaAberta", StockIconId.FolderOpen);
            AdicionarIconeStock("arquivo", StockIconId.DocumentNoAssociation);
            AdicionarIconeStock("unidadeFixa", StockIconId.DriveFixed);
            AdicionarIconeStock("unidadeRemovivel", StockIconId.DriveRemovable);
            AdicionarIconeStock("unidadeRede", StockIconId.DriveNet);
            AdicionarIconeStock("unidadeCD", StockIconId.DriveCD);
        }

        private void AdicionarIconeStock(string chave, StockIconId id)
        {
            using var icone = SystemIcons.GetStockIcon(id, imlIcones.ImageSize.Width);
            AdicionarIcone(chave, icone);
        }

        // Redimensiona com qualidade para o tamanho do ImageList (os ícones do shell vêm em 16 ou 32 px).
        private void AdicionarIcone(string chave, Icon icone)
        {
            var tamanho = imlIcones.ImageSize;
            if (icone.Size == tamanho)
            {
                imlIcones.Images.Add(chave, icone);
                return;
            }

            using var original = icone.ToBitmap();
            var imagem = new Bitmap(tamanho.Width, tamanho.Height);
            using (var grafico = Graphics.FromImage(imagem))
            {
                grafico.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                grafico.DrawImage(original, new Rectangle(Point.Empty, tamanho));
            }
            imlIcones.Images.Add(chave, imagem);
        }

        private string ObterChaveIconePasta(string caminho, string chave)
        {
            if (imlIcones.Images.ContainsKey(chave))
                return chave;

            var tamanhoShell = imlIcones.ImageSize.Width > 16 ? SHGFI_LARGEICON : SHGFI_SMALLICON;
            var info = new SHFILEINFO();
            SHGetFileInfo(caminho, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | tamanhoShell);
            if (info.hIcon == IntPtr.Zero)
                return "pasta";

            try
            {
                using var icone = Icon.FromHandle(info.hIcon);
                AdicionarIcone(chave, icone);
                return chave;
            }
            catch (Exception)
            {
                return "pasta";
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        private string ObterChaveIconeArquivo(string caminho)
        {
            var chave = "ext" + Path.GetExtension(caminho).ToLowerInvariant();
            if (imlIcones.Images.ContainsKey(chave))
                return chave;

            try
            {
                using var icone = Icon.ExtractAssociatedIcon(caminho);
                if (icone == null)
                    return "arquivo";

                AdicionarIcone(chave, icone);
                return chave;
            }
            catch (Exception)
            {
                return "arquivo";
            }
        }

        private void CarregarRaizes()
        {
            trvPastas.BeginUpdate();
            try
            {
                trvPastas.Nodes.Clear();

                var atalhos = new (string Nome, string Caminho, string ChaveIcone)[]
                {
                    ("Área de Trabalho", Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "pastaDesktop"),
                    ("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pastaDocumentos"),
                    ("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "pastaDownloads")
                };

                foreach (var (nome, caminho, chaveIcone) in atalhos)
                {
                    if (!string.IsNullOrEmpty(caminho) && Directory.Exists(caminho))
                        trvPastas.Nodes.Add(CriarNoPasta(nome, caminho, ObterChaveIconePasta(caminho, chaveIcone)));
                }

                foreach (var unidade in DriveInfo.GetDrives())
                {
                    if (!unidade.IsReady)
                        continue;

                    var rotulo = string.IsNullOrWhiteSpace(unidade.VolumeLabel)
                        ? unidade.Name
                        : $"{unidade.VolumeLabel} ({unidade.Name.TrimEnd('\\')})";

                    var icone = unidade.DriveType switch
                    {
                        DriveType.Removable => "unidadeRemovivel",
                        DriveType.Network => "unidadeRede",
                        DriveType.CDRom => "unidadeCD",
                        _ => "unidadeFixa"
                    };

                    trvPastas.Nodes.Add(CriarNoPasta(rotulo, unidade.RootDirectory.FullName, icone));
                }
            }
            finally
            {
                trvPastas.EndUpdate();
            }
        }

        private static TreeNode CriarNoPasta(string texto, string caminho, string chaveIcone)
        {
            var no = new TreeNode(texto)
            {
                Tag = new NoArvore(caminho, false),
                ImageKey = chaveIcone,
                SelectedImageKey = chaveIcone
            };
            no.Nodes.Add(new TreeNode(TextoNoFicticio));
            return no;
        }

        private TreeNode CriarNoArquivo(string caminho)
        {
            var chaveIcone = ObterChaveIconeArquivo(caminho);
            return new TreeNode(Path.GetFileName(caminho))
            {
                Tag = new NoArvore(caminho, true),
                ImageKey = chaveIcone,
                SelectedImageKey = chaveIcone
            };
        }

        private void trvPastas_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
        {
            var no = e.Node;
            if (no == null || no.Tag is not NoArvore { EhArquivo: false } info)
                return;

            if (no.Nodes.Count != 1 || no.Nodes[0].Tag != null)
                return;

            trvPastas.BeginUpdate();
            Cursor = Cursors.WaitCursor;
            try
            {
                no.Nodes.Clear();
                var diretorio = new DirectoryInfo(info.Caminho);

                foreach (var subpasta in ListarSubpastas(diretorio))
                    no.Nodes.Add(CriarNoPasta(subpasta.Name, subpasta.FullName, "pasta"));

                foreach (var arquivo in ListarArquivos(diretorio))
                    no.Nodes.Add(CriarNoArquivo(arquivo.FullName));
            }
            finally
            {
                Cursor = Cursors.Default;
                trvPastas.EndUpdate();
            }
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

        // A confirmação é assíncrona (pode salvar), então a seleção é cancelada aqui e refeita
        // depois, já sem alterações pendentes no editor.
        private async void trvPastas_BeforeSelect(object? sender, TreeViewCancelEventArgs e)
        {
            if (_ignorarSelecao || !sciEditor.Modified || e.Node?.Tag is not NoArvore { EhArquivo: true })
                return;

            e.Cancel = true;
            var no = e.Node;
            if (await ConfirmarDescarteAsync() && no.TreeView == trvPastas)
                trvPastas.SelectedNode = no;
        }

        private async void trvPastas_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (_ignorarSelecao || e.Node?.Tag is not NoArvore { EhArquivo: true } info)
                return;

            if (!_previewPronto)
            {
                _arquivoPendente = info.Caminho;
                return;
            }

            await ExibirArquivoAsync(info.Caminho);
        }

        private static (string AbrirLocal, string CopiarCaminho, string CopiarNome) TextosMenu(bool ehArquivo) =>
            ehArquivo
                ? ("Abrir local do arquivo", "Copiar caminho completo", "Copiar nome do arquivo")
                : ("Abrir pasta", "Copiar caminho completo", "Copiar nome da pasta");

        private void AbrirLocal(NoArvore no)
        {
            if (no.EhArquivo)
                AbrirNoExplorer($"/select,\"{no.Caminho}\"", no.Caminho);
            else
                AbrirComShell(no.Caminho);
        }

        private static void CopiarCaminho(NoArvore no) => Clipboard.SetText(no.Caminho);

        private static void CopiarNome(NoArvore no)
        {
            var nome = Path.GetFileName(no.Caminho.TrimEnd(Path.DirectorySeparatorChar));
            Clipboard.SetText(string.IsNullOrEmpty(nome) ? no.Caminho : nome);
        }

        private void cmsArvore_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var no = cmsArvore.SourceControl == trvPastas && trvPastas.ClientRectangle.Contains(trvPastas.PointToClient(Cursor.Position))
                ? trvPastas.GetNodeAt(trvPastas.PointToClient(Cursor.Position))
                : trvPastas.SelectedNode;

            if (no?.Tag is not NoArvore info)
            {
                _noMenuContexto = null;
                _treeNodeMenuContexto = null;
                e.Cancel = true;
                return;
            }

            _noMenuContexto = info;
            _treeNodeMenuContexto = no;
            var textos = TextosMenu(info.EhArquivo);
            tsmAbrirLocal.Text = textos.AbrirLocal;
            tsmCopiarCaminho.Text = textos.CopiarCaminho;
            tsmCopiarNome.Text = textos.CopiarNome;
            tssAtualizar.Visible = !info.EhArquivo;
            tsmAtualizar.Visible = !info.EhArquivo;
        }

        private void tsmAtualizar_Click(object? sender, EventArgs e)
        {
            if (_treeNodeMenuContexto?.TreeView == trvPastas)
                AtualizarPasta(_treeNodeMenuContexto);
        }

        private void AtualizarPasta(TreeNode no)
        {
            if (no.Tag is not NoArvore { EhArquivo: false } || NaoCarregado(no))
                return;

            trvPastas.BeginUpdate();
            Cursor = Cursors.WaitCursor;
            _ignorarSelecao = true;
            try
            {
                AtualizarFilhos(no);
            }
            finally
            {
                _ignorarSelecao = false;
                Cursor = Cursors.Default;
                trvPastas.EndUpdate();
            }
        }

        private static bool NaoCarregado(TreeNode no) => no.Nodes.Count == 1 && no.Nodes[0].Tag == null;

        private void AtualizarFilhos(TreeNode no)
        {
            var diretorio = new DirectoryInfo(((NoArvore)no.Tag!).Caminho);
            var existentes = no.Nodes.Cast<TreeNode>()
                .Where(n => n.Tag is NoArvore)
                .ToDictionary(n => (NoArvore)n.Tag!);

            var atualizados = new List<TreeNode>();

            foreach (var subpasta in ListarSubpastas(diretorio))
            {
                if (existentes.TryGetValue(new NoArvore(subpasta.FullName, false), out var filho))
                {
                    if (filho.IsExpanded)
                        AtualizarFilhos(filho);
                    else if (!NaoCarregado(filho))
                    {
                        filho.Nodes.Clear();
                        filho.Nodes.Add(new TreeNode(TextoNoFicticio));
                    }
                }
                else
                    filho = CriarNoPasta(subpasta.Name, subpasta.FullName, "pasta");

                atualizados.Add(filho);
            }

            foreach (var arquivo in ListarArquivos(diretorio))
                atualizados.Add(existentes.GetValueOrDefault(new NoArvore(arquivo.FullName, true))
                    ?? CriarNoArquivo(arquivo.FullName));

            var manter = atualizados.ToHashSet();
            foreach (var filho in no.Nodes.Cast<TreeNode>().Where(n => !manter.Contains(n)).ToList())
                filho.Remove();

            // Os nós mantidos já estão na ordem certa; basta inserir os novos nas posições corretas,
            // sem remover os existentes (o que perderia o estado de expansão).
            for (var i = 0; i < atualizados.Count; i++)
                if (i >= no.Nodes.Count || no.Nodes[i] != atualizados[i])
                {
                    if (atualizados[i].Parent == no)
                        atualizados[i].Remove();
                    no.Nodes.Insert(i, atualizados[i]);
                }
        }

        private void tsmAbrirLocal_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                AbrirLocal(_noMenuContexto);
        }

        private void tsmCopiarCaminho_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                CopiarCaminho(_noMenuContexto);
        }

        private void tsmCopiarNome_Click(object? sender, EventArgs e)
        {
            if (_noMenuContexto != null)
                CopiarNome(_noMenuContexto);
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
        }

        private void RemoverDaArvore(string caminho)
        {
            var nos = LocalizarNosCarregados(caminho);
            if (nos.Count == 0)
                return;

            _ignorarSelecao = true;
            trvPastas.BeginUpdate();
            try
            {
                foreach (var no in nos)
                {
                    if (trvPastas.SelectedNode == no)
                        trvPastas.SelectedNode = null;
                    no.Remove();
                }
            }
            finally
            {
                trvPastas.EndUpdate();
                _ignorarSelecao = false;
            }
        }

        // Percorre só os nós já carregados (sem expandir) e devolve todos os nós do arquivo ou pasta,
        // já que ele pode aparecer em mais de uma raiz (ex.: Área de Trabalho e a unidade C:).
        private List<TreeNode> LocalizarNosCarregados(string caminho)
        {
            var caminhoCompleto = Path.TrimEndingDirectorySeparator(Path.GetFullPath(caminho));
            var encontrados = new List<TreeNode>();
            var pendentes = new Stack<TreeNode>(trvPastas.Nodes.Cast<TreeNode>());

            while (pendentes.Count > 0)
            {
                var no = pendentes.Pop();
                if (no.Tag is not NoArvore info)
                    continue;

                if (string.Equals(Path.TrimEndingDirectorySeparator(info.Caminho), caminhoCompleto, StringComparison.OrdinalIgnoreCase))
                {
                    encontrados.Add(no);
                    continue;
                }

                if (info.EhArquivo)
                    continue;

                // Raiz de unidade ("C:\") já termina com separador; não pode virar "C:\\".
                var prefixo = Path.EndsInDirectorySeparator(info.Caminho)
                    ? info.Caminho
                    : info.Caminho + Path.DirectorySeparatorChar;
                if (caminhoCompleto.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (TreeNode filho in no.Nodes)
                        pendentes.Push(filho);
                }
            }

            return encontrados;
        }

        private void tsbArvore_Click(object? sender, EventArgs e) => AlternarArvore();

        private void AlternarArvore()
        {
            if (spcPrincipal.Panel1Collapsed)
            {
                spcPrincipal.Panel1Collapsed = false;
                spcPrincipal.SplitterDistance = Math.Max(_larguraArvore, LarguraMinimaArvore);
                tsbArvore.Text = TextoEsconderArvore;
                AtualizarIcones();
            }
            else
            {
                _larguraArvore = spcPrincipal.SplitterDistance;
                spcPrincipal.Panel1Collapsed = true;
                tsbArvore.Text = TextoMostrarArvore;
                AtualizarIcones();
            }
        }

        // O arraste nativo do SplitContainer desenha a barra via GDI (XOR), que não aparece
        // sobre o WebView2; por isso o arraste é tratado aqui, redimensionando ao vivo.
        private void spcPrincipal_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && spcPrincipal.SplitterRectangle.Contains(e.Location))
                _deslocamentoArraste = e.X - spcPrincipal.SplitterDistance;
        }

        private void spcPrincipal_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_deslocamentoArraste is int deslocamento)
            {
                var maximo = spcPrincipal.Width - spcPrincipal.SplitterWidth - spcPrincipal.Panel2MinSize;
                var novaDistancia = Math.Clamp(e.X - deslocamento, spcPrincipal.Panel1MinSize, Math.Max(maximo, spcPrincipal.Panel1MinSize));

                if (novaDistancia != spcPrincipal.SplitterDistance)
                    spcPrincipal.SplitterDistance = novaDistancia;
            }

            AtualizarCursorDivisoria(e.Location);
        }

        private void spcPrincipal_MouseUp(object? sender, MouseEventArgs e)
        {
            _deslocamentoArraste = null;
            AtualizarCursorDivisoria(e.Location);
        }

        // O Cursor do SplitContainer é herdado pelos painéis filhos; ao sair para a árvore ou o
        // preview ele precisa voltar ao padrão, senão o VSplit continua aparecendo neles.
        private void spcPrincipal_MouseLeave(object? sender, EventArgs e)
        {
            if (_deslocamentoArraste == null)
                spcPrincipal.Cursor = Cursors.Default;
        }

        private void AtualizarCursorDivisoria(Point posicao)
        {
            spcPrincipal.Cursor = _deslocamentoArraste != null || spcPrincipal.SplitterRectangle.Contains(posicao)
                ? Cursors.VSplit
                : Cursors.Default;
        }

        private void spcPrincipal_MouseCaptureChanged(object? sender, EventArgs e) => _deslocamentoArraste = null;
    }
}
