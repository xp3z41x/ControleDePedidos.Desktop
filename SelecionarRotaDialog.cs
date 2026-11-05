namespace CONTROLE_DE_PEDIDOS
{
    public class SelecionarRotaDialog : FormEscalavel
    {
        private ComboBox cmbRota = null!;
        private Label lblDica = null!;
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
            ClientSize = Dpi(350, 160);

            var lblRota = new Label
            {
                Text = "Qual a rota deste pedido?",
                Location = DpiPt(16, 16),
                AutoSize = true,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular
            };

            cmbRota = new ComboBox
            {
                Location = DpiPt(16, 42),
                Size = Dpi(318, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                DisplayMember = "Nome"
            };

            // Dica de estado (carregando / sem rotas / erro): sem ela, o cadastro vazio
            // mostrava só uma lista muda e o Confirmar devolvia "Selecione uma rota.".
            lblDica = new Label
            {
                Text = "Carregando rotas…",
                Location = DpiPt(16, 78),
                Size = Dpi(318, 28), // cabe 2 linhas (o texto quebra sozinho)
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Size = Dpi(90, 32),
                Location = DpiPt(244, 108),
                DialogResult = DialogResult.Cancel
            };
            Theme.EstilizarBotao(btnCancelar);

            btnOk = new Button
            {
                Text = "Confirmar",
                Size = Dpi(90, 32),
                Location = DpiPt(146, 108),
                DialogResult = DialogResult.OK,
                Enabled = false // habilitado só depois que houver rota para escolher
            };
            Theme.EstilizarBotao(btnOk, primario: true);

            AcceptButton = btnOk;
            CancelButton = btnCancelar;

            Controls.AddRange([lblRota, cmbRota, lblDica, btnOk, btnCancelar]);

            ResumeLayout(false);
            PerformLayout();
        }

        private async Task CarregarRotasAsync()
        {
            try
            {
                var db = DatabaseService.Instancia;
                if (!db.ControleConectado)
                {
                    MostrarDica($"Sem conexão com o banco {db.Config.BancoControle}.", Theme.Error);
                    return;
                }

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

                if (IsDisposed) return; // diálogo fechado durante a carga

                if (lista is not { Count: > 0 })
                {
                    cmbRota.Enabled = false;
                    MostrarDica("Nenhuma rota cadastrada — cadastre em ROTAS › Cadastro de Rotas.", Theme.Warning);
                    return;
                }

                cmbRota.DataSource = lista;
                cmbRota.SelectedIndex = 0;
                lblDica.Visible = false;
                btnOk.Enabled = true;
                cmbRota.Focus();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao carregar rotas", ex);
                if (!IsDisposed)
                    MostrarDica($"Erro ao carregar rotas: {ex.Message}", Theme.Error);
            }
        }

        private void MostrarDica(string texto, Color cor)
        {
            lblDica.Text = texto;
            lblDica.ForeColor = cor;
            lblDica.Visible = true;
        }
    }
}
