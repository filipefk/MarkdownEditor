namespace MarkdownEditor
{
    partial class frmEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmEditor));
            spcPrincipal = new SplitContainer();
            trvPastas = new TreeView();
            imlIcones = new ImageList(components);
            wvwPreview = new Microsoft.Web.WebView2.WinForms.WebView2();
            tsrNavegacao = new ToolStrip();
            tsbArvore = new ToolStripButton();
            tssArvore = new ToolStripSeparator();
            tsbVoltar = new ToolStripButton();
            tsbAvancar = new ToolStripButton();
            ((System.ComponentModel.ISupportInitialize)spcPrincipal).BeginInit();
            spcPrincipal.Panel1.SuspendLayout();
            spcPrincipal.Panel2.SuspendLayout();
            spcPrincipal.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)wvwPreview).BeginInit();
            tsrNavegacao.SuspendLayout();
            SuspendLayout();
            // 
            // spcPrincipal
            // 
            spcPrincipal.Dock = DockStyle.Fill;
            spcPrincipal.FixedPanel = FixedPanel.Panel1;
            spcPrincipal.Location = new Point(0, 0);
            spcPrincipal.Name = "spcPrincipal";
            // 
            // spcPrincipal.Panel1
            // 
            spcPrincipal.Panel1.Controls.Add(trvPastas);
            // 
            // spcPrincipal.Panel2
            // 
            spcPrincipal.Panel2.Controls.Add(wvwPreview);
            spcPrincipal.Size = new Size(1080, 597);
            spcPrincipal.SplitterDistance = 280;
            spcPrincipal.TabIndex = 0;
            // 
            // trvPastas
            // 
            trvPastas.Dock = DockStyle.Fill;
            trvPastas.HideSelection = false;
            trvPastas.ImageIndex = 0;
            trvPastas.ImageList = imlIcones;
            trvPastas.Location = new Point(0, 0);
            trvPastas.Name = "trvPastas";
            trvPastas.SelectedImageIndex = 0;
            trvPastas.Size = new Size(280, 597);
            trvPastas.TabIndex = 0;
            trvPastas.BeforeExpand += trvPastas_BeforeExpand;
            trvPastas.AfterSelect += trvPastas_AfterSelect;
            // 
            // imlIcones
            // 
            imlIcones.ColorDepth = ColorDepth.Depth32Bit;
            imlIcones.ImageSize = new Size(16, 16);
            imlIcones.TransparentColor = Color.Transparent;
            // 
            // wvwPreview
            // 
            wvwPreview.AllowExternalDrop = true;
            wvwPreview.CreationProperties = null;
            wvwPreview.DefaultBackgroundColor = Color.White;
            wvwPreview.Dock = DockStyle.Fill;
            wvwPreview.Location = new Point(0, 0);
            wvwPreview.Name = "wvwPreview";
            wvwPreview.Size = new Size(796, 597);
            wvwPreview.TabIndex = 0;
            wvwPreview.ZoomFactor = 1D;
            //
            // tsrNavegacao
            //
            tsrNavegacao.GripStyle = ToolStripGripStyle.Hidden;
            tsrNavegacao.Items.AddRange(new ToolStripItem[] { tsbArvore, tssArvore, tsbVoltar, tsbAvancar });
            tsrNavegacao.Location = new Point(0, 0);
            tsrNavegacao.Name = "tsrNavegacao";
            tsrNavegacao.Size = new Size(1080, 25);
            tsrNavegacao.TabIndex = 1;
            //
            // tsbArvore
            //
            tsbArvore.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbArvore.Name = "tsbArvore";
            tsbArvore.Size = new Size(110, 22);
            tsbArvore.Text = "◧ Esconder árvore";
            tsbArvore.Click += tsbArvore_Click;
            //
            // tssArvore
            //
            tssArvore.Name = "tssArvore";
            tssArvore.Size = new Size(6, 25);
            //
            // tsbVoltar
            //
            tsbVoltar.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbVoltar.Enabled = false;
            tsbVoltar.Name = "tsbVoltar";
            tsbVoltar.Size = new Size(60, 22);
            tsbVoltar.Text = "◀ Voltar";
            tsbVoltar.Click += tsbVoltar_Click;
            //
            // tsbAvancar
            //
            tsbAvancar.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tsbAvancar.Enabled = false;
            tsbAvancar.Name = "tsbAvancar";
            tsbAvancar.Size = new Size(68, 22);
            tsbAvancar.Text = "Avançar ▶";
            tsbAvancar.Click += tsbAvancar_Click;
            // 
            // frmEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1080, 597);
            Controls.Add(spcPrincipal);
            Controls.Add(tsrNavegacao);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(573, 276);
            Name = "frmEditor";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Visualizador e editor de Markdown";
            Load += frmEditor_Load;
            spcPrincipal.Panel1.ResumeLayout(false);
            spcPrincipal.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)spcPrincipal).EndInit();
            spcPrincipal.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)wvwPreview).EndInit();
            tsrNavegacao.ResumeLayout(false);
            tsrNavegacao.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private SplitContainer spcPrincipal;
        private TreeView trvPastas;
        private ImageList imlIcones;
        private Microsoft.Web.WebView2.WinForms.WebView2 wvwPreview;
        private ToolStrip tsrNavegacao;
        private ToolStripButton tsbArvore;
        private ToolStripSeparator tssArvore;
        private ToolStripButton tsbVoltar;
        private ToolStripButton tsbAvancar;
    }
}
