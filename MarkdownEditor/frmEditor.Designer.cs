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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmEditor));
            trvPastas = new TreeView();
            SuspendLayout();
            // 
            // trvPastas
            // 
            trvPastas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            trvPastas.Location = new Point(12, 12);
            trvPastas.Name = "trvPastas";
            trvPastas.Size = new Size(241, 447);
            trvPastas.TabIndex = 0;
            // 
            // frmEditor
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(663, 471);
            Controls.Add(trvPastas);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(573, 276);
            Name = "frmEditor";
            Text = "Visualizador e editor de Markdown";
            ResumeLayout(false);
        }

        #endregion

        private TreeView trvPastas;
    }
}