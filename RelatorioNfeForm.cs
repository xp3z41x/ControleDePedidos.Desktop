using System.ComponentModel;
using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    public class RelatorioNfeForm : Form
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private DataGridView dgvPedidos = null!;
        private Button btnMarcarTodos = null!;
        private Button btnDesmarcarTodos = null!;
        private Button btnMarcarRota = null!;
        private Button btnVisualizar = null!;
        private Button btnGerar = null!;
        private Label lblContagem = null!;
        private Label lblSelecionados = null!;

        public RelatorioNfeForm()
        {
            InitializeUI();
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);
            _ = CarregarAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Relatório de NF-e";
            ClientSize = new Size(950, 620);
            MinimumSize = new Size(750, 500);
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
                Text = "Relatório de NF-e",
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

            btnMarcarRota = new Button
            {
                Text = "Marcar Rota",
                Size = new Size(110, 32)
            };
            Theme.EstilizarBotao(btnMarcarRota);
            btnMarcarRota.ForeColor = Theme.Accent;
            btnMarcarRota.Click += (_, _) => MarcarPorRota();

            lblSelecionados = new Label
            {
                Text = "0 selecionados",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                AutoSize = true
            };

            btnVisualizar = new Button
            {
                Text = "Visualizar",
                Size = new Size(110, 32)
            };
            Theme.EstilizarBotao(btnVisualizar);
            btnVisualizar.Click += BtnVisualizar_Click;

            btnGerar = new Button
            {
                Text = "Imprimir",
                Size = new Size(110, 32)
            };
            Theme.EstilizarBotao(btnGerar, primario: true);
            btnGerar.Click += BtnGerar_Click;

            panelAcoes.Controls.Add(btnMarcarTodos);
            panelAcoes.Controls.Add(btnDesmarcarTodos);
            panelAcoes.Controls.Add(btnMarcarRota);
            panelAcoes.Controls.Add(lblSelecionados);
            panelAcoes.Controls.Add(btnVisualizar);
            panelAcoes.Controls.Add(btnGerar);

            panelAcoes.Resize += (_, _) =>
            {
                int cy = (panelAcoes.Height - 32) / 2;
                btnMarcarTodos.Location = new Point(16, cy);
                btnDesmarcarTodos.Location = new Point(btnMarcarTodos.Right + 8, cy);
                btnMarcarRota.Location = new Point(btnDesmarcarTodos.Right + 8, cy);

                lblSelecionados.Location = new Point(
                    btnMarcarRota.Right + 16,
                    (panelAcoes.Height - lblSelecionados.Height) / 2);

                btnGerar.Location = new Point(panelAcoes.Width - btnGerar.Width - 16, cy);
                btnVisualizar.Location = new Point(btnGerar.Left - btnVisualizar.Width - 8, cy);
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
            Theme.EstilizarGrid(dgvPedidos, "RelatorioNfe_Grid");

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

            dgvPedidos.Columns.AddRange(
                colCheck,
                new DataGridViewTextBoxColumn { Name = "colOrcamento", HeaderText = "ORÇAMENTO", DataPropertyName = "Orcamento", Width = 100, MinimumWidth = 80, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colCliente", HeaderText = "CLIENTE", DataPropertyName = "Cliente", Width = 220, MinimumWidth = 120, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colFantasia", HeaderText = "NOME FANTASIA", DataPropertyName = "NomeFantasia", Width = 200, MinimumWidth = 100, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colRota", HeaderText = "ROTA", DataPropertyName = "Rota", Width = 120, MinimumWidth = 80, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colNfe", HeaderText = "Nº NF-e", DataPropertyName = "NumeroNfe", Width = 110, MinimumWidth = 70, ReadOnly = true }
            );

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
            try
            {
                var config = _db.Config;

                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<PedidoNfe>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT orcamento, cliente, nome_fantasia, rota
                        FROM pedidos_controlados
                        ORDER BY orcamento DESC";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new PedidoNfe
                        {
                            Orcamento = reader.GetInt32(0),
                            Cliente = reader.GetString(1),
                            NomeFantasia = reader.GetString(2),
                            Rota = reader.GetString(3)
                        });
                    }
                    return result;
                });

                var dados = lista ?? [];

                // Buscar NF-e do integra via estmov
                if (dados.Count > 0 && _db.IntegraConectado && config.FilialFiscal > 0)
                {
                    try
                    {
                        var nfeData = await _db.ConsultarIntegraAsync(async conn =>
                        {
                            var dict = new Dictionary<string, string>();
                            using var cmd = conn.CreateCommand();
                            var paramList = string.Join(",", dados.Select((_, i) => $"@p{i}"));
                            cmd.CommandText = $@"
                                SELECT observacao, nota_fiscal
                                FROM estmov
                                WHERE filial = @filialFiscal
                                  AND observacao IN ({paramList})";
                            cmd.Parameters.AddWithValue("filialFiscal", config.FilialFiscal);
                            for (int i = 0; i < dados.Count; i++)
                                cmd.Parameters.AddWithValue($"p{i}", dados[i].Orcamento.ToString());
                            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                var obs = await reader.IsDBNullAsync(0) ? "" : reader.GetString(0);
                                var nf = await reader.IsDBNullAsync(1) ? "" : reader.GetInt32(1).ToString();
                                if (!string.IsNullOrEmpty(obs))
                                    dict[obs] = nf;
                            }
                            return dict;
                        });

                        if (nfeData != null)
                        {
                            foreach (var p in dados)
                            {
                                if (nfeData.TryGetValue(p.Orcamento.ToString(), out var nf))
                                    p.NumeroNfe = nf;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Erro ao buscar NF-e do integra", ex);
                    }
                }

                dgvPedidos.DataSource = new SortableBindingList<PedidoNfe>(dados);
                lblContagem.Text = $"{dados.Count} pedido{(dados.Count != 1 ? "s" : "")}";
                AtualizarContagemSelecionados();
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
                row.Cells["colSelecionar"].Value = selecionar;
            dgvPedidos.EndEdit();
            dgvPedidos.RefreshEdit();
            AtualizarContagemSelecionados();
        }

        private async void MarcarPorRota()
        {
            List<RotaItem>? rotas = null;
            try
            {
                rotas = await _db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<RotaItem>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, nome FROM cadastro_rotas ORDER BY nome";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        result.Add(new RotaItem { Id = reader.GetInt32(0), Nome = reader.GetString(1) });
                    return result;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao carregar rotas: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (rotas == null || rotas.Count == 0)
            {
                MessageBox.Show(this, "Nenhuma rota cadastrada.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new Form
            {
                Text = "Selecionar Rota",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Theme.BgDark,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                ClientSize = new Size(350, 140)
            };

            var lblRota = new Label
            {
                Text = "Marcar pedidos da rota:",
                Location = new Point(16, 16),
                AutoSize = true,
                ForeColor = Theme.TextPrimary
            };

            var cmbRota = new ComboBox
            {
                Location = new Point(16, 42),
                Size = new Size(318, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontRegular,
                DisplayMember = "Nome",
                DataSource = rotas
            };

            var btnCancelar = new Button
            {
                Text = "Cancelar",
                Size = new Size(90, 32),
                Location = new Point(244, 88),
                DialogResult = DialogResult.Cancel
            };
            Theme.EstilizarBotao(btnCancelar);

            var btnOk = new Button
            {
                Text = "Confirmar",
                Size = new Size(90, 32),
                Location = new Point(146, 88),
                DialogResult = DialogResult.OK
            };
            Theme.EstilizarBotao(btnOk, primario: true);

            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancelar;
            dialog.Controls.AddRange([lblRota, cmbRota, btnOk, btnCancelar]);

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var rotaSelecionada = (cmbRota.SelectedItem as RotaItem)?.Nome ?? "";
            if (string.IsNullOrEmpty(rotaSelecionada)) return;

            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.DataBoundItem is PedidoNfe p && p.Rota == rotaSelecionada)
                    row.Cells["colSelecionar"].Value = true;
            }
            dgvPedidos.EndEdit();
            dgvPedidos.RefreshEdit();
            AtualizarContagemSelecionados();
        }

        private List<PedidoNfe> ObterSelecionados()
        {
            var selecionados = new List<PedidoNfe>();
            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.Cells["colSelecionar"].Value is true && row.DataBoundItem is PedidoNfe p)
                    selecionados.Add(p);
            }
            return selecionados;
        }

        private void AtualizarContagemSelecionados()
        {
            int count = ObterSelecionados().Count;
            lblSelecionados.Text = $"{count} selecionado{(count != 1 ? "s" : "")}";
        }

        private void BtnGerar_Click(object? sender, EventArgs e)
        {
            var selecionados = ObterSelecionados();
            if (selecionados.Count == 0)
            {
                MessageBox.Show(this, "Nenhum pedido selecionado.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Ordenar por Cliente ASC
            selecionados.Sort((a, b) =>
                string.Compare(a.Cliente, b.Cliente, StringComparison.OrdinalIgnoreCase));

            using var doc = new RelatorioNfePrintDoc(selecionados);
            using var dialog = new PrintDialog
            {
                Document = doc,
                UseEXDialog = true
            };
            // Camada 1 (PrintingHelper.AplicarConfigA4) já pré-selecionou impressora A4-capable
            // no construtor da PrintDoc. PrintDialog abrirá com ela, não com a térmica padrão.

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                // Camada 2: usuário pode ter trocado de impressora — re-forçar A4/Landscape/Margins.
                // PrintDialog reescreve DefaultPageSettings baseado na impressora final escolhida.
                doc.DefaultPageSettings.PaperSize = PrintingHelper.CriarPaperSizeA4();
                doc.DefaultPageSettings.Landscape = true;
                doc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

                if (!PrintingHelper.ImpressoraSuportaA4(doc))
                {
                    var continuar = MessageBox.Show(this,
                        $"A impressora '{doc.PrinterSettings.PrinterName}' não declara suporte a papel A4.\n" +
                        "O documento pode ser impresso de forma incorreta ou rejeitado pelo driver.\n\n" +
                        "Imprimir mesmo assim?",
                        "Aviso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (continuar != DialogResult.Yes) return;
                }

                PrintingHelper.LogarEstado(doc, "Imprimir Relatório de NF-e");
                try
                {
                    doc.Print();
                }
                catch (Exception ex)
                {
                    Logger.Error("Erro ao imprimir Relatório de NF-e", ex);
                    MessageBox.Show(this, $"Erro ao imprimir: {ex.Message}", "Erro",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnVisualizar_Click(object? sender, EventArgs e)
        {
            var selecionados = ObterSelecionados();
            if (selecionados.Count == 0)
            {
                MessageBox.Show(this, "Nenhum pedido selecionado.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            selecionados.Sort((a, b) =>
                string.Compare(a.Cliente, b.Cliente, StringComparison.OrdinalIgnoreCase));

            var doc = new RelatorioNfePrintDoc(selecionados);
            PrintingHelper.LogarEstado(doc, "Visualizar Relatório de NF-e");
            using var preview = new PrintPreviewDialog
            {
                Document = doc,
                WindowState = FormWindowState.Maximized
            };
            preview.ShowDialog(this);
        }
    }

    public class PedidoNfe
    {
        public int Orcamento { get; set; }
        public string Cliente { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
        public string Rota { get; set; } = "";
        public string NumeroNfe { get; set; } = "";
    }
}
