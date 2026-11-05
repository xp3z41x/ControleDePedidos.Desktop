using System.ComponentModel;

namespace CONTROLE_DE_PEDIDOS
{
    public class IgnoradosForm : FormEscalavel
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private OptimizedDataGridView dgvIgnorados = null!;
        private Button btnRemover = null!;
        private Label lblContagem = null!;
        private SortableBindingList<OrcamentoIgnorado> _lista = [];
        private bool _alterou;
        private bool _ocupado;

        public IgnoradosForm()
        {
            InitializeUI();
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);
            _ = CarregarAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Orçamentos Ignorados";
            ClientSize = Dpi(750, 550);
            MinimumSize = Dpi(550, 400);
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
                Location = DpiPt(16, 16),
                Size = new Size(ClientSize.Width - Dpi(32), ClientSize.Height - Dpi(32)),
                BackColor = Theme.BgPanel,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            panelCard.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelCard.Width - 1, panelCard.Height - 1);
            };

            // Header
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi(48),
                BackColor = Theme.BgCard
            };

            var lblTitulo = new Label
            {
                Text = "Orçamentos Ignorados",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = DpiPt(16, 14)
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
                lblTitulo.Location = new Point(Dpi(16), (panelHeader.Height - lblTitulo.Height) / 2);
                lblContagem.Location = new Point(
                    panelHeader.Width - lblContagem.Width - Dpi(16),
                    (panelHeader.Height - lblContagem.Height) / 2);
            };
            lblContagem.TextChanged += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    lblContagem.Location = new Point(
                        panelHeader.Width - lblContagem.Width - Dpi(16),
                        (panelHeader.Height - lblContagem.Height) / 2);
                });
            };

            var separador = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            // Barra de acoes
            var panelAcoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Dpi(46),
                BackColor = Theme.BgCard
            };
            panelAcoes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAcoes.Width, 0);
            };

            btnRemover = new Button
            {
                Text = "Restaurar Selecionado",
                Size = Dpi(160, 30)
            };
            Theme.EstilizarBotao(btnRemover, primario: true);
            btnRemover.Click += BtnRemover_Click;

            panelAcoes.Controls.Add(btnRemover);
            panelAcoes.Resize += (_, _) =>
            {
                btnRemover.Location = new Point(panelAcoes.Width - btnRemover.Width - Dpi(16), (panelAcoes.Height - btnRemover.Height) / 2);
            };

            var separadorInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            // Grid
            dgvIgnorados = new OptimizedDataGridView
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
            Theme.EstilizarGrid(dgvIgnorados, "Ignorados_Grid");

            dgvIgnorados.Columns.AddRange(
                new DataGridViewTextBoxColumn
                {
                    Name = "colOrcamento",
                    HeaderText = "ORÇAMENTO",
                    DataPropertyName = "Orcamento",
                    Width = Dpi(110)
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colNome",
                    HeaderText = "CLIENTE",
                    DataPropertyName = "Nome",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colCidade",
                    HeaderText = "CIDADE",
                    DataPropertyName = "Cidade",
                    Width = Dpi(130)
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "colEstado",
                    HeaderText = "UF",
                    DataPropertyName = "Estado",
                    Width = Dpi(55)
                }
            );

            // Ignorado que não existe mais no estorc da filial: linha esmaecida e o botão
            // passa a "Remover da Lista" (não há o que restaurar nos pendentes).
            dgvIgnorados.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.CellStyle is null) return;
                if (dgvIgnorados.Rows[e.RowIndex].DataBoundItem is OrcamentoIgnorado { ExisteNoIntegra: false })
                    e.CellStyle.ForeColor = Theme.TextMuted;
            };
            dgvIgnorados.SelectionChanged += (_, _) => AtualizarBotao();

            panelCard.Controls.Add(dgvIgnorados);
            panelCard.Controls.Add(separadorInferior);
            panelCard.Controls.Add(panelAcoes);
            panelCard.Controls.Add(separador);
            panelCard.Controls.Add(panelHeader);

            Controls.Add(panelCard);

            ResumeLayout(false);
            PerformLayout();
        }

        private async Task CarregarAsync()
        {
            var (sortCol, sortDir) = DataGridViewSortHelper.SaveSort(dgvIgnorados);
            try
            {
                // Buscar IDs ignorados do banco de controle
                var ignorados = await _db.ExecutarControleAsync(async conn =>
                {
                    var ids = new List<int>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT orcamento FROM orcamentos_ignorados ORDER BY data_inclusao DESC";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        ids.Add(reader.GetInt32(0));
                    return ids;
                });
                if (IsDisposed) return; // janela fechada durante a carga

                if (ignorados == null || ignorados.Count == 0)
                {
                    _lista = [];
                    dgvIgnorados.SuspendLayout();
                    dgvIgnorados.SkipAutoAdjust = true;
                    try
                    {
                        dgvIgnorados.DataSource = _lista;
                        DataGridViewSortHelper.RestoreSort(dgvIgnorados, sortCol, sortDir);
                        AtualizarContagem();
                    }
                    finally
                    {
                        dgvIgnorados.SkipAutoAdjust = false;
                        dgvIgnorados.ResumeLayout(true);
                    }
                    return;
                }

                // Buscar dados dos orcamentos no integra
                var config = _db.Config;
                var dados = await _db.ConsultarIntegraAsync(async conn =>
                {
                    var result = new List<OrcamentoIgnorado>();
                    using var cmd = conn.CreateCommand();

                    var paramList = string.Join(",", ignorados.Select((_, i) => $"@p{i}"));
                    cmd.CommandText = $@"
                        SELECT e.orcamento, COALESCE(c.nome, e.nome) AS nome, e.cidade, e.estado
                        FROM estorc e
                        LEFT JOIN cadcli c ON c.cliente = e.cliente
                        WHERE e.orcamento IN ({paramList}) AND e.filial = @filial
                        ORDER BY e.orcamento DESC";

                    cmd.Parameters.AddWithValue("filial", config.Filial);
                    for (int i = 0; i < ignorados.Count; i++)
                        cmd.Parameters.AddWithValue($"p{i}", ignorados[i]);

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new OrcamentoIgnorado
                        {
                            Orcamento = reader.GetInt32(0),
                            Nome = await reader.IsDBNullAsync(1) ? "" : reader.GetString(1),
                            Cidade = await reader.IsDBNullAsync(2) ? "" : reader.GetString(2),
                            Estado = await reader.IsDBNullAsync(3) ? "" : reader.GetString(3)
                        });
                    }
                    return result;
                });
                if (IsDisposed) return;

                // Ignorados sem linha no estorc da filial (orçamento apagado no Integra ou
                // de outra filial) sumiam da tela e nunca podiam sair da tabela de
                // ignorados. Entram na lista como "órfãos" para poderem ser removidos.
                dados ??= [];
                var encontrados = dados.Select(d => d.Orcamento).ToHashSet();
                dados.AddRange(ignorados
                    .Where(o => !encontrados.Contains(o))
                    .OrderByDescending(o => o)
                    .Select(o => new OrcamentoIgnorado
                    {
                        Orcamento = o,
                        Nome = "(não existe mais no Integra)",
                        ExisteNoIntegra = false
                    }));

                _lista = new SortableBindingList<OrcamentoIgnorado>(dados);
                dgvIgnorados.SuspendLayout();
                dgvIgnorados.SkipAutoAdjust = true;
                try
                {
                    dgvIgnorados.DataSource = _lista;
                    DataGridViewSortHelper.RestoreSort(dgvIgnorados, sortCol, sortDir);
                    AtualizarContagem();
                }
                finally
                {
                    dgvIgnorados.SkipAutoAdjust = false;
                    dgvIgnorados.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao carregar ignorados", ex);
            }
        }

        private async void BtnRemover_Click(object? sender, EventArgs e)
        {
            if (_ocupado) return;
            if (dgvIgnorados.CurrentRow?.DataBoundItem is not OrcamentoIgnorado item) return;

            var confirm = item.ExisteNoIntegra
                ? MessageBox.Show(this,
                    $"Restaurar o orçamento {item.Orcamento} ({item.Nome})?\n\nEle voltará a aparecer nos pendentes.",
                    "Confirmar",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                : MessageBox.Show(this,
                    $"O orçamento {item.Orcamento} não existe mais no Integra (filial {_db.Config.Filial}).\n\n" +
                    "Ele será apenas removido da lista de ignorados — não há pedido para voltar aos pendentes. " +
                    "Se o orçamento reaparecer no Integra, ele voltará a ser exibido normalmente.\n\n" +
                    "Remover da lista de ignorados?",
                    "Orçamento inexistente no Integra",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            // Sem a trava, um segundo clique durante o await repetia o DELETE e exibia um
            // falso "restaurado por outro usuário"; fechar no meio perdia o _alterou
            // (Form1 não recarregava) e o MessageBox(this) estourava em form descartado.
            _ocupado = true;
            btnRemover.Enabled = false;
            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM orcamentos_ignorados WHERE orcamento = @orc";
                    cmd.Parameters.AddWithValue("orc", item.Orcamento);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas == 0)
                {
                    MessageBox.Show(this, "Este item já foi restaurado por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                _alterou = true;
                await CarregarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao restaurar: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _ocupado = false;
                btnRemover.Enabled = true;
            }
        }

        private void AtualizarContagem()
        {
            int orfaos = _lista.Count(i => !i.ExisteNoIntegra);
            lblContagem.Text = $"{_lista.Count} ignorado{(_lista.Count != 1 ? "s" : "")}" +
                (orfaos > 0 ? $" ({orfaos} inexistente{(orfaos != 1 ? "s" : "")} no Integra)" : "");
            AtualizarBotao();
        }

        private void AtualizarBotao()
        {
            btnRemover.Text = dgvIgnorados.CurrentRow?.DataBoundItem is OrcamentoIgnorado { ExisteNoIntegra: false }
                ? "Remover da Lista"
                : "Restaurar Selecionado";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_ocupado && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }
            if (_alterou)
                DialogResult = DialogResult.OK;
            base.OnFormClosing(e);
        }
    }

    public class OrcamentoIgnorado
    {
        public int Orcamento { get; set; }
        public string Nome { get; set; } = "";
        public string Cidade { get; set; } = "";
        public string Estado { get; set; } = "";
        /// <summary>False quando o orçamento ignorado não tem mais linha no estorc da filial.</summary>
        public bool ExisteNoIntegra { get; set; } = true;
    }
}
