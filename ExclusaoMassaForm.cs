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
        private Button btnMarcarRotas = null!;
        private Button btnExcluir = null!;
        private Label lblContagem = null!;
        private Label lblSelecionados = null!;
        private bool _alterou;

        public ExclusaoMassaForm()
        {
            InitializeUI();
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);
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

            btnMarcarRotas = new Button
            {
                Text = "Marcar Rotas",
                Size = new Size(120, 32)
            };
            Theme.EstilizarBotao(btnMarcarRotas);
            btnMarcarRotas.ForeColor = Theme.Accent;
            btnMarcarRotas.Click += BtnMarcarRotas_Click;

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
            panelAcoes.Controls.Add(btnMarcarRotas);
            panelAcoes.Controls.Add(lblSelecionados);
            panelAcoes.Controls.Add(btnExcluir);

            panelAcoes.Resize += (_, _) =>
            {
                int cy = (panelAcoes.Height - 32) / 2;
                btnMarcarTodos.Location = new Point(16, cy);
                btnDesmarcarTodos.Location = new Point(btnMarcarTodos.Right + 8, cy);
                btnMarcarFaturados.Location = new Point(btnDesmarcarTodos.Right + 8, cy);
                btnMarcarRotas.Location = new Point(btnMarcarFaturados.Right + 8, cy);

                lblSelecionados.Location = new Point(
                    btnMarcarRotas.Right + 16,
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
                        Logger.Warn("Erro ao buscar pedido_venda", ex);
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

        private const string SemRotaLabel = "(Sem rota)";

        private void BtnMarcarRotas_Click(object? sender, EventArgs e)
        {
            var rotas = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.DataBoundItem is PedidoExclusao p)
                    rotas.Add(string.IsNullOrWhiteSpace(p.Rota) ? SemRotaLabel : p.Rota.Trim());
            }

            if (rotas.Count == 0)
            {
                MessageBox.Show(this, "Nenhuma rota disponível.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SelecionarRotasParaMarcarDialog(rotas);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.RotasSelecionadas.Count > 0)
            {
                MarcarPorRotas(dlg.RotasSelecionadas);
            }
        }

        private void MarcarPorRotas(ISet<string> rotasSelecionadas)
        {
            // Comparador case-insensitive para os nomes de rota selecionados;
            // "(Sem rota)" e o sentinel para linhas sem rota.
            bool incluirSemRota = rotasSelecionadas.Contains(SemRotaLabel);
            var rotasNormalizadas = new HashSet<string>(
                rotasSelecionadas.Where(r => r != SemRotaLabel),
                StringComparer.CurrentCultureIgnoreCase);

            foreach (DataGridViewRow row in dgvPedidos.Rows)
            {
                if (row.DataBoundItem is not PedidoExclusao p) continue;

                bool semRota = string.IsNullOrWhiteSpace(p.Rota);
                bool match = (semRota && incluirSemRota)
                          || (!semRota && rotasNormalizadas.Contains(p.Rota.Trim()));

                if (match)
                    row.Cells["colSelecionar"].Value = true;
            }

            dgvPedidos.EndEdit();
            AtualizarContagemSelecionados();
        }

        // ============================================================
        // Dialog auxiliar — picker multi-seleção de rotas
        // ============================================================
        private sealed class SelecionarRotasParaMarcarDialog : Form
        {
            private readonly CheckedListBox _lista;
            private readonly Button _btnOk;
            private readonly Button _btnCancelar;
            private readonly Button _btnMarcarTodas;
            private readonly Button _btnDesmarcarTodas;

            public HashSet<string> RotasSelecionadas { get; } = new(StringComparer.Ordinal);

            public SelecionarRotasParaMarcarDialog(IEnumerable<string> rotas)
            {
                Text = "Marcar pedidos por rota";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;
                BackColor = Theme.BgDark;
                ForeColor = Theme.TextPrimary;
                Font = Theme.FontRegular;
                ClientSize = new Size(380, 420);
                ShowInTaskbar = false;

                var lblTitulo = new Label
                {
                    Text = "Selecione uma ou mais rotas:",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    ForeColor = Theme.TextPrimary,
                    Font = Theme.FontRegular
                };

                _lista = new CheckedListBox
                {
                    Location = new Point(16, 44),
                    Size = new Size(348, 280),
                    BackColor = Theme.BgInput,
                    ForeColor = Theme.TextPrimary,
                    Font = Theme.FontRegular,
                    BorderStyle = BorderStyle.FixedSingle,
                    CheckOnClick = true,
                    IntegralHeight = false
                };
                foreach (var r in rotas) _lista.Items.Add(r);

                _btnMarcarTodas = new Button
                {
                    Text = "Marcar todas",
                    Size = new Size(110, 28),
                    Location = new Point(16, 332)
                };
                Theme.EstilizarBotao(_btnMarcarTodas);
                _btnMarcarTodas.Click += (_, _) => DefinirTodas(true);

                _btnDesmarcarTodas = new Button
                {
                    Text = "Desmarcar todas",
                    Size = new Size(130, 28),
                    Location = new Point(132, 332)
                };
                Theme.EstilizarBotao(_btnDesmarcarTodas);
                _btnDesmarcarTodas.Click += (_, _) => DefinirTodas(false);

                _btnCancelar = new Button
                {
                    Text = "Cancelar",
                    Size = new Size(90, 32),
                    Location = new Point(274, 376),
                    DialogResult = DialogResult.Cancel
                };
                Theme.EstilizarBotao(_btnCancelar);

                _btnOk = new Button
                {
                    Text = "Confirmar",
                    Size = new Size(90, 32),
                    Location = new Point(178, 376)
                };
                Theme.EstilizarBotao(_btnOk, primario: true);
                _btnOk.Click += BtnOk_Click;

                AcceptButton = _btnOk;
                CancelButton = _btnCancelar;

                Controls.AddRange([lblTitulo, _lista, _btnMarcarTodas, _btnDesmarcarTodas, _btnOk, _btnCancelar]);
            }

            private void DefinirTodas(bool marcado)
            {
                for (int i = 0; i < _lista.Items.Count; i++)
                    _lista.SetItemChecked(i, marcado);
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                RotasSelecionadas.Clear();
                foreach (var item in _lista.CheckedItems)
                {
                    if (item is string s) RotasSelecionadas.Add(s);
                }

                if (RotasSelecionadas.Count == 0)
                {
                    MessageBox.Show(this, "Selecione ao menos uma rota.", "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            }
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

            var confirm = MessageBox.Show(this,
                $"ATENÇÃO: Esta ação é irreversível!\n\nExcluir {selecionados.Count} pedido{(selecionados.Count != 1 ? "s" : "")} do controle?\n\nOs orçamentos voltarão para a lista de pendentes caso não estejam faturados!",
                "Confirmar Exclusão em Massa",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            btnExcluir.Enabled = false;
            btnMarcarTodos.Enabled = false;
            btnDesmarcarTodos.Enabled = false;
            btnMarcarFaturados.Enabled = false;
            btnMarcarRotas.Enabled = false;

            try
            {
                int excluidos = 0;
                int conflitos = 0;
                int erros = 0;
                string? primeiroErro = null;

                // Lookup batch das NFes antes do loop (1 query com IN, zero N+1)
                var nfes = await _db.BuscarNotasFiscaisAsync(selecionados.Select(p => p.Orcamento));

                // Excluir um a um com verificação de concorrência + historico
                foreach (var pedido in selecionados)
                {
                    try
                    {
                        nfes.TryGetValue(pedido.Orcamento, out var nfe);
                        var reg = new ExclusaoRegistro(
                            Orcamento: pedido.Orcamento,
                            Cliente: pedido.Cliente,
                            NomeFantasia: pedido.NomeFantasia,
                            Rota: pedido.Rota,
                            Vendedor: "", // PedidoExclusao nao carrega vendedor
                            PedidoVenda: pedido.PedidoVenda,
                            NotaFiscal: nfe ?? "",
                            Status: ExclusaoRegistro.CalcularStatus(
                                pedido.Aprovado, pedido.EmSeparacao, pedido.Conferido,
                                pedido.Liberado, pedido.Travado, pedido.EmFaturamento,
                                pedido.PedidoVenda),
                            Aprovado: pedido.Aprovado,
                            EmSeparacao: pedido.EmSeparacao,
                            Conferido: pedido.Conferido,
                            Liberado: pedido.Liberado,
                            Travado: pedido.Travado,
                            EmFaturamento: pedido.EmFaturamento,
                            TipoExclusao: ExclusaoRegistro.TipoMassa);

                        var linhas = await _db.ExcluirPedidoComHistoricoAsync(reg);

                        if (linhas > 0)
                            excluidos++;
                        else
                            conflitos++;
                    }
                    catch (Exception ex)
                    {
                        erros++;
                        primeiroErro ??= ex.Message;
                        Logger.Error($"Erro ao excluir pedido {pedido.Orcamento}", ex);
                    }
                }

                _alterou = excluidos > 0;

                var mensagem = $"Exclusão concluída:\n\n• {excluidos} pedido{(excluidos != 1 ? "s" : "")} excluído{(excluidos != 1 ? "s" : "")} com sucesso.";
                if (conflitos > 0)
                    mensagem += $"\n• {conflitos} pedido{(conflitos != 1 ? "s" : "")} já {(conflitos != 1 ? "haviam" : "havia")} sido excluído{(conflitos != 1 ? "s" : "")} por outro usuário.";
                if (erros > 0)
                    mensagem += $"\n• {erros} pedido{(erros != 1 ? "s" : "")} com erro ao excluir.\n  Primeiro erro: {primeiroErro}";

                var icone = erros > 0
                    ? MessageBoxIcon.Error
                    : (conflitos > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                MessageBox.Show(this, mensagem, "Resultado", MessageBoxButtons.OK, icone);

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
                btnMarcarRotas.Enabled = true;
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
