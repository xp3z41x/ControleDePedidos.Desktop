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
        private Panel panelFiltrosPedidos = null!;
        private ToolStrip toolStripFiltros = null!;
        private ToolStripDropDownButton btnFiltroVendedor = null!;
        private ToolStripDropDownButton btnFiltroRota = null!;
        private ToolStripDropDownButton btnFiltroEtapa = null!;
        private ToolStripButton btnLimparFiltros = null!;
        private OptimizedDataGridView dgvPedidos = null!;
        private Button btnAlterarRota = null!;
        private Button btnExcluirPedido = null!;
        private Button btnAvancarStatus = null!;
        private Button btnRetornarStatus = null!;
        private Button btnImprimirSeparacao = null!;
        private Button btnCopiaPedido = null!;
        private Button btnTravar = null!;
        private Button btnDestravar = null!;
        private Button btnAssumir = null!;
        private Button btnLargar = null!;
        private ToolStripButton btnRefresh = null!;
        private System.Windows.Forms.Timer timerAutoRefresh = null!;
        private System.Windows.Forms.Timer timerCoalescer = null!;
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

        // Layout constants (valores lógicos a 96 DPI — escalados por Dpi() no runtime)
        private const int MaxContainerWidth = 1870;
        private const int MinContainerWidth = 1000;  // pendentes (380) + gap + detalhes (592)
        private const int ContainerPadding = 32;
        private const int InnerGap = 8;

        // Faixa superior: card de pendentes à esquerda + card de detalhes à direita.
        // A altura é ancorada num número INTEIRO de linhas do grid de pendentes (evita
        // meia-linha cortada); a sobra volta para o grid de pedidos.
        private const int PendentesCromoH = 80;      // 2 padding + 36 header + 1 + 1 + 40 ações
        private const int GridHeaderH     = 36;      // Theme: ColumnHeadersHeight
        private const int GridRowH        = 32;      // Theme: RowTemplate.Height
        private const float TopoFracao      = 0.32f;
        private const int TopoMinLinhas     = 3;
        private const int TopoMaxLinhas     = 6;
        private const float PendentesFracao = 0.28f;
        private const int PendentesMinW     = 380;   // mínimo do header (título+contagem+botão)
        private const int PendentesMaxW     = 460;   // acima disso sobra vazio à direita do grid
        private const int DetalhesRowH      = 46;    // linhas 1 e 2 do card de detalhes (fixas)
        private const int DetalhesRow3Min   = 56;    // observação (elástica, absorve o resto)

        // O AutoScaleMode.Font não escala as métricas aplicadas em runtime pelo layout
        // manual; este helper as converte para o DPI corrente do form (PerMonitorV2).
        private int Dpi(int v) => LogicalToDeviceUnits(v);

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
            // Mínimo que ainda comporta a faixa superior (pendentes + detalhes) sem
            // estourar o container: ClientSize precisa ≥ Dpi(MinContainerWidth) + padding.
            MinimumSize = new Size(1060, 640);
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

            var menuHistoricoExclusoes = new ToolStripMenuItem("Histórico de Exclusões")
            {
                ForeColor = Theme.TextPrimary
            };
            menuHistoricoExclusoes.Click += MenuHistoricoExclusoes_Click;
            menuPedidos.DropDownItems.Add(menuHistoricoExclusoes);

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

            var menuImprimirPorOrcamento = new ToolStripMenuItem("Imprimir por Orçamento...")
            {
                ForeColor = Theme.TextPrimary,
                ShortcutKeys = Keys.Control | Keys.P
            };
            menuImprimirPorOrcamento.Click += MenuImprimirPorOrcamento_Click;
            menuRelatorios.DropDownItems.Add(menuImprimirPorOrcamento);

            var menuObservacao = new ToolStripMenuItem("OBSERVAÇÃO")
            {
                ForeColor = Theme.TextPrimary
            };
            var menuCorrigirObservacao = new ToolStripMenuItem("Corrigir Observação")
            {
                ForeColor = Theme.TextPrimary
            };
            menuCorrigirObservacao.Click += MenuCorrigirObservacao_Click;
            menuObservacao.DropDownItems.Add(menuCorrigirObservacao);

            menuStrip.Items.Add(menuRotas);
            menuStrip.Items.Add(menuPedidos);
            menuStrip.Items.Add(menuRelatorios);
            menuStrip.Items.Add(menuObservacao);
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
                Text = "Configura\u00e7\u00f5es",
                Size = new Size(110, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgStatusBar,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontSmall,
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
            // PANEL DETALHES (faixa horizontal acima do grid de pedidos)
            // ================================================================
            panelDetalhes = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(800, Dpi(240)), // recalculado por AtualizarLayoutCentralizado
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
                BackColor = Theme.BgPanel
            };

            int padCel = Dpi(14);
            var sepsVerticaisDetalhes = new List<Panel>();
            var sepHorizontalDetalhes = new Panel { Size = new Size(1, 1), BackColor = Theme.Border };
            var sepHorizontalDetalhes2 = new Panel { Size = new Size(1, 1), BackColor = Theme.Border };
            containerDetalhes.Controls.Add(sepHorizontalDetalhes);
            containerDetalhes.Controls.Add(sepHorizontalDetalhes2);

            (Label tit, Label val) CriarCelulaH(string titulo)
            {
                var tit = new Label
                {
                    Text = titulo,
                    Font = Theme.FontDetalheLabel,
                    ForeColor = Theme.TextMuted,
                    AutoSize = true
                };
                var val = new Label
                {
                    Text = "—",
                    Font = Theme.FontDetalheValor,
                    ForeColor = Theme.TextPrimary,
                    AutoEllipsis = true
                };
                containerDetalhes.Controls.Add(tit);
                containerDetalhes.Controls.Add(val);
                return (tit, val);
            }

            var celValorTotal = CriarCelulaH("VALOR TOTAL");
            lblDetValorTotal = celValorTotal.val;
            lblDetValorTotal.Font = Theme.FontDetalheDestaque;
            lblDetValorTotal.ForeColor = Theme.Accent;

            var celCidade = CriarCelulaH("CIDADE");
            lblDetCidade = celCidade.val;
            var celEstado = CriarCelulaH("UF");
            lblDetEstado = celEstado.val;
            var celVendedor = CriarCelulaH("VENDEDOR");
            lblDetVendedor = celVendedor.val;
            var celTexto01 = CriarCelulaH("FORMA DE PAGAMENTO");
            lblDetTexto01 = celTexto01.val;
            var celTexto02 = CriarCelulaH("NÚMERO DO PEDIDO");
            lblDetTexto02 = celTexto02.val;
            var celTexto03 = CriarCelulaH("VOLUMES");
            lblDetTexto03 = celTexto03.val;
            var celTexto04 = CriarCelulaH("PESO");
            lblDetTexto04 = celTexto04.val;
            var celObservacao = CriarCelulaH("OBSERVAÇÃO");
            lblDetObservacao = celObservacao.val;
            lblDetObservacao.AutoEllipsis = false;
            lblDetObservacao.AutoSize = false; // wrap com clip nas 2 linhas da célula

            // Três linhas de células: (célula, % da largura, mínimo lógico).
            // pct 0 = célula elástica, que absorve o espaço restante da linha.
            var linhaDet1 = new[]
            {
                (cel: celValorTotal, pct: 0.17f, min: 160),
                (cel: celCidade, pct: 0.22f, min: 150),
                (cel: celEstado, pct: 0.07f, min: 60),
                (cel: celVendedor, pct: 0f, min: 200)
            };
            var linhaDet2 = new[]
            {
                (cel: celTexto01, pct: 0f, min: 220),      // FORMA DE PAGAMENTO (elástica)
                (cel: celTexto02, pct: 0.22f, min: 160),
                (cel: celTexto03, pct: 0.13f, min: 105),
                (cel: celTexto04, pct: 0.13f, min: 105)
            };
            var linhaDet3 = new[]
            {
                (cel: celObservacao, pct: 0f, min: 300)    // largura inteira
            };
            for (int i = 0; i < linhaDet1.Length + linhaDet2.Length + linhaDet3.Length - 3; i++)
            {
                var sep = new Panel { Size = new Size(1, 1), BackColor = Theme.Border };
                sepsVerticaisDetalhes.Add(sep);
                containerDetalhes.Controls.Add(sep);
            }

            void LayoutDetalhes()
            {
                int larguraTotal = containerDetalhes.ClientSize.Width;
                int alturaTotal = containerDetalhes.ClientSize.Height;
                if (larguraTotal <= 0 || alturaTotal <= 0) return;
                int sepIdx = 0;

                int PosicionarLinha(((Label tit, Label val) cel, float pct, int min)[] linha, int y, int rowH)
                {
                    // A primeira célula com pct 0 é a elástica (absorve o resto da linha);
                    // sem nenhuma marcada, a última assume o papel.
                    var w = new int[linha.Length];
                    int soma = 0;
                    int idxElastico = -1;
                    for (int i = 0; i < linha.Length; i++)
                    {
                        if (linha[i].pct <= 0f && idxElastico < 0) { idxElastico = i; continue; }
                        w[i] = Math.Max(Dpi(linha[i].min), (int)(larguraTotal * linha[i].pct));
                        soma += w[i];
                    }
                    if (idxElastico < 0) { idxElastico = linha.Length - 1; soma -= w[idxElastico]; }
                    w[idxElastico] = Math.Max(larguraTotal - soma, Dpi(linha[idxElastico].min));

                    int x = 0;
                    for (int i = 0; i < linha.Length; i++)
                    {
                        var (tit, val) = linha[i].cel;
                        tit.Location = new Point(x + padCel, y + Dpi(4));
                        val.Location = new Point(x + padCel, y + Dpi(19));
                        val.Size = new Size(w[i] - 2 * padCel, rowH - Dpi(21));
                        x += w[i];
                        if (i < linha.Length - 1)
                        {
                            var sep = sepsVerticaisDetalhes[sepIdx++];
                            sep.Location = new Point(x - 1, y + Dpi(8));
                            sep.Size = new Size(1, rowH - Dpi(16));
                        }
                    }
                    return y + rowH;
                }

                int rowH = Dpi(DetalhesRowH);

                int yLinha2 = PosicionarLinha(linhaDet1, 0, rowH);
                sepHorizontalDetalhes.Location = new Point(0, yLinha2);
                sepHorizontalDetalhes.Size = new Size(larguraTotal, 1);

                int yLinha3 = PosicionarLinha(linhaDet2, yLinha2 + 1, rowH);
                sepHorizontalDetalhes2.Location = new Point(0, yLinha3);
                sepHorizontalDetalhes2.Size = new Size(larguraTotal, 1);

                // Observação absorve a altura restante — o card se adapta a qualquer
                // altura da faixa superior.
                int row3H = Math.Max(alturaTotal - (yLinha3 + 1), Dpi(DetalhesRow3Min));
                PosicionarLinha(linhaDet3, yLinha3 + 1, row3H);
            }
            containerDetalhes.Resize += (_, _) => LayoutDetalhes();
            LayoutDetalhes();

            panelDetalhes.Controls.Add(containerDetalhes);
            panelDetalhes.Controls.Add(separadorDetalhes);
            panelDetalhes.Controls.Add(headerDetalhes);

            // Auto-refresh: o intervalo real é definido por AgendarProximoRefresh()
            // (base 15s com jitter, backoff por foco, pausa com sessão RDP inativa).
            timerAutoRefresh = new System.Windows.Forms.Timer(components)
            {
                Interval = 15000
            };
            timerAutoRefresh.Tick += TimerAutoRefresh_Tick;

            // Coalescer das notificações LISTEN/NOTIFY: uma rajada de avisos (vários
            // operadores agindo junto) vira um único refresh, meio segundo depois.
            timerCoalescer = new System.Windows.Forms.Timer(components) { Interval = 500 };
            timerCoalescer.Tick += TimerCoalescer_Tick;

            // ================================================================
            // PANEL ORCAMENTOS (hospedado na janela modal "Pendentes")
            // ================================================================
            panelOrcamentos = new Panel
            {
                Size = new Size(420, 280),
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
                Height = 36,   // alinhado com o headerDetalhes, que fica ao lado na faixa
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
                Height = 40,   // compacto: a faixa superior é curta, cada pixel vira linha
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
            // PANEL PEDIDOS (largura total, abaixo da faixa de detalhes)
            // ================================================================
            panelPedidos = new Panel
            {
                Location = new Point(0, 0),
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
                    else if (valor == "SIM" || valor == "OK")
                    {
                        bg = Theme.StatusSimBg;
                        fg = Theme.StatusSimFg;
                    }
                    else if (valor == "MANUAL")
                    {
                        bg = Theme.StatusManualBg;
                        fg = Theme.StatusManualFg;
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
                new DataGridViewTextBoxColumn { Name = "colPedOrcamento", HeaderText = "ORÇ.", DataPropertyName = "Orcamento", Width = 100, MinimumWidth = 80 },
                new DataGridViewTextBoxColumn { Name = "colPedCliente", HeaderText = "CLIENTE", DataPropertyName = "Cliente", Width = 200, MinimumWidth = 120 },
                new DataGridViewTextBoxColumn { Name = "colPedFantasia", HeaderText = "NOME FANTASIA", DataPropertyName = "NomeFantasia", Width = 180, MinimumWidth = 100 },
                new DataGridViewTextBoxColumn { Name = "colPedRota", HeaderText = "ROTA", DataPropertyName = "Rota", Width = 120, MinimumWidth = 80 },
                new DataGridViewTextBoxColumn { Name = "colPedAprovado", HeaderText = "APROVADO", DataPropertyName = "AprovadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedSeparacao", HeaderText = "SEPARANDO", DataPropertyName = "EmSeparacaoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedConferido", HeaderText = "CONFERIDO", DataPropertyName = "ConferidoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedLiberado", HeaderText = "LIBERADO", DataPropertyName = "LiberadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colPedFaturado", HeaderText = "FATURADO", DataPropertyName = "FaturadoTexto", Width = 110, MinimumWidth = 80, DefaultCellStyle = statusCellStyle }
            );

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

            btnImprimirSeparacao = new Button
            {
                Text = "\uD83D\uDDA8 Imprimir",
                Size = new Size(110, 30)
            };
            Theme.EstilizarBotao(btnImprimirSeparacao);
            btnImprimirSeparacao.Click += BtnImprimirSeparacao_Click;

            btnCopiaPedido = new Button
            {
                Text = "\uD83D\uDCC4 C\u00F3pia de Pedido",
                Size = new Size(150, 30)
            };
            Theme.EstilizarBotao(btnCopiaPedido);
            btnCopiaPedido.Click += BtnCopiaPedido_Click;

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
            panelAcoesPedidos.Controls.Add(btnCopiaPedido);
            panelAcoesPedidos.Controls.Add(btnImprimirSeparacao);
            panelAcoesPedidos.Controls.Add(btnRetornarStatus);
            panelAcoesPedidos.Controls.Add(btnAvancarStatus);

            // Em telas estreitas os dois grupos (esquerda: ações; direita: status) não
            // cabem lado a lado — a barra vira duas linhas, com o grupo de status embaixo.
            void ReposicionarBotoesPedidos()
            {
                int larguraEsquerda = 16 + btnAlterarRota.Width + 8 + btnExcluirPedido.Width + 8
                    + btnTravar.Width + 8 + btnDestravar.Width + 8 + btnAssumir.Width + 8 + btnLargar.Width;
                int larguraDireita = btnCopiaPedido.Width + 8 + btnImprimirSeparacao.Width + 8 + btnRetornarStatus.Width + 8 + btnAvancarStatus.Width + 16;
                bool duasLinhas = larguraEsquerda + 24 + larguraDireita > panelAcoesPedidos.Width;

                int alturaAlvo = duasLinhas ? 82 : 46;
                if (panelAcoesPedidos.Height != alturaAlvo)
                {
                    // Mudar Height dentro do pass de layout do dock deixa o grid (Dock=Fill)
                    // cobrindo a barra; adiar para depois do pass garante o re-layout correto.
                    if (panelAcoesPedidos.IsHandleCreated)
                        panelAcoesPedidos.BeginInvoke(new Action(() => panelAcoesPedidos.Height = alturaAlvo));
                    else
                        panelAcoesPedidos.Height = alturaAlvo;
                }

                int cyEsq = duasLinhas ? 8 : (panelAcoesPedidos.Height - 30) / 2;
                int cyDir = duasLinhas ? panelAcoesPedidos.Height - 38 : cyEsq;

                btnAlterarRota.Location = new Point(16, cyEsq);
                btnExcluirPedido.Location = new Point(btnAlterarRota.Right + 8, cyEsq);
                btnTravar.Location = new Point(btnExcluirPedido.Right + 8, cyEsq);
                btnDestravar.Location = new Point(btnTravar.Right + 8, cyEsq);
                btnAssumir.Location = new Point(btnDestravar.Right + 8, cyEsq);
                btnLargar.Location = new Point(btnAssumir.Right + 8, cyEsq);
                btnAvancarStatus.Location = new Point(panelAcoesPedidos.Width - btnAvancarStatus.Width - 16, cyDir);
                btnRetornarStatus.Location = new Point(btnAvancarStatus.Left - btnRetornarStatus.Width - 8, cyDir);
                btnImprimirSeparacao.Location = new Point(btnRetornarStatus.Left - btnImprimirSeparacao.Width - 8, cyDir);
                btnCopiaPedido.Location = new Point(btnImprimirSeparacao.Left - btnCopiaPedido.Width - 8, cyDir);
            }
            panelAcoesPedidos.Resize += (_, _) => ReposicionarBotoesPedidos();

            var separadorPedidosInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            // ================================================================
            // BARRA DE FILTROS (Vendedor / Rota / Etapa)
            // ================================================================
            panelFiltrosPedidos = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Theme.BgCard,
                Padding = new Padding(12, 0, 12, 0)
            };

            toolStripFiltros = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                BackColor = Theme.BgCard,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                Renderer = new ThemeToolStripRenderer(),
                ImageScalingSize = new Size(16, 16),
                AutoSize = false,
                Padding = new Padding(0)
            };

            var lblFiltrar = new ToolStripLabel("Filtrar:")
            {
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontSmall,
                Margin = new Padding(4, 0, 8, 0)
            };

            btnFiltroVendedor = new ToolStripDropDownButton("Vendedor")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                AutoToolTip = false,
                Margin = new Padding(0, 4, 4, 4),
                Padding = new Padding(10, 0, 6, 0)
            };
            btnFiltroRota = new ToolStripDropDownButton("Rota")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                AutoToolTip = false,
                Margin = new Padding(0, 4, 4, 4),
                Padding = new Padding(10, 0, 6, 0)
            };
            btnFiltroEtapa = new ToolStripDropDownButton("Etapa")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                AutoToolTip = false,
                Margin = new Padding(0, 4, 4, 4),
                Padding = new Padding(10, 0, 6, 0)
            };

            btnLimparFiltros = new ToolStripButton("Limpar Filtros")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Theme.Warning,
                Font = Theme.FontSmall,
                AutoToolTip = false,
                Visible = false,
                Alignment = ToolStripItemAlignment.Right,
                Margin = new Padding(0, 4, 4, 4),
                Padding = new Padding(8, 0, 8, 0)
            };

            btnRefresh = new ToolStripButton("⟳ Atualizar")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = Theme.FontRegular,
                AutoToolTip = false,
                Alignment = ToolStripItemAlignment.Right,
                Margin = new Padding(0, 4, 4, 4),
                Padding = new Padding(8, 0, 8, 0)
            };
            btnRefresh.Click += BtnRefresh_Click;

            toolStripFiltros.Items.Add(lblFiltrar);
            toolStripFiltros.Items.Add(btnFiltroVendedor);
            toolStripFiltros.Items.Add(btnFiltroRota);
            toolStripFiltros.Items.Add(btnFiltroEtapa);
            // Itens Right empilham a partir da borda direita na ordem da coleção:
            // Atualizar (fixo) fica na extrema direita; Limpar Filtros à esquerda dele.
            toolStripFiltros.Items.Add(btnRefresh);
            toolStripFiltros.Items.Add(btnLimparFiltros);

            // DropDowns com tema e fonte próprios
            foreach (var dd in new[] { btnFiltroVendedor.DropDown, btnFiltroRota.DropDown, btnFiltroEtapa.DropDown })
            {
                dd.BackColor = Theme.BgPanel;
                dd.ForeColor = Theme.TextPrimary;
                dd.Font = Theme.FontRegular;
                dd.Renderer = new ThemeToolStripRenderer();
            }

            panelFiltrosPedidos.Controls.Add(toolStripFiltros);

            var separadorFiltros = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            // Ordem Dock: último .Add em Top fica colado na borda superior.
            // Do fundo do Z-order para o topo: separadorFiltros → panelFiltrosPedidos → separadorPedidos → headerPedidos
            panelPedidos.Controls.Add(dgvPedidos);
            panelPedidos.Controls.Add(separadorPedidosInferior);
            panelPedidos.Controls.Add(panelAcoesPedidos);
            panelPedidos.Controls.Add(separadorFiltros);
            panelPedidos.Controls.Add(panelFiltrosPedidos);
            panelPedidos.Controls.Add(separadorPedidos);
            panelPedidos.Controls.Add(headerPedidos);
            panelPedidos.ResumeLayout(false);

            // ================================================================
            // MONTAR CONTAINER
            // ================================================================
            panelContainer.Controls.Add(panelOrcamentos);
            panelContainer.Controls.Add(panelDetalhes);
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
            // PerMonitorV2: re-executa o layout manual ao mudar de monitor/DPI
            // (Dpi() lê o DeviceDpi corrente a cada chamada)
            DpiChanged += (_, _) => AtualizarLayoutCentralizado();

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

            int containerHeight = areaHeight - Dpi(32);
            if (containerHeight < Dpi(200)) containerHeight = Dpi(200);

            // Largura dinamica: usa o espaco disponivel, limitado ao maximo
            int availableWidth = ClientSize.Width - Dpi(ContainerPadding);
            int containerWidth = Math.Clamp(availableWidth, Dpi(MinContainerWidth), Dpi(MaxContainerWidth));

            // Centralizar se sobrar espaco, senao colar a esquerda
            int containerX = Math.Max(Dpi(16), (ClientSize.Width - containerWidth) / 2);

            panelContainer.Location = new Point(containerX, areaTop + Dpi(16));
            panelContainer.Size = new Size(containerWidth, containerHeight);

            int inner = Dpi(InnerGap);

            // Altura da faixa superior ancorada em nº INTEIRO de linhas do grid de
            // pendentes: sem isso sobraria meia linha cortada no rodapé do card.
            int cromoTopo = Dpi(PendentesCromoH + GridHeaderH);
            int linhaGrid = Dpi(GridRowH);
            int linhasTopo = Math.Clamp(
                ((int)(containerHeight * TopoFracao) - cromoTopo) / linhaGrid,
                TopoMinLinhas, TopoMaxLinhas);
            int alturaTopo = cromoTopo + linhasTopo * linhaGrid;

            // Nunca engolir o grid de pedidos em janelas muito baixas
            int tetoTopo = containerHeight - inner - Dpi(180);
            if (alturaTopo > tetoTopo)
                alturaTopo = Math.Max(cromoTopo + TopoMinLinhas * linhaGrid, tetoTopo);

            // Faixa superior: pendentes à esquerda, detalhes ocupando o resto
            int larguraPendentes = Math.Clamp(
                (int)(containerWidth * PendentesFracao), Dpi(PendentesMinW), Dpi(PendentesMaxW));
            int larguraDetalhes = containerWidth - larguraPendentes - inner;

            panelOrcamentos.Location = new Point(0, 0);
            panelOrcamentos.Size = new Size(larguraPendentes, alturaTopo);

            panelDetalhes.Location = new Point(larguraPendentes + inner, 0);
            panelDetalhes.Size = new Size(larguraDetalhes, alturaTopo);

            // Grid de pedidos: largura total, abaixo da faixa
            panelPedidos.Location = new Point(0, alturaTopo + inner);
            panelPedidos.Size = new Size(containerWidth,
                Math.Max(containerHeight - alturaTopo - inner, Dpi(100)));
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
