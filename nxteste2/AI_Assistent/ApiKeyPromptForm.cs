using System;
using System.Drawing;
using System.Windows.Forms;

namespace nxteste2
{
    // Caixa de diálogo simples pra digitar a chave da API Anthropic na
    // primeira vez que o assistente é usado. Campo mascarado; a chave só é
    // usada localmente (ApiKeyStore) e enviada pra API da Anthropic — não é
    // logada nem exibida em nenhum outro lugar.
    public class ApiKeyPromptForm : Form
    {
        private TextBox txtKey;
        public string EnteredKey { get; private set; }

        public ApiKeyPromptForm()
        {
            Text = "Anthropic API Key";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(360, 170);

            Label lbl = new Label
            {
                Text = "Paste your Anthropic API key\n(console.anthropic.com → Settings → API Keys).\n"
                     + "It is stored locally, encrypted, and only ever\n"
                     + "sent to the Anthropic API.",
                Location = new Point(12, 12),
                Size = new Size(336, 72),
                Font = new Font("Segoe UI", 8.5F),
            };

            txtKey = new TextBox
            {
                Location = new Point(12, 90),
                Size = new Size(336, 23),
                UseSystemPasswordChar = true,
            };

            Button btnOk = new Button
            {
                Text = "Save",
                Location = new Point(192, 126),
                Size = new Size(75, 28),
                DialogResult = DialogResult.OK,
            };
            Button btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(273, 126),
                Size = new Size(75, 28),
                DialogResult = DialogResult.Cancel,
            };

            btnOk.Click += (s, e) => EnteredKey = txtKey.Text.Trim();

            Controls.Add(lbl);
            Controls.Add(txtKey);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
