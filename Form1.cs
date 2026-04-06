using System.ComponentModel;

namespace CONTROLE_DE_PEDIDOS
{
    public partial class Form1 : Form
    {
        private readonly DatabaseService _db;
        private bool _dadosIniciaisCarregados;
        private HashSet<int> _ignorados = [];
        private HashSet<int> _aprovados = [];
        private bool _recarregando;

        public Form1()
        {
            InitializeComponent();
            _db = DatabaseService.Instancia;
            _db.StatusAlterado += Db_StatusAlterado;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke(() => _db.Iniciar(15000));
        }

        private async Task CarregarIgnoradosAsync()
        {
            if (!_db.ControleConectado) return;

            try
            {
                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var ids = new HashSet<int>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT orcamento FROM orcamentos_ignorados";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        ids.Add(reader.GetInt32(0));
                    return ids;
                });

                _ignorados = lista ?? [];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar orçamentos ignorados: {ex}");
                _ignorados = [];
            }
        }

        private async Task CarregarOrcamentosAsync()
        {
            if (!_db.IntegraConectado) return;

            await CarregarIgnoradosAsync();
            await CarregarAprovadosAsync();

            // Salvar estado atual para restaurar após reload
            int orcSelecionado = -1;
            if (dgvOrcamentos.CurrentRow?.DataBoundItem is OrcamentoResumo orcAtual)
                orcSelecionado = orcAtual.Orcamento;

            int firstDisplayedRowIndex = dgvOrcamentos.FirstDisplayedScrollingRowIndex;
            var (sortCol, sortDir) = DataGridViewSortHelper.SaveSort(dgvOrcamentos);

            try
            {
                var config = _db.Config;

                var lista = await _db.ConsultarIntegraAsync(async conn =>
                {
                    var result = new List<OrcamentoResumo>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT e.orcamento, COALESCE(c.nome, e.nome) AS nome, COALESCE(c.fantasia, '') AS fantasia
                        FROM estorc e
                        LEFT JOIN cadcli c ON c.cliente = e.cliente
                        WHERE e.filial = @filial AND e.pedido_venda = 0 AND e.perdido = 'N'
                              AND e.orcamento > @orcMin
                        ORDER BY e.orcamento DESC";
                    cmd.Parameters.AddWithValue("filial", config.Filial);
                    cmd.Parameters.AddWithValue("orcMin", config.OrcamentoMinimo);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var orc = reader.GetInt32(0);
                        if (_ignorados.Contains(orc) || _aprovados.Contains(orc)) continue;

                        result.Add(new OrcamentoResumo
                        {
                            Orcamento = orc,
                            Nome = await reader.IsDBNullAsync(1) ? "" : reader.GetString(1),
                            NomeFantasia = await reader.IsDBNullAsync(2) ? "" : reader.GetString(2)
                        });
                    }
                    return result;
                });

                var dados = lista ?? [];

                // Suprimir repaints intermediários durante a atualização
                dgvOrcamentos.SuspendLayout();
                dgvOrcamentos.SkipAutoAdjust = true;
                try
                {
                    dgvOrcamentos.DataSource = new SortableBindingList<OrcamentoResumo>(dados);
                    lblContagem.Text = $"{dados.Count} registro{(dados.Count != 1 ? "s" : "")}";
                    DataGridViewSortHelper.RestoreSort(dgvOrcamentos, sortCol, sortDir);

                    // Restaurar seleção e rolagem
                    if (orcSelecionado > 0)
                    {
                        for (int i = 0; i < dgvOrcamentos.Rows.Count; i++)
                        {
                            if (dgvOrcamentos.Rows[i].DataBoundItem is OrcamentoResumo o && o.Orcamento == orcSelecionado)
                            {
                                dgvOrcamentos.ClearSelection();
                                dgvOrcamentos.Rows[i].Selected = true;
                                dgvOrcamentos.CurrentCell = dgvOrcamentos.Rows[i].Cells[0];
                                break;
                            }
                        }
                    }

                    if (firstDisplayedRowIndex >= 0 && firstDisplayedRowIndex < dgvOrcamentos.RowCount)
                    {
                        dgvOrcamentos.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;
                    }
                }
                finally
                {
                    dgvOrcamentos.SkipAutoAdjust = false;
                    dgvOrcamentos.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                dgvOrcamentos.DataSource = null;
                lblContagem.Text = "";
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar orçamentos: {ex}");
            }
        }

        private async Task CarregarAprovadosAsync()
        {
            if (!_db.ControleConectado) return;

            try
            {
                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var ids = new HashSet<int>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT orcamento FROM pedidos_controlados";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                        ids.Add(reader.GetInt32(0));
                    return ids;
                });

                _aprovados = lista ?? [];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar orçamentos aprovados: {ex}");
                _aprovados = [];
            }
        }

        private async Task CarregarPedidosAsync()
        {
            if (!_db.ControleConectado) return;

            try
            {
                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<PedidoControlado>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT orcamento, cliente, nome_fantasia, rota,
                               aprovado, em_separacao, conferido, liberado, travado, em_faturamento
                        FROM pedidos_controlados
                        ORDER BY orcamento DESC";
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new PedidoControlado
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

                var dados = lista ?? [];

                // Buscar pedido_venda do integra para cada pedido
                if (dados.Count > 0 && _db.IntegraConectado)
                {
                    try
                    {
                        var config = _db.Config;
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

                // Se já existe uma lista no grid, atualizar in-place (preserva sort, seleção, scroll)
                if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaExistente)
                {
                    listaExistente.AtualizarInPlace(dados, p => p.Orcamento, (src, dst) => dst.CopiarDe(src));
                    lblContagemPedidos.Text = $"{listaExistente.Count} registro{(listaExistente.Count != 1 ? "s" : "")}";
                }
                else
                {
                    // Primeira carga: criar novo DataSource
                    dgvPedidos.DataSource = new SortableBindingList<PedidoControlado>(dados);
                    lblContagemPedidos.Text = $"{dados.Count} registro{(dados.Count != 1 ? "s" : "")}";
                }
            }
            catch (Exception ex)
            {
                dgvPedidos.DataSource = null;
                lblContagemPedidos.Text = "";
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar pedidos: {ex}");
            }
        }

        private async Task RecarregarDadosAsync(bool recarregarOrcamentos = true, bool recarregarPedidos = true)
        {
            if (_recarregando) return;
            _recarregando = true;
            
            // TODO: Considerar desabilitar botões ou mostrar um indicador de loading

            try
            {
                var tasks = new List<Task>();
                if (recarregarOrcamentos) tasks.Add(CarregarOrcamentosAsync());
                if (recarregarPedidos) tasks.Add(CarregarPedidosAsync());

                await Task.WhenAll(tasks);
            }
            finally
            {
                _recarregando = false;
            }
        }

        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            await RecarregarDadosAsync();
        }

        private async void BtnAprovar_Click(object? sender, EventArgs e)
        {
            if (dgvOrcamentos.CurrentRow?.DataBoundItem is not OrcamentoResumo orc) return;
            if (!_db.ControleConectado) return;

            using var dialog = new SelecionarRotaDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (string.IsNullOrWhiteSpace(dialog.RotaSelecionada))
            {
                MessageBox.Show(this, "Selecione uma rota.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO pedidos_controlados (orcamento, cliente, nome_fantasia, rota, aprovado)
                        VALUES (@orc, @cliente, @fantasia, @rota, FALSE)
                        ON CONFLICT (orcamento) DO NOTHING";
                    cmd.Parameters.AddWithValue("orc", orc.Orcamento);
                    cmd.Parameters.AddWithValue("cliente", orc.Nome);
                    cmd.Parameters.AddWithValue("fantasia", orc.NomeFantasia);
                    cmd.Parameters.AddWithValue("rota", dialog.RotaSelecionada);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    // Atualiza o estado local e as grids manualmente para evitar um refresh completo
                    _aprovados.Add(orc.Orcamento);

                    // Remove do grid de orçamentos
                    if (dgvOrcamentos.DataSource is SortableBindingList<OrcamentoResumo> listaOrcamentos)
                    {
                        listaOrcamentos.Remove(orc);
                        lblContagem.Text = $"{listaOrcamentos.Count} registro{(listaOrcamentos.Count != 1 ? "s" : "")}";
                    }

                    // Adiciona no grid de pedidos
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        var novoPedido = new PedidoControlado
                        {
                            Orcamento = orc.Orcamento,
                            Cliente = orc.Nome,
                            NomeFantasia = orc.NomeFantasia,
                            Rota = dialog.RotaSelecionada
                        };
                        listaPedidos.Insert(0, novoPedido); // Insere no topo para manter a ordem decrescente
                        lblContagemPedidos.Text = $"{listaPedidos.Count} registro{(listaPedidos.Count != 1 ? "s" : "")}";
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este orçamento já foi aprovado por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao aprovar pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnIgnorar_Click(object? sender, EventArgs e)
        {
            if (dgvOrcamentos.CurrentRow?.DataBoundItem is not OrcamentoResumo orc) return;
            if (!_db.ControleConectado) return;

            var confirm = MessageBox.Show(this,
                $"Ignorar o orçamento {orc.Orcamento} ({orc.Nome})?\n\nEle não aparecerá mais na lista de pendentes.",
                "Confirmar",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                await _db.ExecutarControleAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO orcamentos_ignorados (orcamento)
                        VALUES (@orc)
                        ON CONFLICT (orcamento) DO NOTHING";
                    cmd.Parameters.AddWithValue("orc", orc.Orcamento);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                // Atualiza o estado local e a grid manualmente
                _ignorados.Add(orc.Orcamento);
                if (dgvOrcamentos.DataSource is SortableBindingList<OrcamentoResumo> listaOrcamentos)
                {
                    listaOrcamentos.Remove(orc);
                    lblContagem.Text = $"{listaOrcamentos.Count} registro{(listaOrcamentos.Count != 1 ? "s" : "")}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao ignorar orçamento: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnVerIgnorados_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado || !_db.IntegraConectado)
            {
                MessageBox.Show(this, "Sem conexão com os bancos.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new IgnoradosForm();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await RecarregarDadosAsync(recarregarPedidos: false);
            }
        }

        private async void Db_StatusAlterado(object? sender, StatusConexaoEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(() => Db_StatusAlterado(sender, e));
                return;
            }
            
            if (e.StatusIntegra == StatusConexao.Conectado && e.StatusControle == StatusConexao.Conectado && !_dadosIniciaisCarregados)
            {
                _dadosIniciaisCarregados = true;
                _ = _db.GarantirTabelasAsync();
                await RecarregarDadosAsync();
                timerAutoRefresh.Start();
            }
            else if (e.StatusControle == StatusConexao.Conectado && _dadosIniciaisCarregados)
            {
                _ = _db.GarantirTabelasAsync();
                await CarregarPedidosAsync();
            }

            // Parar auto-refresh se desconectou
            if (e.StatusIntegra == StatusConexao.Desconectado)
                timerAutoRefresh.Stop();

            StatusConexao pior;
            if (e.StatusIntegra == StatusConexao.Desconectado || e.StatusControle == StatusConexao.Desconectado)
                pior = StatusConexao.Desconectado;
            else if (e.StatusIntegra == StatusConexao.Verificando || e.StatusControle == StatusConexao.Verificando)
                pior = StatusConexao.Verificando;
            else
                pior = StatusConexao.Conectado;

            var (corTexto, corIndicador) = pior switch
            {
                StatusConexao.Conectado => (Theme.Success, Theme.Success),
                StatusConexao.Desconectado => (Theme.Error, Theme.Error),
                _ => (Theme.Warning, Theme.Warning)
            };

            lblStatusConexao.Text = e.Mensagem;
            lblStatusConexao.ForeColor = corTexto;
            panelIndicador.BackColor = corIndicador;
            panelIndicador.Invalidate();
        }

        private async void BtnAlterarRota_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            using var dialog = new SelecionarRotaDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (string.IsNullOrWhiteSpace(dialog.RotaSelecionada))
            {
                MessageBox.Show(this, "Selecione uma rota.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE pedidos_controlados SET rota = @rota WHERE orcamento = @orc AND rota = @rotaAnterior";
                    cmd.Parameters.AddWithValue("rota", dialog.RotaSelecionada);
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    cmd.Parameters.AddWithValue("rotaAnterior", pedido.Rota);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    // Atualiza o objeto na lista e notifica a grid
                    pedido.Rota = dialog.RotaSelecionada;
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este pedido foi alterado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao alterar rota: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnExcluirPedido_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            var confirm = MessageBox.Show(this,
                $"ATENÇÃO: Esta ação é irreversível!\n\nExcluir o pedido {pedido.Orcamento} ({pedido.Cliente}) do controle?\n\nO orçamento voltará para a lista de pendentes.",
                "Excluir Pedido",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

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
                {
                    // Remove o item da grid de pedidos e recarrega a de orçamentos
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.Remove(pedido);
                        lblContagemPedidos.Text = $"{listaPedidos.Count} registro{(listaPedidos.Count != 1 ? "s" : "")}";
                    }
                    await CarregarOrcamentosAsync();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao excluir pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnTravar_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (pedido.Travado)
            {
                MessageBox.Show(this, "Este pedido já está travado.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido está em faturamento. Não é possível travá-lo.\nO operador responsável deve largá-lo primeiro.",
                    "Pedido em Faturamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE pedidos_controlados SET travado = TRUE WHERE orcamento = @orc AND travado = FALSE AND em_faturamento = FALSE";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    pedido.Travado = true;
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi travado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao travar pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDestravar_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (!pedido.Travado)
            {
                MessageBox.Show(this, "Este pedido não está travado.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido está em faturamento. Não é possível destravá-lo.\nO operador responsável deve largá-lo primeiro.",
                    "Pedido em Faturamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE pedidos_controlados SET travado = FALSE WHERE orcamento = @orc AND travado = TRUE AND em_faturamento = FALSE";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    pedido.Travado = false;
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi destravado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao destravar pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAvancarStatus_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (pedido.Travado)
            {
                MessageBox.Show(this, "Este pedido está travado. Destrave-o antes de alterar o status.",
                    "Pedido Travado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido está em faturamento. Não é possível alterar o status.\nO operador responsável deve largá-lo primeiro.",
                    "Pedido em Faturamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? coluna = ObterProximoStatus(pedido);
            if (coluna == null)
            {
                MessageBox.Show(this, "Este pedido já está no status final (Liberado).",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
                        UPDATE pedidos_controlados SET {coluna} = TRUE
                        WHERE orcamento = @orc AND travado = FALSE AND em_faturamento = FALSE
                          AND aprovado = @aprov AND em_separacao = @em_sep AND conferido = @conf AND liberado = @lib";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    cmd.Parameters.AddWithValue("aprov", pedido.Aprovado);
                    cmd.Parameters.AddWithValue("em_sep", pedido.EmSeparacao);
                    cmd.Parameters.AddWithValue("conf", pedido.Conferido);
                    cmd.Parameters.AddWithValue("lib", pedido.Liberado);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    // Atualiza o objeto na lista e notifica a grid
                    switch (coluna)
                    {
                        case "aprovado": pedido.Aprovado = true; break;
                        case "em_separacao": pedido.EmSeparacao = true; break;
                        case "conferido": pedido.Conferido = true; break;
                        case "liberado": pedido.Liberado = true; break;
                    }
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "O status deste pedido foi alterado por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao avançar status: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnRetornarStatus_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (pedido.Travado)
            {
                MessageBox.Show(this, "Este pedido está travado. Destrave-o antes de alterar o status.",
                    "Pedido Travado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido está em faturamento. Não é possível alterar o status.\nO operador responsável deve largá-lo primeiro.",
                    "Pedido em Faturamento", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? coluna = ObterStatusAtual(pedido);
            if (coluna == null)
            {
                MessageBox.Show(this, "Este pedido já está no status inicial (Não Aprovado).",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $@"
                        UPDATE pedidos_controlados SET {coluna} = FALSE
                        WHERE orcamento = @orc AND travado = FALSE AND em_faturamento = FALSE
                          AND aprovado = @aprov AND em_separacao = @em_sep AND conferido = @conf AND liberado = @lib";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    cmd.Parameters.AddWithValue("aprov", pedido.Aprovado);
                    cmd.Parameters.AddWithValue("em_sep", pedido.EmSeparacao);
                    cmd.Parameters.AddWithValue("conf", pedido.Conferido);
                    cmd.Parameters.AddWithValue("lib", pedido.Liberado);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    // Atualiza o objeto na lista e notifica a grid
                    switch (coluna)
                    {
                        case "liberado": pedido.Liberado = false; break;
                        case "conferido": pedido.Conferido = false; break;
                        case "em_separacao": pedido.EmSeparacao = false; break;
                        case "aprovado": pedido.Aprovado = false; break;
                    }
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "O status deste pedido foi alterado por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao retornar status: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnAssumir_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido já está em faturamento.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (pedido.PedidoVenda > 0)
            {
                MessageBox.Show(this, "Este pedido já foi faturado.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!pedido.EmSeparacao || !pedido.Conferido || !pedido.Liberado)
            {
                MessageBox.Show(this, "Este pedido ainda não cumpriu todas as etapas anteriores.\nÉ necessário que esteja Em Separação, Conferido e Liberado.",
                    "Etapas Pendentes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE pedidos_controlados SET em_faturamento = TRUE WHERE orcamento = @orc AND em_faturamento = FALSE AND em_separacao = TRUE AND conferido = TRUE AND liberado = TRUE";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    pedido.EmFaturamento = true;
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi assumido por outro operador.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao assumir pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnLargar_Click(object? sender, EventArgs e)
        {
            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            if (!pedido.EmFaturamento)
            {
                MessageBox.Show(this, "Este pedido não está em faturamento.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE pedidos_controlados SET em_faturamento = FALSE WHERE orcamento = @orc AND em_faturamento = TRUE";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    pedido.EmFaturamento = false;
                    if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> listaPedidos)
                    {
                        listaPedidos.ResetItem(listaPedidos.IndexOf(pedido));
                    }
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi largado por outro operador.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await CarregarPedidosAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao largar pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Retorna a coluna do próximo status a ser ativado, ou null se já está no final
        private static string? ObterProximoStatus(PedidoControlado p)
        {
            if (!p.Aprovado) return "aprovado";
            if (!p.EmSeparacao) return "em_separacao";
            if (!p.Conferido) return "conferido";
            if (!p.Liberado) return "liberado";
            return null;
        }

        // Retorna a coluna do status atual (mais avançado ativo), ou null se está no inicial (não aprovado)
        private static string? ObterStatusAtual(PedidoControlado p)
        {
            if (p.Liberado) return "liberado";
            if (p.Conferido) return "conferido";
            if (p.EmSeparacao) return "em_separacao";
            if (p.Aprovado) return "aprovado";
            return null;
        }

        private int _orcamentoDetalheAtual;

        private void DgvSelectionChanged(object? sender, EventArgs e)
        {
            int? orcamento = null;

            if (sender == dgvOrcamentos && dgvOrcamentos.CurrentRow?.DataBoundItem is OrcamentoResumo orc)
                orcamento = orc.Orcamento;
            else if (sender == dgvPedidos && dgvPedidos.CurrentRow?.DataBoundItem is PedidoControlado ped)
                orcamento = ped.Orcamento;

            if (orcamento == null || orcamento == _orcamentoDetalheAtual) return;
            _orcamentoDetalheAtual = orcamento.Value;
            CarregarDetalhes(orcamento.Value);
        }

        private async void CarregarDetalhes(int orcamento)
        {
            if (!_db.IntegraConectado) return;

            try
            {
                var det = await _db.ConsultarIntegraAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        SELECT e.cidade, e.estado, e.vr_total,
                               e.texto01, e.texto02, e.texto03, e.texto04,
                               e.observacao, COALESCE(f.nome, '') AS vendedor_nome
                        FROM estorc e
                        LEFT JOIN cadfun f ON f.funcionario = e.vendedor
                        WHERE e.filial = @filial AND e.orcamento = @orc";
                    cmd.Parameters.AddWithValue("filial", _db.Config.Filial);
                    cmd.Parameters.AddWithValue("orc", orcamento);

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    if (!await reader.ReadAsync().ConfigureAwait(false))
                        return (DetalhesOrcamento?)null;

                    return new DetalhesOrcamento
                    {
                        Cidade = await reader.IsDBNullAsync(0) ? "" : reader.GetString(0),
                        Estado = await reader.IsDBNullAsync(1) ? "" : reader.GetString(1),
                        ValorTotal = await reader.IsDBNullAsync(2) ? 0m : reader.GetDecimal(2),
                        Texto01 = await reader.IsDBNullAsync(3) ? "" : reader.GetString(3),
                        Texto02 = await reader.IsDBNullAsync(4) ? "" : reader.GetString(4),
                        Texto03 = await reader.IsDBNullAsync(5) ? "" : reader.GetString(5),
                        Texto04 = await reader.IsDBNullAsync(6) ? "" : reader.GetString(6),
                        Observacao = await reader.IsDBNullAsync(7) ? "" : reader.GetString(7),
                        Vendedor = reader.GetString(8)
                    };
                });

                if (_orcamentoDetalheAtual != orcamento) return;

                if (det != null)
                {
                    lblDetCidade.Text = string.IsNullOrWhiteSpace(det.Cidade) ? "—" : det.Cidade;
                    lblDetEstado.Text = string.IsNullOrWhiteSpace(det.Estado) ? "—" : det.Estado;
                    lblDetValorTotal.Text = det.ValorTotal.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
                    lblDetTexto01.Text = string.IsNullOrWhiteSpace(det.Texto01) ? "—" : det.Texto01;
                    lblDetTexto02.Text = string.IsNullOrWhiteSpace(det.Texto02) ? "—" : det.Texto02;
                    lblDetTexto03.Text = string.IsNullOrWhiteSpace(det.Texto03) ? "—" : $"{det.Texto03} VOLUMES";
                    lblDetTexto04.Text = string.IsNullOrWhiteSpace(det.Texto04) ? "—" : $"{det.Texto04} KG";
                    lblDetObservacao.Text = string.IsNullOrWhiteSpace(det.Observacao) ? "—" : det.Observacao;
                    lblDetVendedor.Text = string.IsNullOrWhiteSpace(det.Vendedor) ? "—" : det.Vendedor;
                }
                else
                {
                    LimparDetalhes();
                }
            }
            catch (Exception ex)
            {
                LimparDetalhes();
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar detalhes do orçamento {orcamento}: {ex}");
            }
        }

        private void LimparDetalhes()
        {
            lblDetCidade.Text = "—";
            lblDetEstado.Text = "—";
            lblDetValorTotal.Text = "—";
            lblDetTexto01.Text = "—";
            lblDetTexto02.Text = "—";
            lblDetTexto03.Text = "—";
            lblDetTexto04.Text = "—";
            lblDetObservacao.Text = "—";
            lblDetVendedor.Text = "—";
            _orcamentoDetalheAtual = 0;
        }

        private void MenuCadastroRotas_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado)
            {
                MessageBox.Show(this, "Sem conexão com o banco de controle.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new RotasForm();
            form.ShowDialog(this);
        }

        private async void MenuExclusaoMassa_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado)
            {
                MessageBox.Show(this, "Sem conexão com o banco de controle.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new ExclusaoMassaForm();
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                await RecarregarDadosAsync();
            }
        }

        private void MenuRelatorioCarga_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado)
            {
                MessageBox.Show(this, "Sem conexão com o banco de controle.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new RelatorioCargaForm();
            form.ShowDialog(this);
        }

        private void MenuRelatorioNfe_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado)
            {
                MessageBox.Show(this, "Sem conexão com o banco de controle.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new RelatorioNfeForm();
            form.ShowDialog(this);
        }

        private void BtnConfig_Click(object? sender, EventArgs e)
        {
            using var dialog = new ConfigDialog(_db.Config);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _db.AtualizarConfig(dialog.Config);
                _dadosIniciaisCarregados = false; // Força recarregamento na próxima verificação de conexão
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _db.Dispose();
            base.OnFormClosed(e);
        }
    }

    public class OrcamentoResumo
    {
        public int Orcamento { get; set; }
        public string Nome { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
    }

    public class PedidoControlado
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

        public string AprovadoTexto => Travado ? "TRAVADO" : (Aprovado ? "SIM" : "NÃO");
        public string EmSeparacaoTexto => Travado ? "TRAVADO" : (EmSeparacao ? "SIM" : "NÃO");
        public string ConferidoTexto => Travado ? "TRAVADO" : (Conferido ? "SIM" : "NÃO");
        public string LiberadoTexto => Travado ? "TRAVADO" : (Liberado ? "SIM" : "NÃO");
        public string FaturadoTexto => PedidoVenda > 0 ? PedidoVenda.ToString() : (EmFaturamento ? "EM FATURAMENTO" : "NÃO");

        public void CopiarDe(PedidoControlado outro)
        {
            Cliente = outro.Cliente;
            NomeFantasia = outro.NomeFantasia;
            Rota = outro.Rota;
            Aprovado = outro.Aprovado;
            EmSeparacao = outro.EmSeparacao;
            Conferido = outro.Conferido;
            Liberado = outro.Liberado;
            Travado = outro.Travado;
            EmFaturamento = outro.EmFaturamento;
            PedidoVenda = outro.PedidoVenda;
        }
    }

    public class OptimizedDataGridView : DataGridView
    {
        /// <summary>Quando true, o handler DataSourceChanged do Theme não chama AutoAjustarColunas.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.Browsable(false)]
        public bool SkipAutoAdjust { get; set; }

        public OptimizedDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    public class DetalhesOrcamento
    {
        public string Cidade { get; set; } = "";
        public string Estado { get; set; } = "";
        public decimal ValorTotal { get; set; }
        public string Texto01 { get; set; } = "";
        public string Texto02 { get; set; } = "";
        public string Texto03 { get; set; } = "";
        public string Texto04 { get; set; } = "";
        public string Observacao { get; set; } = "";
        public string Vendedor { get; set; } = "";
    }

    public class SortableBindingList<T> : BindingList<T>
    {
        private bool _isSorted;
        private PropertyDescriptor? _sortProperty;
        private ListSortDirection _sortDirection;

        public SortableBindingList() { }
        public SortableBindingList(IList<T> list) : base(list) { }

        protected override bool SupportsSortingCore => true;
        protected override bool IsSortedCore => _isSorted;
        protected override PropertyDescriptor? SortPropertyCore => _sortProperty;
        protected override ListSortDirection SortDirectionCore => _sortDirection;

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            _sortProperty = prop;
            _sortDirection = direction;

            if (Items is List<T> list)
            {
                // OrderBy é estável: itens com valores iguais mantêm sua ordem relativa
                var comparer = Comparer<object?>.Create((va, vb) =>
                {
                    if (va == null && vb == null) return 0;
                    if (va == null) return -1;
                    if (vb == null) return 1;
                    if (va is IComparable ca) return ca.CompareTo(vb);
                    return string.Compare(va.ToString(), vb.ToString(), StringComparison.OrdinalIgnoreCase);
                });

                var sorted = direction == ListSortDirection.Ascending
                    ? list.OrderBy(x => prop.GetValue(x), comparer).ToList()
                    : list.OrderByDescending(x => prop.GetValue(x), comparer).ToList();

                list.Clear();
                list.AddRange(sorted);
            }

            _isSorted = true;
            ResetBindings();
        }

        /// <summary>
        /// Atualiza os dados in-place sem trocar o DataSource.
        /// Usa a chave para identificar itens, atualiza existentes, adiciona novos e remove ausentes.
        /// Se houver sort ativo, reaplica ao final. O grid não sofre mutações visuais.
        /// </summary>
        public void AtualizarInPlace(IList<T> novos, Func<T, int> keySelector, Action<T, T> copiar)
        {
            RaiseListChangedEvents = false;
            try
            {
                var novosPorChave = novos.ToDictionary(keySelector);
                var existentesPorChave = new Dictionary<int, T>();

                // Atualizar existentes e remover ausentes
                for (int i = Count - 1; i >= 0; i--)
                {
                    var chave = keySelector(this[i]);
                    if (novosPorChave.TryGetValue(chave, out var atualizado))
                    {
                        copiar(atualizado, this[i]);
                        existentesPorChave[chave] = this[i];
                    }
                    else
                    {
                        RemoveAt(i);
                    }
                }

                // Adicionar novos
                foreach (var novo in novos)
                {
                    if (!existentesPorChave.ContainsKey(keySelector(novo)))
                        Add(novo);
                }

                // Reaplicar sort se houver
                if (_isSorted && _sortProperty != null)
                    ApplySortCore(_sortProperty, _sortDirection);
            }
            finally
            {
                RaiseListChangedEvents = true;
                ResetBindings();
            }
        }

        protected override void RemoveSortCore()
        {
            _isSorted = false;
            _sortProperty = null;
        }
    }

    static class DataGridViewSortHelper
    {
        public static (string? columnName, ListSortDirection direction) SaveSort(DataGridView dgv)
        {
            if (dgv.SortedColumn != null && dgv.SortOrder != SortOrder.None)
            {
                var dir = dgv.SortOrder == SortOrder.Ascending
                    ? ListSortDirection.Ascending
                    : ListSortDirection.Descending;
                return (dgv.SortedColumn.Name, dir);
            }
            return (null, default);
        }

        public static void RestoreSort(DataGridView dgv, string? columnName, ListSortDirection direction)
        {
            if (columnName != null && dgv.Columns.Contains(columnName) && dgv.Columns[columnName] is DataGridViewColumn col)
            {
                dgv.Sort(col, direction);
            }
        }
    }
}
