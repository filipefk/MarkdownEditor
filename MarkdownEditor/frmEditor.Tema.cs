using System.Drawing.Drawing2D;
using System.Drawing.Text;
using static MarkdownEditor.NativeMethods;
using EstiloEditor = ScintillaNET.Style;

namespace MarkdownEditor
{
    public partial class frmEditor
    {
        private const int TamanhoIconeLogico = 16;

        // Códigos dos glifos (iguais na Segoe Fluent Icons e na Segoe MDL2 Assets).
        private const char GlifoEsconderPainel = '';
        private const char GlifoMostrarPainel = '';
        private const char GlifoVoltar = '';
        private const char GlifoAvancar = '';
        private const char GlifoVisualizar = '';
        private const char GlifoEditar = '';
        private const char GlifoSalvar = '';
        private const char GlifoDesfazer = '';
        private const char GlifoPastaAberta = '';
        private const char GlifoCopiar = '';
        private const char GlifoAtualizar = '';

        // API oficial do WinForms (.NET 9+); devolve Classic em alto contraste.
        private static bool SistemaUsaTemaEscuro() => Application.SystemColorMode == SystemColorMode.Dark;

        private void AplicarTema(bool escuro)
        {
            if (_temaEscuro == escuro)
                return;

            _temaEscuro = escuro;

            var corFundo = escuro ? CorFundoEscuro : CorFundoClaro;
            var corPainel = escuro ? CorPainelEscuro : CorPainelClaro;
            var corTexto = escuro ? CorTextoEscuro : CorTextoClaro;
            var corBorda = escuro ? CorBordaEscuro : CorBordaClaro;

            BackColor = corFundo;
            spcPrincipal.BackColor = corBorda;
            spcPrincipal.Panel1.BackColor = corPainel;
            spcPrincipal.Panel2.BackColor = corFundo;

            trvPastas.BackColor = corPainel;
            trvPastas.ForeColor = corTexto;
            trvPastas.LineColor = corTexto;

            tsrNavegacao.Renderer = new RenderizadorToolStrip(escuro);
            tsrNavegacao.BackColor = corPainel;
            tsrNavegacao.ForeColor = corTexto;

            cmsArvore.Renderer = new RenderizadorToolStrip(escuro);
            cmsArvore.BackColor = corPainel;
            cmsArvore.ForeColor = corTexto;

            wvwPreview.DefaultBackgroundColor = corFundo;
            AplicarEstilosEditor();

            AplicarTemaBarraTitulo();
            if (trvPastas.IsHandleCreated)
                AplicarEstiloArvore();
            AtualizarIcones();
        }

