namespace CONTROLE_DE_PEDIDOS
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null!;
        private MenuStrip menuStrip = null!;
        private Panel statusBarPanel = null!;
        private Panel panelIndicador = null!;
        private Label lblStatusConexao = null!;
        private Button btnConfig = null!;
        private Panel panelContainer = null!;
        private OptimizedDataGridView dgvOrcamentos = null!;
        private Panel panelOrcamentos = null!;
        private Label lblTituloOrcamentos = null!;
        private Label lblContagem = null!;
        private Button btnIgnorar = null!;
        private Button btnAprovar = null!;
        private Button btnVerIgnorados = null!;
        private Panel panelPedidos = null!;
        private Label lblTituloPedidos = null!;
        private Label lblContagemPedidos = null!;
        private OptimizedDataGridView dgvPedidos = null!;
        private Button btnAlterarRota = null!;
        private Button btnExcluirPedido = null!;
        private Button btnAvancarStatus = null!;
        private Button btnRetornarStatus = null!;
        private Button btnTravar = null!;
        private Button btnDestravar = null!;
        private Button btnAssumir = null!;
        private Button btnLargar = null!;
        private Button btnRefresh = null!;
        private System.Windows.Forms.Timer timerAutoRefresh = null!;
        private Panel panelDetalhes = null!;
        private Label lblDetCidade = null!;
        private Label lblDetEstado = null!;
        private Label lblDetValorTotal = null!;
        private Label lblDetTexto01 = null!;
        private Label lblDetTexto02 = null!;
        private Label lblDetTexto03 = null!;
        private Label lblDetTexto04 = null!;
        private Label lblDetObservacao = null!;
        private Label lblDetVendedor = null!;

        // Layout constants
        private const int MaxContainerWidth = 1870;
        private const int LeftColumnWidth = 420;
        private const int ColumnGap = 16;
        private const int ContainerPadding = 32;
        private const int OrcamentosHeight = 280;
        private const int RefreshButtonHeight = 34;
        private const int InnerGap = 8;

        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            SuspendLayout();

            components = new System.ComponentModel.Container();
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1400, 720);
            MinimumSize = new Size(1000, 600);
            Text = "Controle de Pedidos";
            var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (exeIcon != null)
                Icon = exeIcon;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            // ================================================================
            // MENU SUPERIOR
            // ================================================================
            menuStrip = new MenuStrip
            {
                BackColor = Theme.BgPanel,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                Padding = new Padding(8, 2, 0, 2),
                Renderer = new ThemeMenuRenderer()
            };

            var menuRotas = new ToolStripMenuItem("ROTAS")
            {
                ForeColor = Theme.TextPrimary
            };
            var menuCadastroRotas = new ToolStripMenuItem("Cadastro de Rotas")
            {
                ForeColor = Theme.TextPrimary
            };
            menuCadastroRotas.Click += MenuCadastroRotas_Click;
            menuRotas.DropDownItems.Add(menuCadastroRotas);

            var menuPedidos = new ToolStripMenuItem("PEDIDOS")
            {
                ForeColor = Theme.TextPrimary
            };
            var menuExclusaoMassa = new ToolStripMenuItem("Exclusão em Massa")
            {
                ForeColor = Theme.TextPrimary
            };
            menuExclusaoMassa.Click += MenuExclusaoMassa_Click;
            menuPedidos.DropDownItems.Add(menuExclusaoMassa);

            var menuRelatorios = new ToolStripMenuItem("RELATÓRIOS")
            {
                ForeColor = Theme.TextPrimary
            };
            var menuRelatorioCarga = new ToolStripMenuItem("Relatório de Carga")
            {
                ForeColor = Theme.TextPrimary
            };
            menuRelatorioCarga.Click += MenuRelatorioCarga_Click;
            menuRelatorios.DropDownItems.Add(menuRelatorioCarga);

            var menuRelatorioNfe = new ToolStripMenuItem("Relatório de NF-e")
            {
                ForeColor = Theme.TextPrimary
            };
            menuRelatorioNfe.Click += MenuRelatorioNfe_Click;
            menuRelatorios.DropDownItems.Add(menuRelatorioNfe);

            menuStrip.Items.Add(menuRotas);
            menuStrip.Items.Add(menuPedidos);
            menuStrip.Items.Add(menuRelatorios);
            MainMenuStrip = menuStrip;

            // ================================================================
            // STATUS BAR (rodape)
            // ================================================================
            statusBarPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                BackColor = Theme.BgStatusBar,
                Padding = new Padding(16, 0, 16, 0)
            };
            statusBarPanel.SuspendLayout();

            panelIndicador = new Panel
            {
                Size = new Size(10, 10),
                BackColor = Theme.TextMuted
            };
            panelIndicador.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(panelIndicador.BackColor);
                e.Graphics.Clear(statusBarPanel.BackColor);
                e.Graphics.FillEllipse(brush, 0, 0, 9, 9);
            };

            lblStatusConexao = new Label
            {
                Text = "Verificando...",
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Font = Theme.FontSmall
            };

            btnConfig = new Button
            {
                Text = "\u2699",
                Size = new Size(30, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgStatusBar,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontIcon,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnConfig.FlatAppearance.BorderSize = 0;
            btnConfig.FlatAppearance.MouseOverBackColor = Theme.BgCard;
            btnConfig.Click += BtnConfig_Click;

            statusBarPanel.Controls.Add(btnConfig);
            statusBarPanel.Controls.Add(panelIndicador);
            statusBarPanel.Controls.Add(lblStatusConexao);
            statusBarPanel.ResumeLayout(false);

            statusBarPanel.Resize += (_, _) => PosicionarElementosStatusBar();
            lblStatusConexao.TextChanged += (_, _) => BeginInvoke(PosicionarElementosStatusBar);
            lblStatusConexao.Resize += (_, _) => BeginInvoke(PosicionarElementosStatusBar);

            // ================================================================
            // CONTAINER CENTRAL (largura fixa, centralizado)
            // ================================================================
            panelContainer = new Panel
            {
                BackColor = Color.Transparent,
                Size = new Size(100, 100) // recalculado por AtualizarLayoutCentralizado
            };

            // ================================================================
            // PANEL DETALHES (coluna esquerda, topo)
            // ================================================================
            panelDetalhes = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(LeftColumnWidth, 200), // height set by layout
                BackColor = Theme.BgPanel,
                Padding = new Padding(1)
            };
            panelDetalhes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelDetalhes.Width - 1, panelDetalhes.Height - 1);
            };

            var headerDetalhes = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Theme.BgCard
            };

            var lblTituloDetalhes = new Label
            {
                Text = "Detalhes do Orçamento",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 8)
            };
            headerDetalhes.Controls.Add(lblTituloDetalhes);

            var separadorDetalhes = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            var containerDetalhes = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.BgPanel
            };

            int cellY = 0;
            const int cellPadX = 14;
            const int cellWidth = 392;

            Label CriarCelula(string titulo, int altura = 38)
            {
                if (cellY > 0)
                {
                    var sep = new Panel
                    {
                        Location = new Point(cellPadX, cellY),
                        Size = new Size(cellWidth, 1),
                        BackColor = Theme.Border
                    };
                    containerDetalhes.Controls.Add(sep);
                    cellY += 1;
                }

                var lblTit = new Label
                {
                    Text = titulo,
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = Theme.TextMuted,
                    Location = new Point(cellPadX, cellY + 4),
                    AutoSize = true
                };
                containerDetalhes.Controls.Add(lblTit);

                var lblVal = new Label
                {
                    Text = "—",
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Theme.TextPrimary,
                    Location = new Point(cellPadX, cellY + 19),
                    Size = new Size(cellWidth, altura - 22),
                    AutoEllipsis = true
                };
                containerDetalhes.Controls.Add(lblVal);

                cellY += altura;
                return lblVal;
            }

            lblDetValorTotal = CriarCelula("VALOR TOTAL", 42);
            lblDetValorTotal.Font = new Font("Segoe UI Semibold", 13f);
            lblDetValorTotal.ForeColor = Theme.Accent;

            lblDetCidade = CriarCelula("CIDADE");
            lblDetEstado = CriarCelula("UF");
            lblDetVendedor = CriarCelula("VENDEDOR");
            lblDetTexto01 = CriarCelula("FORMA DE PAGAMENTO");
            lblDetTexto02 = CriarCelula("NÚMERO DO PEDIDO");
            lblDetTexto03 = CriarCelula("VOLUMES");
            lblDetTexto04 = CriarCelula("PESO");

            lblDetObservacao = CriarCelula("OBSERVAÇÃO", 72);
            lblDetObservacao.AutoEllipsis = false;
            lblDetObservacao.AutoSize = false;
            lblDetObservacao.MaximumSize = new Size(cellWidth, 50);

            panelDetalhes.Controls.Add(containerDetalhes);
            panelDetalhes.Controls.Add(separadorDetalhes);
            panelDetalhes.Controls.Add(headerDetalhes);

            // ================================================================
            // BOTAO REFRESH (entre detalhes e orcamentos)
            // ================================================================
            btnRefresh = new Button
            {
                Text = "⟳  Atualizar Dados",
                Size = new Size(LeftColumnWidth, 34)
            };
            Theme.EstilizarBotao(btnRefresh);
            btnRefresh.Font = Theme.FontHeader;
            btnRefresh.Click += BtnRefresh_Click;

            // Timer auto-refresh (3 minutos)
            timerAutoRefresh = new System.Windows.Forms.Timer(components)
            {
                Interval = 180000
            };
            timerAutoRefresh.Tick += (_, _) => BtnRefresh_Click(null, EventArgs.Empty);

            // ================================================================
            // PANEL ORCAMENTOS (coluna esquerda, rodape)
            // ================================================================
            panelOrcamentos = new Panel
            {
                Size = new Size(LeftColumnWidth, OrcamentosHeight),
                BackColor = Theme.BgPanel,
                Padding = new Padding(1)
            };
            panelOrcamentos.SuspendLayout();
            panelOrcamentos.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelOrcamentos.Width - 1, panelOrcamentos.Height - 1);
            };

            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.BgCard,
                Padding = new Padding(16, 0, 16, 0)
            };

            lblTituloOrcamentos = new Label
            {
                Text = "Orçamentos Pendentes",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 14)
            };

            lblContagem = new Label
            {
                Text = "",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = true
            };

            btnVerIgnorados = new Button
            {
                Text = "Ignorados",
                Size = new Size(80, 28),
                Font = Theme.FontSmall
            };
            Theme.EstilizarBotao(btnVerIgnorados);
            btnVerIgnorados.Click += BtnVerIgnorados_Click;

            panelHeader.Controls.Add(lblTituloOrcamentos);
            panelHeader.Controls.Add(lblContagem);
            panelHeader.Controls.Add(btnVerIgnorados);
            panelHeader.Resize += (_, _) =>
            {
                lblTituloOrcamentos.Location = new Point(16, (panelHeader.Height - lblTituloOrcamentos.Height) / 2);
                int cy = (panelHeader.Height - btnVerIgnorados.Height) / 2;
                btnVerIgnorados.Location = new Point(panelHeader.Width - btnVerIgnorados.Width - 16, cy);
                lblContagem.Location = new Point(
                    btnVerIgnorados.Left - lblContagem.Width - 12,
                    (panelHeader.Height - lblContagem.Height) / 2);
            };
            lblContagem.TextChanged += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    lblContagem.Location = new Point(
                        btnVerIgnorados.Left - lblContagem.Width - 12,
                        (panelHeader.Height - lblContagem.Height) / 2);
                });
            };

            var separador = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            var panelAcoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Theme.BgCard
            };
            panelAcoes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAcoes.Width, 0);
            };

            btnIgnorar = new Button
            {
                Text = "Ignorar Orçamento",
                Size = new Size(150, 30)
            };
            Theme.EstilizarBotao(btnIgnorar);
            btnIgnorar.ForeColor = Theme.Warning;
            btnIgnorar.Click += BtnIgnorar_Click;

            btnAprovar = new Button
            {
                Text = "Controlar Pedido",
                Size = new Size(140, 30)
            };
            Theme.EstilizarBotao(btnAprovar, primario: true);
            btnAprovar.Click += BtnAprovar_Click;

            panelAcoes.Controls.Add(btnAprovar);
            panelAcoes.Controls.Add(btnIgnorar);
            panelAcoes.Resize += (_, _) =>
            {
                btnIgnorar.Location = new Point(panelAcoes.Width - btnIgnorar.Width - 16, (panelAcoes.Height - btnIgnorar.Height) / 2);
                btnAprovar.Location = new Point(btnIgnorar.Left - btnAprovar.Width - 8, (panelAcoes.Height - btnAprovar.Height) / 2);
            };

            var separadorInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            dgvOrcamentos = new OptimizedDataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ScrollBars = ScrollBars.Vertical
            };
            Theme.EstilizarGrid(dgvOrcamentos, "Form1_Orcamentos");

            dgvOrcamentos.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colOrcamento", HeaderText = "ORÇAMENTO", DataPropertyName = "Orcamento", Width = 90, MinimumWidth = 70 },
                new DataGridViewTextBoxColumn { Name = "colNome", HeaderText = "CLIENTE", DataPropertyName = "Nome", Width = 280, MinimumWidth = 100 }
            );

            panelOrcamentos.Controls.Add(dgvOrcamentos);
            panelOrcamentos.Controls.Add(separadorInferior);
            panelOrcamentos.Controls.Add(panelAcoes);
            panelOrcamentos.Controls.Add(separador);
            panelOrcamentos.Controls.Add(panelHeader);
            panelOrcamentos.ResumeLayout(false);

            // ================================================================
            // PANEL PEDIDOS (coluna direita, altura total)
            // ================================================================
            panelPedidos = new Panel
            {
                Location = new Point(LeftColumnWidth + ColumnGap, 0),
                Size = new Size(100, 100), // recalculado por AtualizarLayoutCentralizado
                BackColor = Theme.BgPanel,
                Padding = new Padding(1)
            };
            panelPedidos.SuspendLayout();
            panelPedidos.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelPedidos.Width - 1, panelPedidos.Height - 1);
            };

            var headerPedidos = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.BgCard,
                Padding = new Padding(16, 0, 16, 0)
            };

            lblTituloPedidos = new Label
            {
                Text = "Controle de Pedidos",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 14)
            };

            lblContagemPedidos = new Label
            {
                Text = "0 registros",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = true
            };

            headerPedidos.Controls.Add(lblTituloPedidos);
            headerPedidos.Controls.Add(lblContagemPedidos);
            headerPedidos.Resize += (_, _) =>
            {
                lblTituloPedidos.Location = new Point(16, (headerPedidos.Height - lblTituloPedidos.Height) / 2);
                lblContagemPedidos.Location = new Point(
                    headerPedidos.Width - lblContagemPedidos.Width - 16,
                    (headerPedidos.Height - lblContagemPedidos.Height) / 2);
            };
            lblContagemPedidos.TextChanged += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    lblContagemPedidos.Location = new Point(
                        headerPedidos.Width - lblContagemPedidos.Width - 16,
                        (headerPedidos.Height - lblContagemPedidos.Height) / 2);
                });
            };

            var separadorPedidos = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            dgvPedidos = new OptimizedDataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ScrollBars = ScrollBars.Both
            };
            Theme.EstilizarGrid(dgvPedidos, "Form1_Pedidos");

            dgvPedidos.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                var colName = dgvPedidos.Columns[e.ColumnIndex].Name;
                if (colName is "colPedAprovado" or "colPedSeparacao" or "colPedConferido" or "colPedLiberado")
                {
                    var valor = e.Value?.ToString();
                    Color bg, fg;
                    if (valor == "TRAVADO")
                    {
                        bg = Theme.StatusTravadoBg;
                        fg = Theme.StatusTravadoFg;
                    }
                    else if (valor == "SIM")
                    {
                        bg = Theme.StatusSimBg;
                        fg = Theme.StatusSimFg;
                    }
                    else
                    {
                        bg = Theme.StatusNaoBg;
                        fg = Theme.StatusNaoFg;
                    }
                    e.CellStyle.BackColor = bg;
                    e.CellStyle.ForeColor = fg;
                    e.CellStyle.SelectionBackColor = bg;
                    e.CellStyle.SelectionForeColor = fg;
                    e.CellStyle.Font = Theme.FontStatus;
                }
                else if (colName is "colPedFaturado")
                {
                    var valor = e.Value?.ToString();
                    Color bg, fg;
                    if (valor == "NÃO")
                    {
                        bg = Theme.StatusNaoBg;
                        fg = Theme.StatusNaoFg;
                    }
                    else if (valor == "EM FATURAMENTO")
                    {
                        bg = Theme.StatusEmFaturamentoBg;
                        fg = Theme.StatusEmFaturamentoFg;
                    }
                    else
                    {
                        bg = Theme.StatusSimBg;
                        fg = Theme.StatusSimFg;
                    }
                    e.CellStyle.BackColor = bg;
                    e.CellStyle.ForeColor = fg;
                    e.CellStyle.SelectionBackColor = bg;
                    e.CellStyle.SelectionForeColor = fg;
                    e.CellStyle.Font = Theme.FontStatus;
                }
            };

            var statusCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            dgvPedidos.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colPedOrcamento", HeaderText = "ORÇAMENTO", DataPropertyName = "Orcamento", Width = 100, MinimumWidth = 80 },
                new DataGridViewTextBoxColumn { Name = "colPedCliente", HeaderText = "CLIENTE", DataPropertyName = "Cliente", Width = 200, MinimumWidth = 120 },
                new DataGridViewTextBoxColumn { Name = "colPedFantasia", HeaderText = "NOME FANTASIA", DataPropertyName = "NomeFantasia", Width = 180, MinimumWidth = 100 },
                new DataGridViewTextBoxColumn { Name = "colPedRota", HeaderText = "ROTA", DataPropertyName = "Rota", Width = 120, MinimumWidth = 80 },
                new DataGridViewTextBoxColumn { Name = "colPedAprovado", HeaderText = "APROVADO", DataPropertyName = "AprovadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedSeparacao", HeaderText = "EM SEPARAÇÃO", DataPropertyName = "EmSeparacaoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedConferido", HeaderText = "CONFERIDO", DataPropertyName = "ConferidoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedLiberado", HeaderText = "LIBERADO P/ FAT.", DataPropertyName = "LiberadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedFaturado", HeaderText = "FATURADO", DataPropertyName = "FaturadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle }
            );

            foreach (var colName in new[] { "colPedAprovado", "colPedSeparacao", "colPedConferido", "colPedLiberado", "colPedFaturado" })
                dgvPedidos.Columns[colName].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var panelAcoesPedidos = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                BackColor = Theme.BgCard
            };
            panelAcoesPedidos.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAcoesPedidos.Width, 0);
            };

            btnAlterarRota = new Button
            {
                Text = "Alterar Rota",
                Size = new Size(110, 30)
            };
            Theme.EstilizarBotao(btnAlterarRota);
            btnAlterarRota.Click += BtnAlterarRota_Click;

            btnExcluirPedido = new Button
            {
                Text = "Excluir Pedido",
                Size = new Size(120, 30)
            };
            Theme.EstilizarBotao(btnExcluirPedido);
            btnExcluirPedido.ForeColor = Theme.Error;
            btnExcluirPedido.Click += BtnExcluirPedido_Click;

            btnTravar = new Button
            {
                Text = "\uD83D\uDD12 Travar",
                Size = new Size(100, 30)
            };
            Theme.EstilizarBotao(btnTravar);
            btnTravar.ForeColor = Theme.Warning;
            btnTravar.Click += BtnTravar_Click;

            btnDestravar = new Button
            {
                Text = "\uD83D\uDD13 Destravar",
                Size = new Size(110, 30)
            };
            Theme.EstilizarBotao(btnDestravar);
            btnDestravar.ForeColor = Theme.Success;
            btnDestravar.Click += BtnDestravar_Click;

            btnAssumir = new Button
            {
                Text = "Assumir",
                Size = new Size(90, 30)
            };
            Theme.EstilizarBotao(btnAssumir);
            btnAssumir.ForeColor = Theme.StatusEmFaturamentoFg;
            btnAssumir.Click += BtnAssumir_Click;

            btnLargar = new Button
            {
                Text = "Largar",
                Size = new Size(80, 30)
            };
            Theme.EstilizarBotao(btnLargar);
            btnLargar.ForeColor = Theme.Warning;
            btnLargar.Click += BtnLargar_Click;

            btnRetornarStatus = new Button
            {
                Text = "\u25C0 Retornar",
                Size = new Size(110, 30)
            };
            Theme.EstilizarBotao(btnRetornarStatus);
            btnRetornarStatus.Click += BtnRetornarStatus_Click;

            btnAvancarStatus = new Button
            {
                Text = "Avançar \u25B6",
                Size = new Size(110, 30)
            };
            Theme.EstilizarBotao(btnAvancarStatus, primario: true);
            btnAvancarStatus.Click += BtnAvancarStatus_Click;

            panelAcoesPedidos.Controls.Add(btnAlterarRota);
            panelAcoesPedidos.Controls.Add(btnExcluirPedido);
            panelAcoesPedidos.Controls.Add(btnTravar);
            panelAcoesPedidos.Controls.Add(btnDestravar);
            panelAcoesPedidos.Controls.Add(btnAssumir);
            panelAcoesPedidos.Controls.Add(btnLargar);
            panelAcoesPedidos.Controls.Add(btnRetornarStatus);
            panelAcoesPedidos.Controls.Add(btnAvancarStatus);

            panelAcoesPedidos.Resize += (_, _) =>
            {
                int cy = (panelAcoesPedidos.Height - 30) / 2;
                btnAlterarRota.Location = new Point(16, cy);
                btnExcluirPedido.Location = new Point(btnAlterarRota.Right + 8, cy);
                btnTravar.Location = new Point(btnExcluirPedido.Right + 8, cy);
                btnDestravar.Location = new Point(btnTravar.Right + 8, cy);
                btnAssumir.Location = new Point(btnDestravar.Right + 8, cy);
                btnLargar.Location = new Point(btnAssumir.Right + 8, cy);
                btnAvancarStatus.Location = new Point(panelAcoesPedidos.Width - btnAvancarStatus.Width - 16, cy);
                btnRetornarStatus.Location = new Point(btnAvancarStatus.Left - btnRetornarStatus.Width - 8, cy);
            };

            var separadorPedidosInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            panelPedidos.Controls.Add(dgvPedidos);
            panelPedidos.Controls.Add(separadorPedidosInferior);
            panelPedidos.Controls.Add(panelAcoesPedidos);
            panelPedidos.Controls.Add(separadorPedidos);
            panelPedidos.Controls.Add(headerPedidos);
            panelPedidos.ResumeLayout(false);

            // ================================================================
            // MONTAR CONTAINER
            // ================================================================
            panelContainer.Controls.Add(panelDetalhes);
            panelContainer.Controls.Add(btnRefresh);
            panelContainer.Controls.Add(panelOrcamentos);
            panelContainer.Controls.Add(panelPedidos);

            // Evento de seleção nos grids
            dgvOrcamentos.SelectionChanged += DgvSelectionChanged;
            dgvPedidos.SelectionChanged += DgvSelectionChanged;

            // ================================================================
            // MONTAR FORM
            // ================================================================
            Controls.Add(panelContainer);
            Controls.Add(statusBarPanel);
            Controls.Add(menuStrip);

            Resize += (_, _) => AtualizarLayoutCentralizado();

            ResumeLayout(false);
            PerformLayout();
            PosicionarElementosStatusBar();
            AtualizarLayoutCentralizado();
        }

        private void AtualizarLayoutCentralizado()
        {
            int areaTop = menuStrip.Bottom;
            int areaBottom = statusBarPanel.Top;
            int areaHeight = areaBottom - areaTop;

            int containerHeight = areaHeight - 32;
            if (containerHeight < 200) containerHeight = 200;

            // Largura dinamica: usa o espaco disponivel, limitado ao maximo
            int availableWidth = ClientSize.Width - ContainerPadding;
            int containerWidth = Math.Clamp(availableWidth, LeftColumnWidth + ColumnGap + 300, MaxContainerWidth);

            // Centralizar se sobrar espaco, senao colar a esquerda
            int containerX = Math.Max(16, (ClientSize.Width - containerWidth) / 2);

            panelContainer.Location = new Point(containerX, areaTop + 16);
            panelContainer.Size = new Size(containerWidth, containerHeight);

            // Coluna esquerda (fixa)
            int detalhesHeight = containerHeight - OrcamentosHeight - RefreshButtonHeight - InnerGap * 2;
            if (detalhesHeight < 100) detalhesHeight = 100;

            panelDetalhes.Location = new Point(0, 0);
            panelDetalhes.Size = new Size(LeftColumnWidth, detalhesHeight);

            btnRefresh.Location = new Point(0, detalhesHeight + InnerGap);
            btnRefresh.Size = new Size(LeftColumnWidth, RefreshButtonHeight);

            panelOrcamentos.Location = new Point(0, detalhesHeight + InnerGap + RefreshButtonHeight + InnerGap);
            panelOrcamentos.Size = new Size(LeftColumnWidth, OrcamentosHeight);

            // Coluna direita (dinamica - ocupa o resto)
            int rightColumnWidth = containerWidth - LeftColumnWidth - ColumnGap;
            panelPedidos.Location = new Point(LeftColumnWidth + ColumnGap, 0);
            panelPedidos.Size = new Size(rightColumnWidth, containerHeight);
        }

        private void PosicionarElementosStatusBar()
        {
            int leftMargin = 16;
            int indicadorY = (statusBarPanel.Height - panelIndicador.Height) / 2;
            panelIndicador.Location = new Point(leftMargin, indicadorY);

            int lblY = (statusBarPanel.Height - lblStatusConexao.Height) / 2;
            lblStatusConexao.Location = new Point(panelIndicador.Right + 8, lblY);

            int rightMargin = 16;
            int btnY = (statusBarPanel.Height - btnConfig.Height) / 2;
            btnConfig.Location = new Point(statusBarPanel.Width - btnConfig.Width - rightMargin, btnY);
        }

        #endregion
    }
}
