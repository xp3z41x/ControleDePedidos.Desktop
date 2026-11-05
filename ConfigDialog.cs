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
        private CheckBox chkSeparacaoPaisagem = null!;
        private CheckBox chkSeparacaoOcultarCancelados = null!;
        private CheckBox chkSeparacaoOrdenarPorLocal = null!;
        private CheckBox chkSeparacaoCasasDecimais = null!;
        private CheckBox chkCopiaPaisagem = null!;
        private CheckBox chkCopiaOrdenarAlfabetica = null!;
        private TextBox txtEmpresaRazao = null!;
        private TextBox txtEmpresaCnpj = null!;
        private TextBox txtEmpresaIE = null!;
        private TextBox txtEmpresaEndereco = null!;
        private TextBox txtEmpresaCidadeUfCep = null!;
        private TextBox txtEmpresaFone = null!;
        private TextBox txtEmpresaEmail = null!;
        private TextBox txtEmpresaWhats = null!;
        private Button btnSalvar = null!;
        private Button btnCancelar = null!;
        private Button btnTestar = null!;
        private Label lblStatus = null!;

        private const int MargemX = 24;
        private const int MargemTop = 20;

        /// <summary>
        /// CÓPIA de trabalho da configuração. O dialog nunca altera a instância viva
        /// (_db.Config): "Testar" aplica os campos aqui e, se o usuário cancelar, o app
        /// continua com a config original. Quem chama troca a instância via
        /// DatabaseService.AtualizarConfig(dialog.Config) só no OK.
        /// </summary>
        public AppConfig Config { get; private set; }

        public ConfigDialog(AppConfig config)
        {
            Config = Clonar(config);
            InitializeUI();
        }

        // Cópia rasa de todas as propriedades públicas graváveis (todas são string/int/bool).
        // Reflexão em vez de lista manual: chave nova em AppConfig é copiada sem tocar aqui.
        private static AppConfig Clonar(AppConfig origem)
        {
            var copia = new AppConfig();
            foreach (var prop in typeof(AppConfig).GetProperties())
            {
                if (prop.CanRead && prop.CanWrite && prop.GetIndexParameters().Length == 0)
                    prop.SetValue(copia, prop.GetValue(origem));
            }
            return copia;
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

            int larguraJanela = LogicalToDeviceUnits(440);
            int contentWidth = larguraJanela - (MargemX * 2);
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

            y += 10;

            // Secao: Relatorio de Separacao
            y = AdicionarSecao(container, "RELATÓRIO DE SEPARAÇÃO", y, contentWidth);
            y = AdicionarCheckBox(container, "Imprimir em paisagem (padrão: retrato)", out chkSeparacaoPaisagem, Config.SeparacaoPaisagem, y);
            y = AdicionarCheckBox(container, "Ocultar itens cancelados", out chkSeparacaoOcultarCancelados, Config.SeparacaoOcultarCancelados, y);
            y = AdicionarCheckBox(container, "Ordenar itens por LOCAL (sub-ordem descrição)", out chkSeparacaoOrdenarPorLocal, Config.SeparacaoOrdenarPorLocal, y);
            y = AdicionarCheckBox(container, "Exibir quantidades/estoques com 2 casas decimais", out chkSeparacaoCasasDecimais, Config.SeparacaoCasasDecimais, y);

            y += 10;

            // Secao: Copia de Pedido
            y = AdicionarSecao(container, "CÓPIA DE PEDIDO", y, contentWidth);
            y = AdicionarCheckBox(container, "Imprimir em paisagem (padrão: retrato)", out chkCopiaPaisagem, Config.CopiaPaisagem, y);
            y = AdicionarCheckBox(container, "Ordenar itens alfabeticamente (padrão: ordem do pedido)", out chkCopiaOrdenarAlfabetica, Config.CopiaOrdenarAlfabetica, y);

            y += 10;

            // Secao: Dados da empresa (cabecalho da Copia de Pedido)
            y = AdicionarSecao(container, "DADOS DA EMPRESA (CÓPIA DE PEDIDO)", y, contentWidth);
            y = AdicionarCampo(container, "Razão Social", out txtEmpresaRazao, Config.EmpresaRazaoSocial, y, contentWidth);
            y = AdicionarCampo(container, "CNPJ", out txtEmpresaCnpj, Config.EmpresaCnpj, y, contentWidth);
            y = AdicionarCampo(container, "Inscrição Estadual", out txtEmpresaIE, Config.EmpresaInscricaoEstadual, y, contentWidth);
            y = AdicionarCampo(container, "Endereço (rua, nº, bairro)", out txtEmpresaEndereco, Config.EmpresaEndereco, y, contentWidth);
            y = AdicionarCampo(container, "Cidade / UF / CEP", out txtEmpresaCidadeUfCep, Config.EmpresaCidadeUfCep, y, contentWidth);
            y = AdicionarCampo(container, "Telefone", out txtEmpresaFone, Config.EmpresaTelefone, y, contentWidth);
            y = AdicionarCampo(container, "E-mail", out txtEmpresaEmail, Config.EmpresaEmail, y, contentWidth);
            y = AdicionarCampo(container, "WhatsApp", out txtEmpresaWhats, Config.EmpresaWhatsApp, y, contentWidth);

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
            ClientSize = new Size(larguraJanela, alturaTotal);

            // Em telas de pouca altura (ex.: 1366x768) o dialog é clampado à área útil;
            // o container (AutoScroll) passa a rolar e os botões ficam sempre visíveis.
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);

            ResumeLayout(false);
            PerformLayout();
        }

        private int AdicionarSecao(Panel container, string titulo, int y, int width)
        {
            var lbl = new Label
            {
                Text = titulo,
                Font = Theme.FontSecaoTitulo,
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

        private int AdicionarCheckBox(Panel container, string label, out CheckBox checkBox, bool value, int y)
        {
            checkBox = new CheckBox
            {
                Text = label,
                Checked = value,
                AutoSize = true,
                Location = new Point(MargemX, y),
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            container.Controls.Add(checkBox);

            return y + 30;
        }

        private async void BtnTestar_Click(object? sender, EventArgs e)
        {
            lblStatus.ForeColor = Theme.Warning;
            lblStatus.Text = "Testando conexão...";
            btnTestar.Enabled = false;

            AplicarConfig();
            // Testa os dois bancos e reporta cada um (antes só o Integra era testado).
            var r = await DatabaseService.Instancia.TestarConexao(Config);
            if (IsDisposed) return; // dialog fechado durante o teste

            string textoControle = r.ErroControle == null ? "OK"
                : r.ControleSeraCriado ? "será criado" : "FALHA";
            lblStatus.ForeColor = r.IntegraOk && r.ControleOk ? Theme.Success
                : r.IntegraOk || r.ControleOk ? Theme.Warning : Theme.Error;
            lblStatus.Text = $"{Config.BancoIntegra}: {(r.IntegraOk ? "OK" : "FALHA")}  ·  {Config.BancoControle}: {textoControle}";
            btnTestar.Enabled = true;

            if (!r.IntegraOk || !r.ControleOk || r.ControleSeraCriado)
            {
                var detalhes = $"Banco {Config.BancoIntegra} (Integra, somente leitura):\n  "
                    + (r.IntegraOk ? "Conexão bem-sucedida." : r.ErroIntegra)
                    + $"\n\nBanco {Config.BancoControle} (controle):\n  "
                    + (r.ErroControle == null ? "Conexão bem-sucedida."
                       : r.ControleSeraCriado ? "Ainda não existe; será criado automaticamente ao conectar."
                       : r.ErroControle);
                // Owner = este diálogo (modal), não o Form1.
                MessageBox.Show(this, detalhes, "Teste de Conexão", MessageBoxButtons.OK,
                    r.IntegraOk && r.ControleOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }

        private void BtnSalvar_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtPorta.Text.Trim(), out var porta) || porta < 1 || porta > 65535)
            {
                MessageBox.Show(this, "Porta inválida (deve estar entre 1 e 65535).", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPorta.Focus();
                return;
            }
            if (!int.TryParse(txtFilial.Text.Trim(), out var filial) || filial <= 0)
            {
                MessageBox.Show(this, "Filial deve ser um número maior que zero.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFilial.Focus();
                return;
            }
            if (!int.TryParse(txtOrcamentoMinimo.Text.Trim(), out var orcMin) || orcMin < 0)
            {
                MessageBox.Show(this, "Orçamento Mínimo deve ser zero ou maior.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOrcamentoMinimo.Focus();
                return;
            }
            if (!int.TryParse(txtFilialFiscal.Text.Trim(), out var filialFiscal) || filialFiscal < 0)
            {
                MessageBox.Show(this, "Filial Fiscal deve ser zero ou maior (0 = não configurada).", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFilialFiscal.Focus();
                return;
            }

            AplicarConfig();
            try
            {
                Config.Salvar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao salvar configurações:\n{ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
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
            Config.SeparacaoPaisagem = chkSeparacaoPaisagem.Checked;
            Config.SeparacaoOcultarCancelados = chkSeparacaoOcultarCancelados.Checked;
            Config.SeparacaoOrdenarPorLocal = chkSeparacaoOrdenarPorLocal.Checked;
            Config.SeparacaoCasasDecimais = chkSeparacaoCasasDecimais.Checked;
            Config.CopiaPaisagem = chkCopiaPaisagem.Checked;
            Config.CopiaOrdenarAlfabetica = chkCopiaOrdenarAlfabetica.Checked;
            Config.EmpresaRazaoSocial = txtEmpresaRazao.Text.Trim();
            Config.EmpresaCnpj = txtEmpresaCnpj.Text.Trim();
            Config.EmpresaInscricaoEstadual = txtEmpresaIE.Text.Trim();
            Config.EmpresaEndereco = txtEmpresaEndereco.Text.Trim();
            Config.EmpresaCidadeUfCep = txtEmpresaCidadeUfCep.Text.Trim();
            Config.EmpresaTelefone = txtEmpresaFone.Text.Trim();
            Config.EmpresaEmail = txtEmpresaEmail.Text.Trim();
            Config.EmpresaWhatsApp = txtEmpresaWhats.Text.Trim();
        }
    }
}