        // Cores do VS Code Light+/Dark+. Os números de estilo dependem do lexer,
        // por isso as cores de sintaxe são reaplicadas a cada troca de lexer.
        private void AplicarEstilosEditor()
        {
            var escuro = _temaEscuro ?? false;

            sciEditor.StyleResetDefault();
            var padrao = sciEditor.Styles[EstiloEditor.Default];
            padrao.Font = FonteEditor;
            padrao.Size = TamanhoFonteEditor;
            padrao.BackColor = escuro ? CorFundoEscuro : CorFundoClaro;
            padrao.ForeColor = escuro ? CorTextoEscuro : CorTextoClaro;
            sciEditor.StyleClearAll();

            var numeros = sciEditor.Styles[EstiloEditor.LineNumber];
            numeros.BackColor = escuro ? CorPainelEscuro : CorPainelClaro;
            numeros.ForeColor = ColorTranslator.FromHtml(escuro ? "#858585" : "#237893");

            sciEditor.CaretForeColor = ColorTranslator.FromHtml(escuro ? "#AEAFAD" : "#000000");
            sciEditor.SelectionBackColor = ColorTranslator.FromHtml(escuro ? "#264F78" : "#ADD6FF");

            void Cor(int estilo, string corEscuro, string corClaro, bool negrito = false, bool italico = false)
            {
                var s = sciEditor.Styles[estilo];
                s.ForeColor = ColorTranslator.FromHtml(escuro ? corEscuro : corClaro);
                s.Bold = negrito;
                s.Italic = italico;
            }

            switch (sciEditor.LexerName)
            {
                case "markdown":
                    foreach (var titulo in new[] { EstiloEditor.Markdown.Header1, EstiloEditor.Markdown.Header2, EstiloEditor.Markdown.Header3,
                                                   EstiloEditor.Markdown.Header4, EstiloEditor.Markdown.Header5, EstiloEditor.Markdown.Header6 })
                        Cor(titulo, "#569CD6", "#800000", negrito: true);
                    Cor(EstiloEditor.Markdown.Strong1, "#569CD6", "#000080", negrito: true);
                    Cor(EstiloEditor.Markdown.Strong2, "#569CD6", "#000080", negrito: true);
                    Cor(EstiloEditor.Markdown.Em1, "#D4D4D4", "#1E1E1E", italico: true);
                    Cor(EstiloEditor.Markdown.Em2, "#D4D4D4", "#1E1E1E", italico: true);
                    Cor(EstiloEditor.Markdown.Code, "#CE9178", "#800000");
                    Cor(EstiloEditor.Markdown.Code2, "#CE9178", "#800000");
                    Cor(EstiloEditor.Markdown.CodeBk, "#CE9178", "#800000");
                    Cor(EstiloEditor.Markdown.PreChar, "#CE9178", "#800000");
                    Cor(EstiloEditor.Markdown.UListItem, "#6796E6", "#0451A5");
                    Cor(EstiloEditor.Markdown.OListItem, "#6796E6", "#0451A5");
                    Cor(EstiloEditor.Markdown.BlockQuote, "#6A9955", "#0451A5");
                    Cor(EstiloEditor.Markdown.Strikeout, "#808080", "#808080");
                    Cor(EstiloEditor.Markdown.HRule, "#808080", "#808080");
                    Cor(EstiloEditor.Markdown.Link, "#3794FF", "#006AB1");
                    break;

                case "json":
                    Cor(EstiloEditor.Json.PropertyName, "#9CDCFE", "#0451A5");
                    Cor(EstiloEditor.Json.String, "#CE9178", "#A31515");
                    Cor(EstiloEditor.Json.StringEol, "#F44747", "#CD3131");
                    Cor(EstiloEditor.Json.Number, "#B5CEA8", "#098658");
                    Cor(EstiloEditor.Json.Keyword, "#569CD6", "#0000FF");
                    Cor(EstiloEditor.Json.LdKeyword, "#569CD6", "#0000FF");
                    Cor(EstiloEditor.Json.EscapeSequence, "#D7BA7D", "#EE0000");
                    Cor(EstiloEditor.Json.LineComment, "#6A9955", "#008000");
                    Cor(EstiloEditor.Json.BlockComment, "#6A9955", "#008000");
                    Cor(EstiloEditor.Json.Uri, "#3794FF", "#006AB1");
                    Cor(EstiloEditor.Json.Error, "#F44747", "#CD3131");
                    break;

                case "xml":
                    foreach (var tag in new[] { EstiloEditor.Xml.Tag, EstiloEditor.Xml.TagUnknown, EstiloEditor.Xml.TagEnd })
                        Cor(tag, "#569CD6", "#800000");
                    Cor(EstiloEditor.Xml.Attribute, "#9CDCFE", "#E50000");
                    Cor(EstiloEditor.Xml.AttributeUnknown, "#9CDCFE", "#E50000");
                    Cor(EstiloEditor.Xml.DoubleString, "#CE9178", "#0000FF");
                    Cor(EstiloEditor.Xml.SingleString, "#CE9178", "#0000FF");
                    Cor(EstiloEditor.Xml.Value, "#CE9178", "#0000FF");
                    Cor(EstiloEditor.Xml.Number, "#B5CEA8", "#098658");
                    Cor(EstiloEditor.Xml.Comment, "#6A9955", "#008000");
                    Cor(EstiloEditor.Xml.Entity, "#569CD6", "#0000FF");
                    Cor(EstiloEditor.Xml.CData, "#CE9178", "#A31515");
                    Cor(EstiloEditor.Xml.XmlStart, "#808080", "#800000");
                    Cor(EstiloEditor.Xml.XmlEnd, "#808080", "#800000");
                    Cor(EstiloEditor.Xml.Question, "#808080", "#800000");
                    break;
            }
        }

        private void AplicarTemaBarraTitulo()
        {
            if (!IsHandleCreated || _temaEscuro == null)
                return;

            var valor = _temaEscuro.Value ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref valor, sizeof(int));
        }

