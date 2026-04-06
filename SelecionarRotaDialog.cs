namespace CONTROLE_DE_PEDIDOS
{
    public class SelecionarRotaDialog : Form
    {
        private ComboBox cmbRota = null!;
        private Button btnOk = null!;
        private Button btnCancelar = null!;

        public string RotaSelecionada => (cmbRota.SelectedItem as RotaItem)?.Nome ?? "";

        public SelecionarRotaDialog()
        {
            InitializeUI();
            _ = CarregarRotasAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Selecionar Rota";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            ClientSize = new Size(350, 140);

            var lblRota = new Label
            {
                Text = "Qual a rota deste pedido?",
                Location = new Point(16, 16),
                AutoSize = true,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular
            };

            cmbRota = new ComboBox
            {
                Location = new Point(16, 42),
                Size = new Size(318, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                DisplayMember = "Nome"
            };

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Size = new Size(90, 32),
                Location = new Point(244, 88),
                DialogResult = DialogResult.Cancel
            };
            Theme.EstilizarBotao(btnCancelar);

            btnOk = new Button
            {
                Text = "Confirmar",
                Size = new Size(90, 32),
                Location = new Point(146, 88),
                DialogResult = DialogResult.OK
            };
            Theme.EstilizarBotao(btnOk, primario: true);

            AcceptButton = btnOk;
            CancelButton = btnCancelar;

            Controls.AddRange([lblRota, cmbRota, btnOk, btnCancelar]);

            ResumeLayout(false);
            PerformLayout();
        }

        private async Task CarregarRotasAsync()
        {
            try
            {
                var db = DatabaseService.Instancia;
                if (!db.ControleConectado) return;

                var lista = await db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<RotaItem>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, nome FROM cadastro_rotas ORDER BY nome";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        result.Add(new RotaItem { Id = reader.GetInt32(0), Nome = reader.GetString(1) });
                    return result;
                });

                cmbRota.DataSource = lista;
                if (lista?.Count > 0) cmbRota.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar rotas: {ex.Message}");
            }
        }
    }
}
