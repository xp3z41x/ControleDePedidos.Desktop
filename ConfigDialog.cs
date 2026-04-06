namespace CONTROLE_DE_PEDIDOS
{
    public class ConfigDialog : Form
    {
        private TextBox txtIP = null!;
        private TextBox txtPorta = null!;
        private TextBox txtUsuario = null!;
        private TextBox txtSenha = null!;
        private TextBox txtBancoIntegra = null!;
        private TextBox txtBancoControle = null!;
        private TextBox txtFilial = null!;
        private TextBox txtOrcamentoMinimo = null!;
        private TextBox txtFilialFiscal = null!;
        private Button btnSalvar = null!;
        private Button btnCancelar = null!;
        private Button btnTestar = null!;
        private Label lblStatus = null!;

        private const int MargemX = 24;
        private const int MargemTop = 20;

        public AppConfig Config { get; private set; }

        public ConfigDialog(AppConfig config)
        {
            Config = config;
            InitializeUI();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Configurações";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            DoubleBuffered = true;

            // ---- Painel de botoes (dock bottom, adicionado primeiro) ----
            var panelBotoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Theme.BgPanel
            };
            panelBotoes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelBotoes.Width, 0);
            };

            btnCancelar = new Button { Text = "Cancelar", Size = new Size(100, 32) };
            Theme.EstilizarBotao(btnCancelar);
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            btnSalvar = new Button { Text = "Salvar", Size = new Size(100, 32) };
            Theme.EstilizarBotao(btnSalvar, primario: true);
            btnSalvar.Click += BtnSalvar_Click;

            btnTestar = new Button { Text = "Testar", Size = new Size(100, 32) };
            Theme.EstilizarBotao(btnTestar);
            btnTestar.Click += BtnTestar_Click;

            int by = (panelBotoes.Height - 32) / 2;
            btnCancelar.Location = new Point(0, by);
            btnSalvar.Location = new Point(0, by);
            btnTestar.Location = new Point(0, by);

            panelBotoes.Controls.Add(btnCancelar);
            panelBotoes.Controls.Add(btnSalvar);
            panelBotoes.Controls.Add(btnTestar);

            panelBotoes.Resize += (_, _) =>
            {
                int rx = panelBotoes.Width - MargemX;
                btnCancelar.Location = new Point(rx - btnCancelar.Width, btnCancelar.Top);
                btnSalvar.Location = new Point(btnCancelar.Left - btnSalvar.Width - 8, btnCancelar.Top);
                btnTestar.Location = new Point(btnSalvar.Left - btnTestar.Width - 8, btnCancelar.Top);
            };

            // ---- Container de campos (dock fill) ----
            var container = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.BgDark
            };

            int contentWidth = 440 - (MargemX * 2);
            int y = MargemTop;

            // Secao: Servidor
            y = AdicionarSecao(container, "SERVIDOR", y, contentWidth);
            y = AdicionarCampo(container, "IP Servidor", out txtIP, Config.ServidorIP, y, contentWidth);
            y = AdicionarCampo(container, "Porta", out txtPorta, Config.Porta, y, contentWidth);
            y = AdicionarCampo(container, "Usuário", out txtUsuario, Config.Usuario, y, contentWidth);
            y = AdicionarCampo(container, "Senha", out txtSenha, Config.Senha, y, contentWidth, true);

            y += 10;

            // Secao: Bancos
            y = AdicionarSecao(container, "BANCOS DE DADOS", y, contentWidth);
            y = AdicionarCampo(container, "Banco Integra", out txtBancoIntegra, Config.BancoIntegra, y, contentWidth);
            y = AdicionarCampo(container, "Banco Controle", out txtBancoControle, Config.BancoControle, y, contentWidth);

            y += 10;

            // Secao: Filtros
            y = AdicionarSecao(container, "FILTROS DE BUSCA", y, contentWidth);
            y = AdicionarCampo(container, "Filial", out txtFilial, Config.Filial.ToString(), y, contentWidth);
            y = AdicionarCampo(container, "Orçamento Mínimo", out txtOrcamentoMinimo, Config.OrcamentoMinimo.ToString(), y, contentWidth);

            y += 10;

            // Secao: Fiscal
            y = AdicionarSecao(container, "FISCAL", y, contentWidth);
            y = AdicionarCampo(container, "Filial Fiscal", out txtFilialFiscal, Config.FilialFiscal.ToString(), y, contentWidth);

            // Status
            lblStatus = new Label
            {
                Text = "",
                AutoSize = false,
                Size = new Size(contentWidth, 24),
                Location = new Point(MargemX, y + 8),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Theme.FontSmall
            };
            container.Controls.Add(lblStatus);
            y += 40;

            // ---- Montar form ----
            Controls.Add(container);
            Controls.Add(panelBotoes);

            // Calcular altura ideal: conteudo + botoes + bordas
            int alturaConteudo = y + MargemTop;
            int alturaTotal = alturaConteudo + panelBotoes.Height;
            ClientSize = new Size(440, alturaTotal);

            ResumeLayout(false);
            PerformLayout();
        }

        private int AdicionarSecao(Panel container, string titulo, int y, int width)
        {
            var lbl = new Label
            {
                Text = titulo,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = Theme.Accent,
                AutoSize = true,
                Location = new Point(MargemX, y)
            };
            container.Controls.Add(lbl);

            var linha = new Panel
            {
                Size = new Size(width, 1),
                Location = new Point(MargemX, y + 18),
                BackColor = Theme.Border
            };
            container.Controls.Add(linha);

            return y + 28;
        }

        private int AdicionarCampo(Panel container, string label, out TextBox textBox, string value, int y, int width, bool isPassword = false)
        {
            var lbl = new Label
            {
                Text = label,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true,
                Location = new Point(MargemX, y)
            };
            container.Controls.Add(lbl);

            textBox = new TextBox
            {
                Text = value,
                Size = new Size(width, 26),
                Location = new Point(MargemX, y + 18)
            };
            Theme.EstilizarTextBox(textBox);
            if (isPassword) textBox.UseSystemPasswordChar = true;
            container.Controls.Add(textBox);

            return y + 48;
        }

        private async void BtnTestar_Click(object? sender, EventArgs e)
        {
            lblStatus.ForeColor = Theme.Warning;
            lblStatus.Text = "Testando conexão...";
            btnTestar.Enabled = false;

            AplicarConfig();
            bool ok = await DatabaseService.Instancia.TestarConexao(Config);

            lblStatus.ForeColor = ok ? Theme.Success : Theme.Error;
            lblStatus.Text = ok ? "Conexão bem-sucedida!" : "Falha na conexão. Verifique os dados.";
            btnTestar.Enabled = true;
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            AplicarConfig();
            Config.Salvar();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AplicarConfig()
        {
            Config.ServidorIP = txtIP.Text.Trim();
            Config.Porta = txtPorta.Text.Trim();
            Config.Usuario = txtUsuario.Text.Trim();
            Config.Senha = txtSenha.Text.Trim();
            Config.BancoIntegra = txtBancoIntegra.Text.Trim();
            Config.BancoControle = txtBancoControle.Text.Trim();
            Config.Filial = int.TryParse(txtFilial.Text.Trim(), out var f) ? f : Config.Filial;
            Config.OrcamentoMinimo = int.TryParse(txtOrcamentoMinimo.Text.Trim(), out var o) ? o : Config.OrcamentoMinimo;
            Config.FilialFiscal = int.TryParse(txtFilialFiscal.Text.Trim(), out var ff) ? ff : Config.FilialFiscal;
        }
    }
}