        // Visual no padrão Fluent do Windows 11: fonte Segoe UI Variable, ícones Segoe Fluent Icons,
        // botões com cantos arredondados e árvore no estilo do painel de navegação do Explorer.
        private void ConfigurarVisual()
        {
            var fonte = CriarFonteInterface();
            Font = fonte;
            tsrNavegacao.Font = fonte;
            cmsArvore.Font = fonte;

            var tamanhoIcone = LogicalToDeviceUnits(TamanhoIconeLogico);
            tsrNavegacao.ImageScalingSize = new Size(tamanhoIcone, tamanhoIcone);
            cmsArvore.ImageScalingSize = new Size(tamanhoIcone, tamanhoIcone);
            tsrNavegacao.Padding = new Padding(LogicalToDeviceUnits(4));
            foreach (ToolStripItem item in tsrNavegacao.Items)
            {
                if (item is ToolStripButton botao)
                {
                    botao.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                    botao.Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(3),
                        LogicalToDeviceUnits(6), LogicalToDeviceUnits(3));
                }
            }

            trvPastas.ItemHeight = LogicalToDeviceUnits(24);
            trvPastas.HandleCreated += (_, _) => AplicarEstiloArvore();
            if (trvPastas.IsHandleCreated)
                AplicarEstiloArvore();

