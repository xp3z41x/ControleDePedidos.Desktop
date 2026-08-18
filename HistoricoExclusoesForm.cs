using System.ComponentModel;

namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Tela de consulta ao historico de exclusoes (tabela append-only).
    /// Oferece filtro textual universal e acao de limpeza de registros antigos (> 3 meses).
    /// </summary>
    public class HistoricoExclusoesForm : Form
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private OptimizedDataGridView dgvHistorico = null!;
        private TextBox txtPesquisa = null!;
        private Button btnLimparAntigos = null!;
        private Label lblContagem = null!;

        // Lista master (completa, do banco)
        private List<HistoricoExclusaoItem> _itens = [];

        // Lista bindada ao grid (filtrada, com sort preservado)
        private SortableBindingList<HistoricoExclusaoItem> _bind = [];

        public HistoricoExclusoesForm()
        {
            InitializeUI();
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);
            _ = CarregarAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Histórico de Exclusões";
            ClientSize = new Size(1100, 640);
            MinimumSize = new Size(800, 450);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            DoubleBuffered = true;

            var panelCard = new Panel
            {
                Location = new Point(16, 16),
                Size = new Size(ClientSize.Width - 32, ClientSize.Height - 32),
                BackColor = Theme.BgPanel,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            panelCard.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelCard.Width - 1, panelCard.Height - 1);
            };

            // ================================================================
            // HEADER
            // ================================================================
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.BgCard
            };

            var lblTitulo = new Label
            {
                Text = "Histórico de Exclusões",
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

            panelHeader.Controls.Add(lblTitulo);
            panelHeader.Controls.Add(lblContagem);
            panelHeader.Resize += (_, _) =>
            {
                lblTitulo.Location = new Point(16, (panelHeader.Height - lblTitulo.Height) / 2);
                lblContagem.Location = new Point(
                    panelHeader.Width - lblContagem.Width - 16,
                    (panelHeader.Height - lblContagem.Height) / 2);
            };
            lblContagem.TextChanged += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    lblContagem.Location = new Point(
                        panelHeader.Width - lblContagem.Width - 16,
                        (panelHeader.Height - lblContagem.Height) / 2);
                });
            };

            var separador = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            // ================================================================
            // BARRA DE FILTROS
            // ================================================================
            var panelFiltros = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Theme.BgCard,
                Padding = new Padding(12, 10, 12, 10)
            };
            panelFiltros.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, panelFiltros.Height - 1, panelFiltros.Width, panelFiltros.Height - 1);
            };

            txtPesquisa = new TextBox
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "Buscar por orçamento, cliente, NFe, usuário..."
            };
            Theme.EstilizarTextBox(txtPesquisa);
            txtPesquisa.TextChanged += (_, _) => AplicarFiltros();

            panelFiltros.Controls.Add(txtPesquisa);

            // ================================================================
            // BARRA DE ACOES (RODAPE)
            // ================================================================
            var panelAcoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Theme.BgCard,
                Padding = new Padding(12, 10, 12, 10)
            };
            panelAcoes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAcoes.Width, 0);
            };

            btnLimparAntigos = new Button
            {
                Text = "Excluir histórico com mais de 3 meses",
                Size = new Size(260, 30)
            };
            Theme.EstilizarBotao(btnLimparAntigos);
            btnLimparAntigos.ForeColor = Theme.Error;
            btnLimparAntigos.Click += BtnLimparAntigos_Click;

            panelAcoes.Controls.Add(btnLimparAntigos);
            panelAcoes.Resize += (_, _) =>
            {
                btnLimparAntigos.Location = new Point(
                    panelAcoes.Width - btnLimparAntigos.Width - 16,
                    (panelAcoes.Height - btnLimparAntigos.Height) / 2);
            };

            var separadorInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            // ================================================================
            // GRID
            // ================================================================
            dgvHistorico = new OptimizedDataGridView
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
            Theme.EstilizarGrid(dgvHistorico, "HistoricoExclusoes_Grid");

            dgvHistorico.Columns.AddRange(
                new DataGridViewTextBoxColumn
                {
                    Name = "colData",
                    HeaderText = "DATA",
                    DataPropertyName = "DataExclusaoTexto",
                    Width = 120,
                    MinimumWidth = 90
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colTipo",
                    HeaderText = "TIPO",
                    DataPropertyName = "TipoExclusao",
                    Width = 70,
                    MinimumWidth = 55
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colOrcamento",
                    HeaderText = "ORÇAMENTO",
                    DataPropertyName = "Orcamento",
                    Width = 95,
                    MinimumWidth = 75
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colStatus",
                    HeaderText = "STATUS",
                    DataPropertyName = "Status",
                    Width = 130,
                    MinimumWidth = 90
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colCliente",
                    HeaderText = "CLIENTE",
                    DataPropertyName = "Cliente",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = 140
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colNomeFantasia",
                    HeaderText = "FANTASIA",
                    DataPropertyName = "NomeFantasia",
                    Width = 200,
                    MinimumWidth = 110
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colRota",
                    HeaderText = "ROTA",
                    DataPropertyName = "Rota",
                    Width = 100,
                    MinimumWidth = 70
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colVendedor",
                    HeaderText = "VENDEDOR",
                    DataPropertyName = "Vendedor",
                    Width = 120,
                    MinimumWidth = 85
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colPedidoVenda",
                    HeaderText = "PED. VENDA",
                    DataPropertyName = "PedidoVendaTexto",
                    Width = 95,
                    MinimumWidth = 75
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colNotaFiscal",
                    HeaderText = "NFE",
                    DataPropertyName = "NotaFiscal",
                    Width = 90,
                    MinimumWidth = 70
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colUsuario",
                    HeaderText = "USUÁRIO",
                    DataPropertyName = "UsuarioWindows",
                    Width = 120,
                    MinimumWidth = 85
                }
            );

            panelCard.Controls.Add(dgvHistorico);
            panelCard.Controls.Add(separadorInferior);
            panelCard.Controls.Add(panelAcoes);
            panelCard.Controls.Add(panelFiltros);
            panelCard.Controls.Add(separador);
            panelCard.Controls.Add(panelHeader);

            Controls.Add(panelCard);

            ResumeLayout(false);
            PerformLayout();
        }

        private async Task CarregarAsync()
        {
            var (sortCol, sortDir) = DataGridViewSortHelper.SaveSort(dgvHistorico);
            try
            {
                var lista = await _db.ListarHistoricoExclusoesAsync();
                _itens = lista;
                // Copia independente para o bind: BindingList<T>(IList<T>) wrapea a MESMA
                // referencia passada. Sem o ToList(), AtualizarInPlace removeria itens de
                // _itens tambem — quebrando o filtro ao apagar o texto (o master ja estaria reduzido).
                _bind = new SortableBindingList<HistoricoExclusaoItem>(lista.ToList());

                dgvHistorico.SuspendLayout();
                dgvHistorico.SkipAutoAdjust = true;
                try
                {
                    dgvHistorico.DataSource = _bind;
                    DataGridViewSortHelper.RestoreSort(dgvHistorico, sortCol, sortDir);
                    AtualizarContagem();
                }
                finally
                {
                    dgvHistorico.SkipAutoAdjust = false;
                    dgvHistorico.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao carregar histórico de exclusões", ex);
                MessageBox.Show(this, $"Erro ao carregar histórico: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AplicarFiltros()
        {
            var termo = txtPesquisa.Text.Trim();
            IEnumerable<HistoricoExclusaoItem> filtrados = _itens;

            if (!string.IsNullOrEmpty(termo))
            {
                filtrados = _itens.Where(h =>
                    h.Orcamento.ToString().Contains(termo, StringComparison.OrdinalIgnoreCase)
                 || h.PedidoVenda.ToString().Contains(termo, StringComparison.OrdinalIgnoreCase)
                 || h.Cliente.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                 || h.NomeFantasia.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                 || h.Rota.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                 || h.Vendedor.Contains(termo, StringComparison.CurrentCultureIgnoreCase)
                 || h.NotaFiscal.Contains(termo, StringComparison.OrdinalIgnoreCase)
                 || h.Status.Contains(termo, StringComparison.OrdinalIgnoreCase)
                 || h.TipoExclusao.Contains(termo, StringComparison.OrdinalIgnoreCase)
                 || h.UsuarioWindows.Contains(termo, StringComparison.CurrentCultureIgnoreCase));
            }

            var lista = filtrados.ToList();
            _bind.AtualizarInPlace(lista, h => h.Id, (src, dst) => dst.CopiarDe(src));
            AtualizarContagem();
        }

        private async void BtnLimparAntigos_Click(object? sender, EventArgs e)
        {
            var confirm = MessageBox.Show(this,
                "Excluir definitivamente todos os registros de exclusão com mais de 3 meses?\n\nEsta ação é IRREVERSÍVEL.",
                "Confirmar Limpeza",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                btnLimparAntigos.Enabled = false;
                var corte = DateTime.Now.AddMonths(-3);
                var removidos = await _db.LimparHistoricoExclusoesAntigoAsync(corte);

                MessageBox.Show(this,
                    $"{removidos} registro(s) removido(s) do histórico.",
                    "Limpeza Concluída",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                await CarregarAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao limpar histórico antigo", ex);
                MessageBox.Show(this, $"Erro ao limpar histórico: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnLimparAntigos.Enabled = true;
            }
        }

        private void AtualizarContagem()
        {
            var total = _bind.Count;
            lblContagem.Text = $"{total} registro{(total != 1 ? "s" : "")}";
        }
    }

    /// <summary>
    /// POCO mutavel (para permitir AtualizarInPlace) que representa uma linha
    /// da tabela historico_exclusoes. Propriedades de display (*Texto) sao
    /// formatadas para o grid.
    /// </summary>
    public class HistoricoExclusaoItem
    {
        public int Id { get; set; }
        public int Orcamento { get; set; }
        public string Cliente { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
        public string Rota { get; set; } = "";
        public string Vendedor { get; set; } = "";
        public int PedidoVenda { get; set; }
        public string NotaFiscal { get; set; } = "";
        public string Status { get; set; } = "";
        public bool Aprovado { get; set; }
        public bool EmSeparacao { get; set; }
        public bool Conferido { get; set; }
        public bool Liberado { get; set; }
        public bool Travado { get; set; }
        public bool EmFaturamento { get; set; }
        public string TipoExclusao { get; set; } = "";
        public string UsuarioWindows { get; set; } = "";
        public DateTime DataExclusao { get; set; }

        // Propriedades de display (usadas por DataPropertyName)
        public string DataExclusaoTexto => DataExclusao.ToString("dd/MM/yyyy HH:mm");
        public string PedidoVendaTexto => PedidoVenda > 0 ? PedidoVenda.ToString() : "";

        public void CopiarDe(HistoricoExclusaoItem outro)
        {
            Orcamento = outro.Orcamento;
            Cliente = outro.Cliente;
            NomeFantasia = outro.NomeFantasia;
            Rota = outro.Rota;
            Vendedor = outro.Vendedor;
            PedidoVenda = outro.PedidoVenda;
            NotaFiscal = outro.NotaFiscal;
            Status = outro.Status;
            Aprovado = outro.Aprovado;
            EmSeparacao = outro.EmSeparacao;
            Conferido = outro.Conferido;
            Liberado = outro.Liberado;
            Travado = outro.Travado;
            EmFaturamento = outro.EmFaturamento;
            TipoExclusao = outro.TipoExclusao;
            UsuarioWindows = outro.UsuarioWindows;
            DataExclusao = outro.DataExclusao;
        }
    }
}
