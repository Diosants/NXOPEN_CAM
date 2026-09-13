namespace nxteste2
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        // A interface inteira agora é montada em código dentro de BuildUi()
        // no Form1.cs — mais fácil de manter do que coordenadas fixas geradas
        // pelo designer visual. InitializeComponent só cuida das propriedades
        // básicas do Form.
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(380, 680);
            this.MinimumSize = new System.Drawing.Size(320, 480);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NXCAM FBM - Mold Plates Machining";
            this.ResumeLayout(false);
        }
    }
}
