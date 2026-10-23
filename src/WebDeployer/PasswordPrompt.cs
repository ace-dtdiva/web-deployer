using System.Drawing;
using System.Windows.Forms;

namespace WebDeployer
{
    /// <summary>Finestra che chiede la password FTP (che non viene mai salvata su disco).</summary>
    internal sealed class PasswordPrompt : Form
    {
        private readonly TextBox _password;

        private PasswordPrompt(string message)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Password FTP";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            _password = new TextBox { UseSystemPasswordChar = true, Width = 340, Margin = new Padding(0, 0, 0, 4) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, AutoSize = true };
            AcceptButton = ok;
            CancelButton = cancel;

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Location = new Point(12, 12) };
            layout.Controls.Add(new Label { Text = message, AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(0, 0, 0, 6) });
            layout.Controls.Add(_password);
            layout.Controls.Add(new Label
            {
                Text = "Non viene salvata: resta in memoria finché WebDeployer è aperto.",
                AutoSize = true,
                MaximumSize = new Size(340, 0),
                ForeColor = SystemColors.GrayText,
                Margin = Padding.Empty,
            });
            layout.Controls.Add(buttons);
            Controls.Add(layout);
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>Restituisce la password inserita, oppure null se l'utente annulla.</summary>
        public static string Ask(IWin32Window owner, string message)
        {
            using (var dialog = new PasswordPrompt(message))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._password.Text : null;
        }
    }
}
