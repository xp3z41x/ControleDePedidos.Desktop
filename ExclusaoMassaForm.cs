using System.ComponentModel;

namespace CONTROLE_DE_PEDIDOS
{
    public class ExclusaoMassaForm : Form
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private OptimizedDataGridView dgvPedidos = null!;
        private Button btnMarcarTodos = null!;
        private Button btnDesmarcarTodos = null!;
        private Button btnMarcarFaturados = null!;
        private Button btnExcluir = null!;
        private Label lblContagem = null!;
        private Label lblSelecionados = null!;
        private bool _alterou;

        public ExclusaoMassaForm()
        {
            InitializeUI();
            _ = CarregarAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Exclusão em Massa de Pedidos";
            ClientSize = new Size(1050, 620);
            MinimumSize = new Size(800, 500);
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

            // Header
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.BgCard
            };

            var lblTitulo = new Label
            {
                Text = "Exclusão em Massa",
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

            // Barra de ações (rodapé)
            var panelAcoes = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Theme.BgCard
            };
            panelAcoes.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAcoes.Width, 0);
            };

            btnMarcarTodos = new Button
            {
                Text = "Marcar Todos",
                Size = new Size(120, 32)
            };
            Theme.EstilizarBotao(btnMarcarTodos);
            btnMarcarTodos.Click += (_, _) => AlterarSelecaoTodos(true);

            btnDesmarcarTodos = new Button
            {
                Text = "Desmarcar Todos",
                Size = new Size(130, 32)
            };
            Theme.EstilizarBotao(btnDesmarcarTodos);
            btnDesmarcarTodos.Click += (_, _) => AlterarSelecaoTodos(false);

            btnMarcarFaturados = new Button
            {
                Text = "Marcar Faturados",
                Size = new Size(140, 32)
            };
            Theme.EstilizarBotao(btnMarcarFaturados);
            btnMarcarFaturados.ForeColor = Theme.Accent;
            btnMarcarFaturados.Click += (_, _) => MarcarFaturados();

            lblSelecionados = new Label
            {
                Text = "0 selecionados",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true
            };

            btnExcluir = new Button
            {
                Text = "Excluir Selecionados",
                Size = new Size(170, 32)
            };
            Theme.EstilizarBotao(btnExcluir, primario: true);
            btnExcluir.BackColor = Theme.Error;
            btnExcluir.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 60, 55);
            btnExcluir.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 50, 45);
            btnExcluir.Click += BtnExcluir_Click;

            panelAcoes.Controls.Add(btnMarcarTodos);
            panelAcoes.Controls.Add(btnDesmarcarTodos);
            panelAcoes.Controls.Add(btnMarcarFaturados);
            panelAcoes.Controls.Add(lblSelecionados);
            panelAcoes.Controls.Add(btnExcluir);

            panelAcoes.Resize += (_, _) =>
            {
                int cy = (panelAcoes.Height - 32) / 2;
                btnMarcarTodos.Location = new Point(16, cy);
                btnDesmarcarTodos.Location = new Point(btnMarcarTodos.Right + 8, cy);
                btnMarcarFaturados.Location = new Point(btnDesmarcarTodos.Right + 8, cy);

                lblSelecionados.Location = new Point(
                    btnMarcarFaturados.Right + 16,
                    (panelAcoes.Height - lblSelecionados.Height) / 2);

                btnExcluir.Location = new Point(panelAcoes.Width - btnExcluir.Width - 16, cy);
            };

            var separadorInferior = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Theme.Border
            };

            // Grid
            dgvPedidos = new OptimizedDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ScrollBars = ScrollBars.Both,
                ReadOnly = false
            };
            Theme.EstilizarGrid(dgvPedidos, "ExclusaoMassa_Grid");

            // Coluna checkbox
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colSelecionar",
                HeaderText = "",
                Width = 40,
                MinimumWidth = 40,
                ReadOnly = false,
                FalseValue = false,
                TrueValue = true,
                Resizable = DataGridViewTriState.False
            };
            colCheck.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCheck.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

            var statusCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

            dgvPedidos.Columns.AddRange(
                colCheck,
                new DataGridViewTextBoxColumn { Name = "colOrcamento", HeaderText = "ORÇAMENTO", DataPropertyName = "Orcamento", Width = 100, MinimumWidth = 80, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colCliente", HeaderText = "CLIENTE", DataPropertyName = "Cliente", Width = 200, MinimumWidth = 120, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colFantasia", HeaderText = "NOME FANTASIA", DataPropertyName = "NomeFantasia", Width = 170, MinimumWidth = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colRota", HeaderText = "ROTA", DataPropertyName = "Rota", Width = 120, MinimumWidth = 80, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "STATUS", DataPropertyName = "StatusResumo", Width = 140, MinimumWidth = 80, ReadOnly = true, DefaultCellStyle = statusCellStyle },
                new DataGridViewTextBoxColumn { Name = "colFaturado", HeaderText = "FATURADO", DataPropertyName = "FaturadoTexto", Width = 110, MinimumWidth = 80, ReadOnly = true, DefaultCellStyle = statusCellStyle }
            );

            dgvPedidos.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                var colName = dgvPedidos.Columns[e.ColumnIndex].Name;
                if (colName == "colFaturado")
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
                else if (colName == "colStatus")
                {
                    var valor = e.Value?.ToString();
                    Color bg, fg;
                    if (valor == "TRAVADO")
                    {
                        bg = Theme.StatusTravadoBg;
                        fg = Theme.StatusTravadoFg;
                    }
                    else
                    {
                        bg = Theme.BgPanel;
                        fg = Theme.TextSecondary;
                    }
                    e.CellStyle.BackColor = bg;
                    e.CellStyle.ForeColor = fg;
                    e.CellStyle.SelectionBackColor = bg;
                    e.CellStyle.SelectionForeColor = fg;
                    e.CellStyle.Font = Theme.FontStatus;
                }
            };

            // Click na célula da checkbox — toggle manual
            dgvPedidos.CellContentClick += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 0) return;
                dgvPedidos.EndEdit();
                AtualizarContagemSelecionados();
            };

            panelCard.Controls.Add(dgvPedidos);
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
            var (sortCol, sortDir) = DataGridViewSortHelper.SaveSort(dgvPedidos);
            try
            {
                var config = _db.Config;

                // Carregar pedidos do banco de controle
                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<PedidoExclusao>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT orcamento, cliente, nome_fantasia, rota,
                               aprovado, em_separacao, conferido, liberado, travado, em_faturamento
                        FROM pedidos_controlados
                        ORDER BY orcamento DESC";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new PedidoExclusao
                        {
                            Orcamento = reader.GetInt32(0),
                            Cliente = reader.GetString(1),
                            NomeFantasia = reader.GetString(2),
                            Rota = reader.GetString(3),
                            Aprovado = reader.GetBoolean(4),
                            EmSeparacao = reader.GetBoolean(5),
                            Conferido = reader.GetBoolean(6),
                            Liberado = reader.GetBoolean(7),
                            Travado = reader.GetBoolean(8),
                            EmFaturamento = reader.GetBoolean(9)
                        });
                    }
                    return result;
                });

                // Buscar pedido_venda do integra
                var dados = lista ?? [];
                if (dados.Count > 0 && _db.IntegraConectado)
                {
                    try
                    {
                        var faturados = await _db.ConsultarIntegraAsync(async conn =>
                        {
                            var dict = new Dictionary<int, int>();
                            using var cmd = conn.CreateCommand();
                            var paramList = string.Join(",", dados.Select((_, i) => $"@p{i}"));
                            cmd.CommandText = $"SELECT orcamento, pedido_venda FROM estorc WHERE filial = @filial AND orcamento IN ({paramList})";
                            cmd.Parameters.AddWithValue("filial", config.Filial);
                            for (int i = 0; i < dados.Count; i++)
                                cmd.Parameters.AddWithValue($"p{i}", dados[i].Orcamento);
                            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                            while (await reader.ReadAsync().ConfigureAwait(false))
                                dict[reader.GetInt32(0)] = reader.GetInt32(1);
                            return dict;
                        });

                        if (faturados != null)
                        {
                            foreach (var p in dados)
                            {
                                if (faturados.TryGetValue(p.Orcamento, out int pv))
                                    p.PedidoVenda = pv;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro ao buscar pedido_venda: {ex}");
                    }
                }

                dgvPedidos.SuspendLayout();
                dgvPedidos.SkipAutoAdjust = true;
                try
                {
                    dgvPedidos.DataSource = new SortableBindingList<PedidoExclusao>(dados);
                    DataGridViewSortHelper.RestoreSort(dgvPedidos, sortCol, sortDir);
                    lblContagem.Text = $"{dados.Count} pedido{(dados.Count != 1 ? "s" : "")}";
                    AtualizarContagemSelecionados();
                }
                finally
                {
                    dgvPedidos.SkipAutoAdjust = false;
                    dgvPedidos.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao carregar pedidos: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AlterarSelecaoTodos(bool selecionar)
        {
            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                row.Cells["colSelecionar"].Value = selecionar;
            }
            dgvPedidos.EndEdit();
            dgvPedidos.RefreshEdit();
            AtualizarContagemSelecionados();
        }

        private void MarcarFaturados()
        {
            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.DataBoundItem is PedidoExclusao p && p.PedidoVenda > 0)
                    row.Cells["colSelecionar"].Value = true;
            }
            dgvPedidos.EndEdit();
            dgvPedidos.RefreshEdit();
            AtualizarContagemSelecionados();
        }

        private List<PedidoExclusao> ObterSelecionados()
        {
            var selecionados = new List<PedidoExclusao>();
            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.Cells["colSelecionar"].Value is true && row.DataBoundItem is PedidoExclusao p)
                    selecionados.Add(p);
            }
            return selecionados;
        }

        private void AtualizarContagemSelecionados()
        {
            int count = ObterSelecionados().Count;
            lblSelecionados.Text = $"{count} selecionado{(count != 1 ? "s" : "")}";
        }

        private async void BtnExcluir_Click(object? sender, EventArgs e)
        {
            var selecionados = ObterSelecionados();
            if (selecionados.Count == 0)
            {
                MessageBox.Show(this, "Nenhum pedido selecionado.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Verificar se há pedidos em faturamento entre os selecionados
            var emFaturamento = selecionados.Where(p => p.EmFaturamento).ToList();
            if (emFaturamento.Count > 0)
            {
                var orcs = string.Join(", ", emFaturamento.Select(p => p.Orcamento));
                MessageBox.Show(this,
                    $"Os seguintes pedidos estão em faturamento e não podem ser excluídos:\n{orcs}\n\nO operador responsável deve largá-los primeiro.\nEsses pedidos serão ignorados na exclusão.",
                    "Pedidos em Faturamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                selecionados = selecionados.Where(p => !p.EmFaturamento).ToList();
                if (selecionados.Count == 0) return;
            }

            var confirm = MessageBox.Show(this,
                $"ATENÇÃO: Esta ação é irreversível!\n\nExcluir {selecionados.Count} pedido{(selecionados.Count != 1 ? "s" : "")} do controle?\n\nOs orçamentos voltarão para a lista de pendentes.",
                "Confirmar Exclusão em Massa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            btnExcluir.Enabled = false;
            btnMarcarTodos.Enabled = false;
            btnDesmarcarTodos.Enabled = false;
            btnMarcarFaturados.Enabled = false;

            try
            {
                int excluidos = 0;
                int conflitos = 0;

                // Excluir um a um com verificação de concorrência
                foreach (var pedido in selecionados)
                {
                    try
                    {
                        var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = "DELETE FROM pedidos_controlados WHERE orcamento = @orc";
                            cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        });

                        if (linhas > 0)
                            excluidos++;
                        else
                            conflitos++;
                    }
                    catch
                    {
                        conflitos++;
                    }
                }

                _alterou = excluidos > 0;

                var mensagem = $"Exclusão concluída:\n\n• {excluidos} pedido{(excluidos != 1 ? "s" : "")} excluído{(excluidos != 1 ? "s" : "")} com sucesso.";
                if (conflitos > 0)
                    mensagem += $"\n• {conflitos} pedido{(conflitos != 1 ? "s" : "")} não {(conflitos != 1 ? "puderam" : "pôde")} ser excluído{(conflitos != 1 ? "s" : "")} (já excluído{(conflitos != 1 ? "s" : "")} por outro usuário).";

                MessageBox.Show(this, mensagem, "Resultado",
                    MessageBoxButtons.OK, conflitos > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                await CarregarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro durante a exclusão: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnExcluir.Enabled = true;
                btnMarcarTodos.Enabled = true;
                btnDesmarcarTodos.Enabled = true;
                btnMarcarFaturados.Enabled = true;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_alterou)
                DialogResult = DialogResult.OK;
            base.OnFormClosing(e);
        }
    }

    public class PedidoExclusao
    {
        public int Orcamento { get; set; }
        public string Cliente { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
        public string Rota { get; set; } = "";
        public bool Aprovado { get; set; }
        public bool EmSeparacao { get; set; }
        public bool Conferido { get; set; }
        public bool Liberado { get; set; }
        public bool Travado { get; set; }
        public bool EmFaturamento { get; set; }
        public int PedidoVenda { get; set; }

        public string StatusResumo
        {
            get
            {
                if (Travado) return "TRAVADO";
                if (Liberado) return "Liberado";
                if (Conferido) return "Conferido";
                if (EmSeparacao) return "Em Separação";
                if (Aprovado) return "Aprovado";
                return "Pendente";
            }
        }

        public string FaturadoTexto => PedidoVenda > 0 ? PedidoVenda.ToString() : (EmFaturamento ? "EM FATURAMENTO" : "NÃO");
    }
}