            cmsArvore.Opened += (_, _) => ArredondarCantos(cmsArvore.Handle);
        }

        private static Font CriarFonteInterface()
        {
            const string FonteWindows11 = "Segoe UI Variable Text";
            var nome = FontFamily.Families.Any(f => f.Name == FonteWindows11) ? FonteWindows11 : "Segoe UI";
            return new Font(nome, 9F);
        }

        private void AplicarEstiloArvore()
        {
            var estilos = (IntPtr)(TVS_EX_DOUBLEBUFFER | TVS_EX_FADEINOUTEXPANDOS);
            SendMessage(trvPastas.Handle, TVM_SETEXTENDEDSTYLE, estilos, estilos);
            SetWindowTheme(trvPastas.Handle, _temaEscuro == true ? "DarkMode_Explorer" : "Explorer", null);
        }

        private static void ArredondarCantos(IntPtr janela)
        {
            var preferencia = DWMWCP_ROUNDSMALL;
            DwmSetWindowAttribute(janela, DWMWA_WINDOW_CORNER_PREFERENCE, ref preferencia, sizeof(int));
        }

        private void AtualizarIcones()
        {
            var cor = _temaEscuro == true ? CorTextoEscuro : CorTextoClaro;
            var tamanho = LogicalToDeviceUnits(TamanhoIconeLogico);

            void Definir(ToolStripItem item, char glifo)
            {
                var anterior = item.Image;
                item.Image = CriarIconeFluent(glifo, cor, tamanho);
                anterior?.Dispose();
            }

            Definir(tsbArvore, spcPrincipal.Panel1Collapsed ? GlifoMostrarPainel : GlifoEsconderPainel);
            Definir(tsbVoltar, GlifoVoltar);
            Definir(tsbAvancar, GlifoAvancar);
            Definir(tsbVisualizar, GlifoVisualizar);
            Definir(tsbEditar, GlifoEditar);
            Definir(tsbSalvar, GlifoSalvar);
            Definir(tsbDesfazer, GlifoDesfazer);
            Definir(tsmAbrirLocal, GlifoPastaAberta);
            Definir(tsmCopiarCaminho, GlifoCopiar);
            Definir(tsmCopiarNome, GlifoCopiar);
            Definir(tsmAtualizar, GlifoAtualizar);
        }

        // Segoe Fluent Icons no Windows 11; Segoe MDL2 Assets (mesmos códigos) no Windows 10.
        private static readonly string FonteIcones =
            FontFamily.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

        private static Bitmap CriarIconeFluent(char glifo, Color cor, int tamanho)
        {
            var bitmap = new Bitmap(tamanho, tamanho);
            using var grafico = Graphics.FromImage(bitmap);
            grafico.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var fonte = new Font(FonteIcones, tamanho * 0.875F, GraphicsUnit.Pixel);
            using var pincel = new SolidBrush(cor);
            using var formato = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            grafico.DrawString(glifo.ToString(), fonte, pincel, new RectangleF(0, 0, tamanho, tamanho), formato);
            return bitmap;
        }

        private static GraphicsPath CriarRetanguloArredondado(Rectangle area, int raio)
        {
            var diametro = raio * 2;
            var caminho = new GraphicsPath();
            caminho.AddArc(area.Left, area.Top, diametro, diametro, 180, 90);
            caminho.AddArc(area.Right - diametro, area.Top, diametro, diametro, 270, 90);
            caminho.AddArc(area.Right - diametro, area.Bottom - diametro, diametro, diametro, 0, 90);
            caminho.AddArc(area.Left, area.Bottom - diametro, diametro, diametro, 90, 90);
            caminho.CloseFigure();
            return caminho;
        }

        private sealed class RenderizadorToolStrip(bool escuro) : ToolStripProfessionalRenderer(new TabelaCoresToolStrip(escuro))
        {
            private const int Raio = 4;

            private Color Borda => escuro ? CorBordaEscuro : CorBordaClaro;
            private Color Destaque => escuro ? CorDestaqueEscuro : CorDestaqueClaro;
            private Color Pressionado => escuro ? CorPressionadoEscuro : CorPressionadoClaro;

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!e.Item.Enabled)
                {
                    TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle,
                        escuro ? CorDesabilitadoEscuro : CorDesabilitadoClaro, e.TextFormat);
                    return;
                }

                base.OnRenderItemText(e);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                var marcado = e.Item is ToolStripButton { Checked: true };
                if (!e.Item.Enabled || (!e.Item.Selected && !e.Item.Pressed && !marcado))
                    return;

                var cor = e.Item.Pressed || marcado ? Pressionado : Destaque;
                PreencherArredondado(e.Graphics, new Rectangle(Point.Empty, e.Item.Size), cor, 0);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.IsOnDropDown)
                {
                    base.OnRenderMenuItemBackground(e);
                    return;
                }

                if (e.Item.Selected && e.Item.Enabled)
                    PreencherArredondado(e.Graphics, new Rectangle(Point.Empty, e.Item.Size), Destaque, 4);
            }

            // Barra principal sem moldura, só uma linha fina embaixo separando do conteúdo.
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (e.ToolStrip is ToolStripDropDown)
                {
                    base.OnRenderToolStripBorder(e);
                    return;
                }

                using var caneta = new Pen(Borda);
                e.Graphics.DrawLine(caneta, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
            }

            private static void PreencherArredondado(Graphics grafico, Rectangle area, Color cor, int recuoHorizontal)
            {
                area.Inflate(-recuoHorizontal, 0);
                area.Width -= 1;
                area.Height -= 1;
                if (area.Width <= Raio * 2 || area.Height <= Raio * 2)
                    return;

                var suavizacao = grafico.SmoothingMode;
                grafico.SmoothingMode = SmoothingMode.AntiAlias;
                using var caminho = CriarRetanguloArredondado(area, Raio);
                using var pincel = new SolidBrush(cor);
                grafico.FillPath(pincel, caminho);
                grafico.SmoothingMode = suavizacao;
            }
        }

        private sealed class TabelaCoresToolStrip(bool escuro) : ProfessionalColorTable
        {
            private Color Painel => escuro ? CorPainelEscuro : CorPainelClaro;
            private Color Borda => escuro ? CorBordaEscuro : CorBordaClaro;
            private Color Destaque => escuro ? CorDestaqueEscuro : CorDestaqueClaro;
            private Color Pressionado => escuro ? CorPressionadoEscuro : CorPressionadoClaro;

            public override Color ToolStripGradientBegin => Painel;
            public override Color ToolStripGradientMiddle => Painel;
            public override Color ToolStripGradientEnd => Painel;
            public override Color ToolStripBorder => Borda;
            public override Color ButtonSelectedHighlight => Destaque;
            public override Color ButtonSelectedGradientBegin => Destaque;
            public override Color ButtonSelectedGradientMiddle => Destaque;
            public override Color ButtonSelectedGradientEnd => Destaque;
            public override Color ButtonSelectedBorder => Borda;
            public override Color ButtonPressedHighlight => Pressionado;
            public override Color ButtonPressedGradientBegin => Pressionado;
            public override Color ButtonPressedGradientMiddle => Pressionado;
            public override Color ButtonPressedGradientEnd => Pressionado;
            public override Color ButtonPressedBorder => Borda;
            public override Color ButtonCheckedHighlight => Pressionado;
            public override Color ButtonCheckedHighlightBorder => Borda;
            public override Color ButtonCheckedGradientBegin => Pressionado;
            public override Color ButtonCheckedGradientMiddle => Pressionado;
            public override Color ButtonCheckedGradientEnd => Pressionado;
            public override Color SeparatorDark => Borda;
            public override Color SeparatorLight => Painel;
            public override Color ToolStripDropDownBackground => Painel;
            public override Color ImageMarginGradientBegin => Painel;
            public override Color ImageMarginGradientMiddle => Painel;
            public override Color ImageMarginGradientEnd => Painel;
            public override Color MenuBorder => Borda;
            public override Color MenuItemBorder => Borda;
            public override Color MenuItemSelected => Destaque;
            public override Color MenuItemSelectedGradientBegin => Destaque;
            public override Color MenuItemSelectedGradientEnd => Destaque;
        }
    }
}
