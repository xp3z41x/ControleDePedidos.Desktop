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
        /// <summary>Suprime DgvSelectionChanged enquanto o código mexe na seleção (refresh).</summary>
        private bool _suprimirSelectionChanged;
        /// <summary>Assinatura do último conjunto de orçamentos pendentes já refletido na tela.</summary>
        private string _assinaturaOrcamentos = "";
        /// <summary>Assinatura do último conjunto de pedidos controlados já refletido na tela.</summary>
        private string _assinaturaPedidos = "";
        /// <summary>Conjuntos já publicados nos dropdowns de filtro (evita recriar itens à toa).</summary>
        private string _assinaturaDropdowns = "";

        // Master list e estado dos filtros (persistidos via FilterStore)
        private List<PedidoControlado> _pedidosTodos = [];
        private HashSet<string> _filtroVendedores = new(StringComparer.CurrentCultureIgnoreCase);
        private HashSet<string> _filtroRotas = new(StringComparer.CurrentCultureIgnoreCase);
        private HashSet<EtapaPedido> _filtroEtapas = [];

        private const string SemVendedor = "(Sem vendedor)";
        private const string SemRota = "(Sem rota)";

        public Form1()
        {
            InitializeComponent();
            _db = DatabaseService.Instancia;
            _db.StatusAlterado += Db_StatusAlterado;
            _db.MudancaNotificada += Db_MudancaNotificada;
            RestaurarFiltrosDaPersistencia();
            WireDropDownMacros();
            AtualizarIndicadoresVisuais();

            // Cadência adaptativa: reprogramar quando o foco ou o estado da janela muda
            Activated += (_, _) => AgendarProximoRefresh();
            Deactivate += (_, _) => AgendarProximoRefresh();
            Resize += (_, _) => AgendarProximoRefresh();
            // SessionSwitch é estático: precisa ser desanexado no fechamento (ver OnFormClosed)
            Microsoft.Win32.SystemEvents.SessionSwitch += SessionSwitchHandler;
        }

        private void RestaurarFiltrosDaPersistencia()
        {
            foreach (var v in FilterStore.Vendedores) _filtroVendedores.Add(v);
            foreach (var r in FilterStore.Rotas) _filtroRotas.Add(r);
            foreach (var e in FilterStore.Etapas)
            {
                if (Enum.TryParse<EtapaPedido>(e, out var etapa))
                    _filtroEtapas.Add(etapa);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke(() => _db.Iniciar(15000));
        }

        // ==================================================================
        // CADÊNCIA DO AUTO-REFRESH
        // Base curta (15s) para a tela ficar quase ao vivo, mas com três freios que
        // mantêm a carga baixa com 10+ sessões no mesmo RDS:
        //   - jitter: as instâncias sobem juntas no logon em massa e os timers alinham,
        //     criando picos (medido: 257ms de latência sincronizado × 4ms escalonado);
        //   - backoff por foco/minimização: no RDS há muitas sessões abertas e poucas
        //     em uso ativo;
        //   - pausa total com a sessão RDP desconectada ou bloqueada — nesse caso o
        //     Windows NÃO dispara Deactivate, então é preciso ouvir SessionSwitch.
        // O custo real do ciclo é o rebind do grid (~45ms), não a query (~1,7ms); por
        // isso o dirty check em memória é o que de fato protege o servidor.
        // ==================================================================
        private const int RefreshBaseMs = 15000;      // janela em foco
        private const int RefreshSemFocoMs = 30000;   // aberta, sem foco
        private const int RefreshMinimizadoMs = 120000;
        private bool _sessaoAtiva = true;             // false = RDP desconectado ou bloqueado
        private bool _mudancaPendente;                // notificação recebida aguardando coalescing

        private int IntervaloRefreshAlvo()
        {
            if (WindowState == FormWindowState.Minimized) return RefreshMinimizadoMs;
            return Form.ActiveForm == this || ContainsFocus ? RefreshBaseMs : RefreshSemFocoMs;
        }

        /// <summary>Reprograma o timer com jitter, respeitando foco e estado da sessão.</summary>
        private void AgendarProximoRefresh()
        {
            if (!_dadosIniciaisCarregados) return;

            if (!_sessaoAtiva)
            {
                timerAutoRefresh.Stop();
                return;
            }

            int alvo = IntervaloRefreshAlvo();
            // Jitter de até 25%: dessincroniza as instâncias que subiram juntas.
            timerAutoRefresh.Interval = alvo + Random.Shared.Next(0, alvo / 4);
            timerAutoRefresh.Start();
        }

        private void SessionSwitchHandler(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            void Aplicar()
            {
                switch (e.Reason)
                {
                    case Microsoft.Win32.SessionSwitchReason.RemoteDisconnect:
                    case Microsoft.Win32.SessionSwitchReason.SessionLock:
                    case Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect:
                        _sessaoAtiva = false;
                        timerAutoRefresh.Stop();
                        Logger.Info($"Sessão inativa ({e.Reason}) — auto-refresh pausado.");
                        break;

                    case Microsoft.Win32.SessionSwitchReason.RemoteConnect:
                    case Microsoft.Win32.SessionSwitchReason.SessionUnlock:
                    case Microsoft.Win32.SessionSwitchReason.ConsoleConnect:
                        _sessaoAtiva = true;
                        Logger.Info($"Sessão reativada ({e.Reason}) — recarregando.");
                        AgendarProximoRefresh();
                        _ = RecarregarDadosAsync();   // pega tudo que mudou durante a ausência
                        break;
                }
            }

            if (InvokeRequired) BeginInvoke(Aplicar);
            else Aplicar();
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
                Logger.Error("Erro ao carregar orçamentos ignorados", ex);
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

                // Dirty check (mesma razão do grid de pedidos): não pagar rebind quando
                // o conteúdo é idêntico ao que já está na tela.
                string assinatura = string.Join("\n", dados.Select(o =>
                    $"{o.Orcamento}\u001F{o.Nome}\u001F{o.NomeFantasia}"));
                if (assinatura == _assinaturaOrcamentos && dgvOrcamentos.DataSource != null)
                    return;
                _assinaturaOrcamentos = assinatura;

                // Suprimir repaints intermediários durante a atualização
                dgvOrcamentos.SuspendLayout();
                dgvOrcamentos.SkipAutoAdjust = true;
                _suprimirSelectionChanged = true;
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
                    _suprimirSelectionChanged = false;
                    dgvOrcamentos.SkipAutoAdjust = false;
                    dgvOrcamentos.ResumeLayout(true);
                }
            }
            catch (Exception ex)
            {
                _assinaturaOrcamentos = "";
                dgvOrcamentos.DataSource = null;
                lblContagem.Text = "";
                Logger.Error("Erro ao carregar orçamentos", ex);
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
                Logger.Error("Erro ao carregar orçamentos aprovados", ex);
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

                // Buscar pedido_venda + vendedor do integra para cada pedido
                bool enriquecido = false;
                if (dados.Count > 0 && _db.IntegraConectado)
                {
                    try
                    {
                        var config = _db.Config;
                        var enriquecimento = await _db.ConsultarIntegraAsync(async conn =>
                        {
                            var dict = new Dictionary<int, (int pedidoVenda, string vendedor, bool conferidoIntegra)>();
                            using var cmd = conn.CreateCommand();
                            // '= ANY(@ids)' em vez de 'IN (@p0..@pN)': com o IN dinâmico o
                            // TEXTO da query mudava a cada variação na quantidade de pedidos,
                            // forçando o servidor a replanejar toda vez (planejamento medido
                            // em ~2,6ms contra 0,4ms de execução — 7x mais caro que executar).
                            cmd.CommandText = @"
                                SELECT e.orcamento, e.pedido_venda, COALESCE(f.nome, '') AS vendedor,
                                       COALESCE(e.status_conferencia, '') AS status_conf
                                FROM estorc e
                                LEFT JOIN cadfun f ON f.funcionario = e.vendedor
                                WHERE e.filial = @filial AND e.orcamento = ANY(@ids)";
                            cmd.Parameters.AddWithValue("filial", config.Filial);
                            cmd.Parameters.AddWithValue("ids", dados.Select(d => d.Orcamento).ToArray());
                            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                int orc = reader.GetInt32(0);
                                int pv = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? 0 : reader.GetInt32(1);
                                string vend = await reader.IsDBNullAsync(2).ConfigureAwait(false) ? "" : reader.GetString(2);
                                string statusConf = await reader.IsDBNullAsync(3).ConfigureAwait(false) ? "" : reader.GetString(3);
                                dict[orc] = (pv, vend, statusConf.Trim() == "S");
                            }
                            return dict;
                        });

                        if (enriquecimento != null)
                        {
                            foreach (var p in dados)
                            {
                                if (enriquecimento.TryGetValue(p.Orcamento, out var info))
                                {
                                    p.PedidoVenda = info.pedidoVenda;
                                    p.Vendedor = info.vendedor;
                                    p.ConferidoIntegra = info.conferidoIntegra;
                                }
                            }
                            enriquecido = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Erro ao buscar pedido_venda/vendedor", ex);
                    }
                }

                // Integra fora/falhou: manter o último enriquecimento conhecido. Sem isso o
                // CopiarDe zerava PedidoVenda/Vendedor/ConferidoIntegra — faturados viravam
                // "NÃO", o filtro por vendedor esvaziava o grid, e com ConferidoIntegra
                // falso o Avançar gravava conferência MANUAL num pedido que o Integra já
                // conferiu.
                if (!enriquecido && dados.Count > 0 && _pedidosTodos.Count > 0)
                {
                    var anteriores = _pedidosTodos.ToDictionary(p => p.Orcamento);
                    foreach (var p in dados)
                    {
                        if (!anteriores.TryGetValue(p.Orcamento, out var ant)) continue;
                        p.PedidoVenda = ant.PedidoVenda;
                        p.Vendedor = ant.Vendedor;
                        p.ConferidoIntegra = ant.ConferidoIntegra;
                    }
                }

                // Dirty check: rebind + repaint do grid custa ~45ms e é o item mais caro
                // do ciclo (×10 sessões no RDS). Como a tela muda poucas vezes por hora,
                // comparar em memória antes evita pagar isso à toa em ~97% dos refreshes.
                string assinatura = AssinaturaPedidos(dados);
                if (assinatura == _assinaturaPedidos && dgvPedidos.DataSource != null)
                    return;
                _assinaturaPedidos = assinatura;

                // Atualizar master list preservando referências existentes quando possível
                AtualizarMasterList(dados);

                // Repovoar dropdowns de filtro e aplicar filtros sobre a nova master
                AtualizarOpcoesDropdowns();
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                _pedidosTodos = [];
                _assinaturaPedidos = "";
                dgvPedidos.DataSource = null;
                lblContagemPedidos.Text = "";
                Logger.Error("Erro ao carregar pedidos", ex);
            }
        }

        /// <summary>
        /// Assinatura de todo o conteúdo exibido do grid de pedidos. Precisa incluir
        /// TODOS os campos visíveis — um campo de fora vira mudança que a tela nunca mostra.
        /// </summary>
        private static string AssinaturaPedidos(List<PedidoControlado> lista)
        {
            const char sep = '\u001F';   // unit separator: nunca aparece nos dados
            var sb = new System.Text.StringBuilder(lista.Count * 48);
            foreach (var p in lista)
            {
                sb.Append(p.Orcamento).Append(sep).Append(p.Cliente).Append(sep)
                  .Append(p.NomeFantasia).Append(sep).Append(p.Rota).Append(sep)
                  .Append(p.Vendedor).Append(sep).Append(p.PedidoVenda).Append(sep)
                  .Append(p.Aprovado ? '1' : '0').Append(p.EmSeparacao ? '1' : '0')
                  .Append(p.Conferido ? '1' : '0').Append(p.Liberado ? '1' : '0')
                  .Append(p.Travado ? '1' : '0').Append(p.EmFaturamento ? '1' : '0')
                  .Append(p.ConferidoIntegra ? '1' : '0').Append('\n');
            }
            return sb.ToString();
        }

        private void AtualizarMasterList(List<PedidoControlado> novos)
        {
            // Preserva referências de objetos existentes (copia campos) para manter o binding
            var existentesPorOrc = _pedidosTodos.ToDictionary(p => p.Orcamento);
            var resultado = new List<PedidoControlado>(novos.Count);
            foreach (var novo in novos)
            {
                if (existentesPorOrc.TryGetValue(novo.Orcamento, out var existente))
                {
                    existente.CopiarDe(novo);
                    resultado.Add(existente);
                }
                else
                {
                    resultado.Add(novo);
                }
            }
            _pedidosTodos = resultado;
        }

        // Pedidos de recarga que chegaram com outra recarga em andamento. Antes eram
        // descartados em silêncio: um refresh já em voo podia ter lido o banco ANTES da
        // mudança que motivou o novo pedido (notificação, restauração de ignorado,
        // conflito "a tela será atualizada"...), e a tela ficava velha até o próximo timer.
        private bool _recarregarOrcamentosPendente;
        private bool _recarregarPedidosPendente;

        private async Task RecarregarDadosAsync(bool recarregarOrcamentos = true, bool recarregarPedidos = true)
        {
            if (_recarregando)
            {
                _recarregarOrcamentosPendente |= recarregarOrcamentos;
                _recarregarPedidosPendente |= recarregarPedidos;
                return;
            }
            _recarregando = true;

            try
            {
                while (recarregarOrcamentos || recarregarPedidos)
                {
                    var tasks = new List<Task>();
                    if (recarregarOrcamentos) tasks.Add(CarregarOrcamentosAsync());
                    if (recarregarPedidos) tasks.Add(CarregarPedidosAsync());

                    await Task.WhenAll(tasks);

                    // Pedidos enfileirados durante esta passada: mais uma rodada.
                    recarregarOrcamentos = _recarregarOrcamentosPendente;
                    recarregarPedidos = _recarregarPedidosPendente;
                    _recarregarOrcamentosPendente = false;
                    _recarregarPedidosPendente = false;
                }
            }
            finally
            {
                _recarregando = false;
            }
        }

        /// <summary>
        /// Chamar logo após uma escrita local bem-sucedida (o próprio app atualiza os
        /// objetos em memória). Se havia recarga em voo, ela pode ter lido o banco antes
        /// da escrita e sobrescreveria o estado novo com o antigo — como a notificação da
        /// própria instância é descartada, ninguém corrigiria até o próximo timer.
        /// Enfileira mais uma passada para depois dela.
        /// </summary>
        private void InvalidarRecargaEmAndamento()
        {
            if (!_recarregando) return;
            _recarregarOrcamentosPendente = true;
            _recarregarPedidosPendente = true;
        }

        /// <summary>
        /// Outra instância gravou algo no banco de controle. Chega em ~1ms pelo canal do
        /// PostgreSQL, em thread de background — precisa marshalar para a UI.
        /// O coalescing evita rajadas: várias notificações seguidas viram um refresh só.
        /// </summary>
        private void Db_MudancaNotificada(object? sender, string motivo)
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                BeginInvoke(() =>
                {
                    if (!_dadosIniciaisCarregados || !_sessaoAtiva) return;
                    Logger.Info($"Notificação recebida ('{motivo}') — atualizando.");
                    _mudancaPendente = true;
                    timerCoalescer.Stop();
                    timerCoalescer.Start();
                });
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao processar notificação de mudança: {ex.Message}");
            }
        }

        private async void TimerCoalescer_Tick(object? sender, EventArgs e)
        {
            timerCoalescer.Stop();
            if (!_mudancaPendente) return;
            _mudancaPendente = false;

            try
            {
                // Só o banco de controle mudou — não precisa reconsultar o Integra inteiro,
                // mas CarregarPedidosAsync já reenriquece, então recarregamos os dois grids
                // para cobrir também o caso de aprovação feita por outro operador.
                await RecarregarDadosAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao atualizar após notificação de mudança", ex);
            }
        }

        /// <summary>
        /// Tick do auto-refresh. Para o timer durante o ciclo (evita empilhamento se um
        /// ciclo demorar mais que o intervalo) e reprograma com novo jitter ao final.
        /// </summary>
        private async void TimerAutoRefresh_Tick(object? sender, EventArgs e)
        {
            timerAutoRefresh.Stop();
            try
            {
                await RecarregarDadosAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro no auto-refresh", ex);
            }
            finally
            {
                AgendarProximoRefresh();
            }
        }

        private async void BtnRefresh_Click(object? sender, EventArgs e)
        {
            await RecarregarDadosAsync();
        }

        // ==================================================================
        // EXCLUSÃO MÚTUA DAS AÇÕES DOS BOTÕES
        // Os handlers são async: um duplo clique (ou clique em outro botão) durante o
        // await da primeira ação rodava a segunda com o MESMO estado em memória, ainda
        // não atualizado — o UPDATE com locking otimista não batia e o operador via um
        // falso "alterado por outro usuário" (ou dois previews de impressão empilhados).
        // ==================================================================
        private bool _acaoEmAndamento;

        private sealed class AcaoEmAndamento(Form1 form) : IDisposable
        {
            public void Dispose() => form._acaoEmAndamento = false;
        }

        /// <summary>null = já há uma ação rodando (o clique é ignorado).</summary>
        private AcaoEmAndamento? TentarIniciarAcao()
        {
            if (_acaoEmAndamento) return null;
            _acaoEmAndamento = true;
            return new AcaoEmAndamento(this);
        }

        private async void BtnAprovar_Click(object? sender, EventArgs e)
        {
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                    InvalidarRecargaEmAndamento();
                    _aprovados.Add(orc.Orcamento);

                    RemoverOrcamentoDoGrid(orc.Orcamento);

                    // Adiciona na master list (Vendedor será preenchido no próximo refresh).
                    // Um refresh concluído durante o await pode já tê-lo trazido do banco:
                    // inserir de novo duplicaria a chave e o ToDictionary do rebind lançaria.
                    if (!_pedidosTodos.Any(p => p.Orcamento == orc.Orcamento))
                    {
                        var novoPedido = new PedidoControlado
                        {
                            Orcamento = orc.Orcamento,
                            Cliente = orc.Nome,
                            NomeFantasia = orc.NomeFantasia,
                            Rota = dialog.RotaSelecionada
                        };
                        _pedidosTodos.Insert(0, novoPedido);
                    }
                    AtualizarOpcoesDropdowns();
                    AplicarFiltros();
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

        /// <summary>
        /// Remove o orçamento do grid de pendentes pela CHAVE. O objeto capturado no
        /// clique pode não estar mais na lista: um refresh durante o diálogo/MessageBox
        /// troca o DataSource por objetos novos, e Remove(objeto) por referência virava
        /// no-op — a linha controlada/ignorada continuava visível até o próximo timer.
        /// </summary>
        private void RemoverOrcamentoDoGrid(int orcamento)
        {
            if (dgvOrcamentos.DataSource is not SortableBindingList<OrcamentoResumo> lista) return;

            for (int i = lista.Count - 1; i >= 0; i--)
            {
                if (lista[i].Orcamento == orcamento)
                    lista.RemoveAt(i);
            }
            lblContagem.Text = $"{lista.Count} registro{(lista.Count != 1 ? "s" : "")}";
        }

        private async void BtnIgnorar_Click(object? sender, EventArgs e)
        {
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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

                _db.NotificarMudanca("ignorados");   // some da lista de pendentes dos outros

                // Atualiza o estado local e a grid manualmente
                InvalidarRecargaEmAndamento();
                _ignorados.Add(orc.Orcamento);
                RemoverOrcamentoDoGrid(orc.Orcamento);
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

        /// <summary>
        /// DDL idempotente. Aguardado ANTES da primeira carga: disparado em paralelo, num
        /// banco de controle recém-criado a carga corria contra o CREATE TABLE, falhava
        /// ("relation does not exist") e os grids ficavam vazios até o próximo timer.
        /// </summary>
        private async Task GarantirTabelasSeguroAsync()
        {
            try
            {
                await _db.GarantirTabelasAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao garantir tabelas do banco de controle", ex);
            }
        }

        private async void Db_StatusAlterado(object? sender, StatusConexaoEventArgs e)
        {
            if (IsDisposed) return;
            if (this.InvokeRequired)
            {
                this.BeginInvoke(() => Db_StatusAlterado(sender, e));
                return;
            }

            try
            {
                await AplicarStatusConexaoAsync(e);
            }
            catch (Exception ex)
            {
                // async void: exceção aqui derrubaria o app pelo handler global
                Logger.Error("Erro ao processar mudança de status da conexão", ex);
            }
        }

        private async Task AplicarStatusConexaoAsync(StatusConexaoEventArgs e)
        {
            if (e.StatusIntegra == StatusConexao.Conectado && e.StatusControle == StatusConexao.Conectado && !_dadosIniciaisCarregados)
            {
                _dadosIniciaisCarregados = true;
                await GarantirTabelasSeguroAsync();
                await RecarregarDadosAsync();
                AgendarProximoRefresh();
                _db.IniciarEscutaMudancas();   // ações de outros operadores chegam em ~1s
            }
            else if (e.StatusControle == StatusConexao.Conectado && _dadosIniciaisCarregados)
            {
                await GarantirTabelasSeguroAsync();
                await RecarregarDadosAsync(recarregarOrcamentos: false);
                // Salvar a configuração cancela a escuta (DatabaseService.AtualizarConfig) e
                // o status é renotificado com _dadosIniciaisCarregados ainda true: sem religar
                // aqui, o LISTEN morria e as outras instâncias só eram vistas pelo timer.
                // Idempotente: não faz nada se a escuta já está ativa.
                if (e.StatusIntegra == StatusConexao.Conectado)
                    _db.IniciarEscutaMudancas();
            }

            if (e.StatusIntegra == StatusConexao.Desconectado)
            {
                // Parar auto-refresh enquanto o Integra está fora
                timerAutoRefresh.Stop();
            }
            else if (e.StatusIntegra == StatusConexao.Conectado && _dadosIniciaisCarregados
                     && !timerAutoRefresh.Enabled)
            {
                // Integra voltou: religar o auto-refresh e recarregar o que se perdeu.
                // Antes o timer ficava parado até reabrir o app ou salvar a configuração.
                Logger.Info("Integra reconectado — auto-refresh religado.");
                AgendarProximoRefresh();
                await RecarregarDadosAsync();
            }

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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                    InvalidarRecargaEmAndamento();
                    pedido.Rota = dialog.RotaSelecionada;
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido foi alterado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            if (!_db.ControleConectado) return;

            var confirm = MessageBox.Show(this,
                $"ATENÇÃO: Esta ação é irreversível!\n\nExcluir o pedido {pedido.Orcamento} ({pedido.Cliente}) do controle?\n\nO orçamento voltará para a lista de pendentes caso não esteja faturado!",
                "Excluir Pedido",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                // Lookup best-effort da NFe (nunca bloqueia exclusao)
                var nfes = await _db.BuscarNotasFiscaisAsync(new[] { pedido.Orcamento });
                nfes.TryGetValue(pedido.Orcamento, out var nfe);

                var reg = new ExclusaoRegistro(
                    Orcamento: pedido.Orcamento,
                    Cliente: pedido.Cliente,
                    NomeFantasia: pedido.NomeFantasia,
                    Rota: pedido.Rota,
                    Vendedor: pedido.Vendedor,
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
                    TipoExclusao: ExclusaoRegistro.TipoManual);

                var linhas = await _db.ExcluirPedidoComHistoricoAsync(reg);

                if (linhas > 0)
                {
                    InvalidarRecargaEmAndamento();
                    _pedidosTodos.RemoveAll(p => p.Orcamento == pedido.Orcamento);
                    AtualizarOpcoesDropdowns();
                    AplicarFiltros();
                    await RecarregarDadosAsync(recarregarPedidos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                    InvalidarRecargaEmAndamento();
                    pedido.Travado = true;
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi travado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                    InvalidarRecargaEmAndamento();
                    pedido.Travado = false;
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi destravado ou excluído por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                await ExecutarAvancoStatusAsync(pedido, coluna);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao avançar status: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Avança o pedido para a coluna de status indicada com optimistic locking
        /// (WHERE com todas as flags atuais). Retorna true se avançou; em conflito
        /// (0 linhas), avisa e recarrega o grid.
        /// </summary>
        private async Task<bool> ExecutarAvancoStatusAsync(PedidoControlado pedido, string coluna,
            Func<IWin32Window>? ownerConflito = null)
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
                InvalidarRecargaEmAndamento();
                AplicarFiltros();
                return true;
            }

            MessageBox.Show(ownerConflito?.Invoke() ?? this, "O status deste pedido foi alterado por outro usuário.\nA tela será atualizada.",
                "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            await RecarregarDadosAsync(recarregarOrcamentos: false);
            return false;
        }

        private async void BtnRetornarStatus_Click(object? sender, EventArgs e)
        {
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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

            // Conferência veio exclusivamente do Integra: a etapa visível é "Conferido",
            // mas não há flag manual a desfazer — bloquear com orientação.
            if (!pedido.Liberado && !pedido.Conferido && pedido.ConferidoIntegra)
            {
                MessageBox.Show(this,
                    "A conferência deste pedido foi registrada pelo Integra (status de conferência 'S').\n" +
                    "Ela não pode ser desfeita por este sistema — a reversão da conferência deve ser feita no próprio Integra.",
                    "Conferência do Integra", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    InvalidarRecargaEmAndamento();
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "O status deste pedido foi alterado por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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

            if (!pedido.EmSeparacao || !pedido.ConferidoEfetivo || !pedido.Liberado)
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
                    // conferido usa lock de igualdade (não exigência de TRUE): a conferência
                    // pode estar satisfeita apenas pelo Integra (status_conferencia = 'S').
                    cmd.CommandText = "UPDATE pedidos_controlados SET em_faturamento = TRUE WHERE orcamento = @orc AND em_faturamento = FALSE AND em_separacao = TRUE AND conferido = @conf AND liberado = TRUE";
                    cmd.Parameters.AddWithValue("orc", pedido.Orcamento);
                    cmd.Parameters.AddWithValue("conf", pedido.Conferido);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas > 0)
                {
                    InvalidarRecargaEmAndamento();
                    pedido.EmFaturamento = true;
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi assumido por outro operador.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

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
                    InvalidarRecargaEmAndamento();
                    pedido.EmFaturamento = false;
                    AplicarFiltros();
                }
                else
                {
                    MessageBox.Show(this, "Este pedido já foi largado por outro operador.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    await RecarregarDadosAsync(recarregarOrcamentos: false);
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
            if (!p.ConferidoEfetivo) return "conferido";
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

        private async void BtnImprimirSeparacao_Click(object? sender, EventArgs e)
        {
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            await ImprimirSeparacaoAsync(pedido.Orcamento, pedido, this);
        }

        /// <summary>
        /// Fluxo completo do Relatório de Separação para um orçamento qualquer (controlado ou não).
        /// pedidoParaAvanco: quando informado (botão do grid), a impressão FÍSICA dispara
        /// APROVADO → EM SEPARAÇÃO; null (mini-janela "Imprimir por Orçamento") = nenhum
        /// efeito colateral. owner: dono dos MessageBoxes e do preview — obrigatório
        /// porque a mini-janela é modal e o preview faz ShowDialog(owner).
        /// </summary>
        private async Task ImprimirSeparacaoAsync(int orcamento, PedidoControlado? pedidoParaAvanco, Form owner)
        {
            if (!_db.IntegraConectado)
            {
                MessageBox.Show(owner, "Sem conexão com o banco Integra.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var config = _db.Config;
                var dados = await _db.ConsultarIntegraAsync(async conn =>
                {
                    SeparacaoCabecalho? cab = null;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT e.orcamento,
                                   COALESCE(e.vendedor::text, '')              AS vend_cod,
                                   COALESCE(f.nome, '')                        AS vend_nome,
                                   COALESCE(e.observacao, '')                  AS obs,
                                   COALESCE(e.cliente::text, '')               AS cli_cod,
                                   COALESCE(c.nome, '')                        AS cli_nome,
                                   COALESCE(c.fantasia, '')                    AS fantasia,
                                   COALESCE(c.endereco, '')                    AS endereco,
                                   COALESCE(c.numero::text, '')                AS numero,
                                   COALESCE(c.bairro, '')                      AS bairro,
                                   COALESCE(NULLIF(e.cidade, ''), c.cidade, '') AS cidade,
                                   COALESCE(NULLIF(e.estado, ''), c.estado, '') AS estado,
                                   e.data_inc,
                                   COALESCE(e.hora_inc, '')                    AS hora_inc
                            FROM estorc e
                            LEFT JOIN cadfun f ON f.funcionario = e.vendedor
                            LEFT JOIN cadcli c ON c.cliente = e.cliente
                            WHERE e.filial = @filial AND e.orcamento = @orc";
                        cmd.Parameters.AddWithValue("filial", config.Filial);
                        cmd.Parameters.AddWithValue("orc", orcamento);
                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var partesEndereco = new[] { reader.GetString(7), reader.GetString(8), reader.GetString(9) }
                                .Where(s => !string.IsNullOrWhiteSpace(s));
                            cab = new SeparacaoCabecalho
                            {
                                Orcamento = reader.GetInt32(0),
                                VendedorCodigo = reader.GetString(1),
                                VendedorNome = reader.GetString(2),
                                Observacao = reader.GetString(3),
                                ClienteCodigo = reader.GetString(4),
                                ClienteNome = reader.GetString(5),
                                Fantasia = reader.GetString(6),
                                Endereco = string.Join(", ", partesEndereco),
                                Cidade = reader.GetString(10),
                                Estado = reader.GetString(11),
                                DataPedido = FormatarDataPedido(reader, 12, 13)
                            };
                        }
                    }

                    var itens = new List<SeparacaoItem>();
                    if (cab != null)
                    {
                        using var cmd = conn.CreateCommand();
                        string filtroCancelado = config.SeparacaoOcultarCancelados
                            ? "AND COALESCE(i.cancelado, 'N') <> 'S'"
                            : "";
                        string ordenacao = config.SeparacaoOrdenarPorLocal
                            ? "COALESCE(s.local, ''), i.descricao, i.item"
                            : "i.descricao, i.item";
                        cmd.CommandText = $@"
                            SELECT i.item,
                                   i.matricula::text                           AS matricula,
                                   COALESCE(i.descricao, '')                   AS descricao,
                                   i.quantidade,
                                   COALESCE(s.local, '')                       AS local,
                                   COALESCE(s.referencia, '')                  AS referencia,
                                   COALESCE(s.marca, '')                       AS marca,
                                   CASE WHEN sys.atividade = 'V'
                                        THEN COALESCE(s.anteriorv,0) + COALESCE(s.entradav,0) - COALESCE(s.saidav,0)
                                        ELSE COALESCE(s.anteriora,0) + COALESCE(s.entradaa,0) - COALESCE(s.saidaa,0)
                                   END                                         AS saldo_atual,
                                   CASE WHEN sys.atividade = 'V'
                                        THEN COALESCE(s.anteriorv,0) + COALESCE(s.entradav,0) - COALESCE(s.saidav,0)
                                             - COALESCE(s.consignacaov,0) - COALESCE(s.condicionalv,0)
                                             - COALESCE(s.consertov,0) - COALESCE(s.servico,0)
                                             - COALESCE(s.estoque_reserva,0) + COALESCE(s.entregafuturav,0)
                                        ELSE COALESCE(s.anteriora,0) + COALESCE(s.entradaa,0) - COALESCE(s.saidaa,0)
                                             - COALESCE(s.consignacaoa,0) - COALESCE(s.condicionala,0)
                                             - COALESCE(s.consertoa,0) - COALESCE(s.servico,0)
                                             - COALESCE(s.estoque_reserva,0) + COALESCE(s.entregafuturaa,0)
                                   END                                         AS estoque_fisico,
                                   COALESCE(p.unidade, '')                     AS unidade
                            FROM estior i
                            LEFT JOIN estsal s ON s.filial = i.filial AND s.matricula = i.matricula
                            LEFT JOIN cadpro p ON p.matricula = i.matricula
                            CROSS JOIN (SELECT COALESCE((SELECT atividade FROM cadsys LIMIT 1), 'V') AS atividade) sys
                            WHERE i.filial = @filial AND i.orcamento = @orc
                              {filtroCancelado}
                            ORDER BY {ordenacao}";
                        cmd.Parameters.AddWithValue("filial", config.Filial);
                        cmd.Parameters.AddWithValue("orc", orcamento);
                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            itens.Add(new SeparacaoItem
                            {
                                Item = Convert.ToInt32(reader.GetValue(0)),
                                Matricula = reader.GetString(1),
                                Descricao = reader.GetString(2),
                                Quantidade = reader.GetDecimal(3),
                                Local = reader.GetString(4),
                                Referencia = reader.GetString(5),
                                Marca = reader.GetString(6),
                                SaldoAtual = reader.GetDecimal(7),
                                EstoqueFisico = reader.GetDecimal(8),
                                Unidade = reader.GetString(9)
                            });
                        }
                    }

                    return (cab, itens);
                });

                if (dados.cab == null)
                {
                    MessageBox.Show(owner, $"Orçamento {orcamento} não encontrado no Integra.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (dados.itens.Count == 0)
                {
                    MessageBox.Show(owner, "Nenhum item para separar neste orçamento.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var doc = new RelatorioSeparacaoPrintDoc(dados.cab, dados.itens,
                    config.SeparacaoPaisagem, config.SeparacaoCasasDecimais);

                if (pedidoParaAvanco is { } pedido)
                {
                    bool jaAvancou = false;
                    doc.ImpressaoRealConcluida += () =>
                    {
                        // Só a transição APROVADO → EM SEPARAÇÃO é automática; reimpressões
                        // em qualquer outro status não mexem em nada.
                        if (jaAvancou) return;
                        if (!pedido.Aprovado || pedido.EmSeparacao) return;
                        if (pedido.Travado || pedido.EmFaturamento) return;
                        jaAvancou = true;
                        _ = AvancarAposImpressaoAsync(pedido);
                    };
                }

                Logger.Info($"Separação {orcamento}: DataPedido='{dados.cab.DataPedido}' (estorc.data_inc/hora_inc)");
                PrintingHelper.LogarEstado(doc, "Imprimir Separação de Mercadorias");
                PrintingHelper.ExibirPreviewComImpressao(owner, doc, "Imprimir Separação de Mercadorias", () =>
                {
                    // PrintDialog reescreve DefaultPageSettings — re-força A4/orientação/margens
                    doc.DefaultPageSettings.PaperSize = PrintingHelper.CriarPaperSizeA4();
                    doc.DefaultPageSettings.Landscape = config.SeparacaoPaisagem;
                    doc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(25, 25, 25, 25);
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao gerar relatório de separação", ex);
                MessageBox.Show(owner, $"Erro ao gerar relatório de separação: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task AvancarAposImpressaoAsync(PedidoControlado pedido)
        {
            try
            {
                if (!_db.ControleConectado) return;
                // ImpressaoRealConcluida dispara com o preview (modal) ainda aberto: o aviso
                // de conflito precisa dele como owner, não do Form1 (invariante 5 — senão
                // nasce mal-parentado). Resolvido só na hora de exibir, depois do await:
                // se o preview já fechou, a janela ativa volta a ser o próprio Form1.
                await ExecutarAvancoStatusAsync(pedido, "em_separacao",
                    () => Form.ActiveForm is { IsDisposed: false } ativa ? ativa : this);
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao avançar status após impressão da separação", ex);
            }
        }

        /// <summary>
        /// Data/hora do pedido registrada no Integra (estorc.data_inc + hora_inc) já
        /// formatada para impressão. hora_inc é texto "HH:MM" pronto — sai verbatim,
        /// sem parse. data_inc nula → "—" (convenção do app para ausente).
        /// </summary>
        private static string FormatarDataPedido(System.Data.Common.DbDataReader reader, int ordData, int ordHora)
        {
            if (reader.IsDBNull(ordData)) return "—";
            var data = reader.GetDateTime(ordData);
            string hora = reader.GetString(ordHora).Trim();
            return hora.Length > 0 ? $"{data:dd/MM/yyyy} {hora}" : data.ToString("dd/MM/yyyy");
        }

        private async void BtnCopiaPedido_Click(object? sender, EventArgs e)
        {
            using var acao = TentarIniciarAcao();
            if (acao == null) return;

            if (dgvPedidos.CurrentRow?.DataBoundItem is not PedidoControlado pedido) return;
            await ImprimirCopiaPedidoAsync(pedido.Orcamento, this);
        }

        /// <summary>
        /// Fluxo completo da Cópia de Pedido para um orçamento qualquer (controlado ou não).
        /// Nunca altera status. owner: dono dos MessageBoxes e do preview (a mini-janela
        /// "Imprimir por Orçamento" é modal — ver ImprimirSeparacaoAsync).
        /// </summary>
        private async Task ImprimirCopiaPedidoAsync(int orcamento, Form owner)
        {
            if (!_db.IntegraConectado)
            {
                MessageBox.Show(owner, "Sem conexão com o banco Integra.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var config = _db.Config;
                var dados = await _db.ConsultarIntegraAsync(async conn =>
                {
                    CopiaPedidoCabecalho? cab = null;

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT e.orcamento,
                                   COALESCE(e.vendedor::text, '')               AS vend_cod,
                                   COALESCE(f.nome, '')                         AS vend_nome,
                                   COALESCE(e.observacao, '')                   AS obs,
                                   COALESCE(e.cliente::text, '')                AS cli_cod,
                                   COALESCE(c.nome, '')                         AS cli_nome,
                                   COALESCE(c.fantasia, '')                     AS fantasia,
                                   COALESCE(c.endereco, '')                     AS endereco,
                                   COALESCE(c.numero::text, '')                 AS numero,
                                   COALESCE(c.bairro, '')                       AS bairro,
                                   COALESCE(NULLIF(e.cidade, ''), c.cidade, '') AS cidade,
                                   COALESCE(NULLIF(e.estado, ''), c.estado, '') AS estado,
                                   COALESCE(c.cep, '')                          AS cep,
                                   COALESCE(c.natureza, '')                     AS natureza,
                                   COALESCE(c.cpf, '')                          AS cpf,
                                   COALESCE(c.identidade, '')                   AS identidade,
                                   COALESCE(c.ddd, '')                          AS ddd,
                                   COALESCE(c.fone, '')                         AS fone,
                                   COALESCE(c.ddd_celular, '')                  AS ddd_celular,
                                   COALESCE(c.celular, '')                      AS celular,
                                   COALESCE(c.e_mail, '')                       AS e_mail,
                                   COALESCE(e.texto01, '')                      AS texto01,
                                   COALESCE(e.texto02, '')                      AS texto02,
                                   COALESCE(e.texto03, '')                      AS texto03,
                                   COALESCE(e.texto04, '')                      AS texto04,
                                   COALESCE(e.vr_total, 0)                      AS vr_total,
                                   e.data_inc,
                                   COALESCE(e.hora_inc, '')                     AS hora_inc,
                                   COALESCE(e.vr_negociado, 0)                  AS vr_negociado
                            FROM estorc e
                            LEFT JOIN cadfun f ON f.funcionario = e.vendedor
                            LEFT JOIN cadcli c ON c.cliente = e.cliente
                            WHERE e.filial = @filial AND e.orcamento = @orc";
                        cmd.Parameters.AddWithValue("filial", config.Filial);
                        cmd.Parameters.AddWithValue("orc", orcamento);
                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            var partesEndereco = new[] { reader.GetString(7), reader.GetString(8), reader.GetString(9) }
                                .Where(s => !string.IsNullOrWhiteSpace(s));
                            cab = new CopiaPedidoCabecalho
                            {
                                Orcamento = reader.GetInt32(0),
                                VendedorCodigo = reader.GetString(1),
                                VendedorNome = reader.GetString(2),
                                Observacao = reader.GetString(3),
                                ClienteCodigo = reader.GetString(4),
                                ClienteNome = reader.GetString(5),
                                Fantasia = reader.GetString(6),
                                Endereco = string.Join(", ", partesEndereco),
                                Cidade = reader.GetString(10),
                                Estado = reader.GetString(11),
                                Cep = reader.GetString(12),
                                Natureza = reader.GetString(13).Trim().ToUpperInvariant(),
                                CpfCnpj = reader.GetString(14),
                                Identidade = reader.GetString(15).Trim(),
                                Ddd = reader.GetString(16),
                                Fone = reader.GetString(17),
                                DddCelular = reader.GetString(18),
                                Celular = reader.GetString(19),
                                Email = reader.GetString(20),
                                Texto01 = reader.GetString(21),
                                Texto02 = reader.GetString(22),
                                Texto03 = reader.GetString(23),
                                Texto04 = reader.GetString(24),
                                VrTotal = reader.GetDecimal(25),
                                DataPedido = FormatarDataPedido(reader, 26, 27),
                                VrNegociado = reader.GetDecimal(28)
                            };
                        }
                    }

                    var itens = new List<CopiaPedidoItem>();
                    if (cab != null)
                    {
                        using var cmd = conn.CreateCommand();
                        // Cliente nunca vê item cancelado — filtro fixo, sem opção de config.
                        string ordenacao = config.CopiaOrdenarAlfabetica
                            ? "i.descricao, i.item"
                            : "i.item";
                        cmd.CommandText = $@"
                            SELECT i.item,
                                   i.matricula::text          AS matricula,
                                   COALESCE(i.descricao, '')  AS descricao,
                                   i.quantidade,
                                   COALESCE(i.vr_unitario, 0) AS vr_unitario,
                                   COALESCE(i.vr_total, 0)    AS vr_total,
                                   COALESCE(s.marca, '')      AS marca,
                                   COALESCE(s.referencia, '') AS referencia
                            FROM estior i
                            LEFT JOIN estsal s ON s.filial = i.filial AND s.matricula = i.matricula
                            WHERE i.filial = @filial AND i.orcamento = @orc
                              AND COALESCE(i.cancelado, 'N') <> 'S'
                            ORDER BY {ordenacao}";
                        cmd.Parameters.AddWithValue("filial", config.Filial);
                        cmd.Parameters.AddWithValue("orc", orcamento);
                        using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            itens.Add(new CopiaPedidoItem
                            {
                                Item = Convert.ToInt32(reader.GetValue(0)),
                                Matricula = reader.GetString(1),
                                Descricao = reader.GetString(2),
                                Quantidade = reader.GetDecimal(3),
                                VrUnitario = reader.GetDecimal(4),
                                VrTotal = reader.GetDecimal(5),
                                Marca = reader.GetString(6),
                                Referencia = reader.GetString(7)
                            });
                        }
                    }

                    return (cab, itens);
                });

                if (dados.cab == null)
                {
                    MessageBox.Show(owner, $"Orçamento {orcamento} não encontrado no Integra.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (dados.itens.Count == 0)
                {
                    MessageBox.Show(owner, "Nenhum item ativo neste orçamento — cópia não gerada.",
                        "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Nº da NF: mesma correlação best-effort do Relatório de NF-e (estmov na
                // filial fiscal). Ausente = pedido ainda não faturado → imprime "—".
                var nfs = await _db.BuscarNotasFiscaisAsync([orcamento]);
                dados.cab.NotaFiscal = nfs.TryGetValue(orcamento, out var nf) ? nf : "";

                // SEM wiring de ImpressaoRealConcluida: a Cópia de Pedido nunca altera status.
                using var doc = new RelatorioCopiaPedidoPrintDoc(dados.cab, dados.itens,
                    EmpresaDados.De(config), config.CopiaPaisagem);

                Logger.Info($"Cópia de Pedido {orcamento}: DataPedido='{dados.cab.DataPedido}' (estorc.data_inc/hora_inc)");
                PrintingHelper.LogarEstado(doc, "Cópia de Pedido");
                PrintingHelper.ExibirPreviewComImpressao(owner, doc, "Cópia de Pedido", () =>
                {
                    // PrintDialog reescreve DefaultPageSettings — re-força A4/orientação/margens
                    doc.DefaultPageSettings.PaperSize = PrintingHelper.CriarPaperSizeA4();
                    doc.DefaultPageSettings.Landscape = config.CopiaPaisagem;
                    doc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(40, 40, 40, 40);
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao gerar Cópia de Pedido", ex);
                MessageBox.Show(owner, $"Erro ao gerar Cópia de Pedido: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvSelectionChanged(object? sender, EventArgs e)
        {
            // Durante um refresh a troca de DataSource mexe na seleção dos dois grids e
            // dispararia detalhes de orçamentos que o usuário não escolheu (roubando o
            // painel e gerando queries extras). Só seleção real do usuário conta.
            if (_suprimirSelectionChanged) return;

            int? orcamento = null;

            if (sender == dgvOrcamentos && dgvOrcamentos.CurrentRow?.DataBoundItem is OrcamentoResumo orc)
                orcamento = orc.Orcamento;
            else if (sender == dgvPedidos && dgvPedidos.CurrentRow?.DataBoundItem is PedidoControlado ped)
                orcamento = ped.Orcamento;

            if (orcamento == null || orcamento == _orcamentoDetalheAtual) return;
            _orcamentoDetalheAtual = orcamento.Value;
            _ = CarregarDetalhesAsync(orcamento.Value);
        }

        private async Task CarregarDetalhesAsync(int orcamento)
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
                        Cidade = await reader.IsDBNullAsync(0).ConfigureAwait(false) ? "" : reader.GetString(0),
                        Estado = await reader.IsDBNullAsync(1).ConfigureAwait(false) ? "" : reader.GetString(1),
                        ValorTotal = await reader.IsDBNullAsync(2).ConfigureAwait(false) ? 0m : reader.GetDecimal(2),
                        Texto01 = await reader.IsDBNullAsync(3).ConfigureAwait(false) ? "" : reader.GetString(3),
                        Texto02 = await reader.IsDBNullAsync(4).ConfigureAwait(false) ? "" : reader.GetString(4),
                        Texto03 = await reader.IsDBNullAsync(5).ConfigureAwait(false) ? "" : reader.GetString(5),
                        Texto04 = await reader.IsDBNullAsync(6).ConfigureAwait(false) ? "" : reader.GetString(6),
                        Observacao = await reader.IsDBNullAsync(7).ConfigureAwait(false) ? "" : reader.GetString(7),
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
                Logger.Warn($"Erro ao carregar detalhes do orçamento {orcamento}", ex);
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

        private void MenuHistoricoExclusoes_Click(object? sender, EventArgs e)
        {
            if (!_db.ControleConectado)
            {
                MessageBox.Show(this, "Sem conexão com o banco de controle.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var form = new HistoricoExclusoesForm();
            form.ShowDialog(this);
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

        private void MenuCorrigirObservacao_Click(object? sender, EventArgs e)
        {
            if (!_db.IntegraConectado)
            {
                MessageBox.Show(this, $"Sem conexão com o banco {_db.Config.BancoIntegra}.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_db.Config.FilialFiscal <= 0)
            {
                MessageBox.Show(this,
                    "Filial Fiscal não configurada. Acesse a Configuração para definir.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new CorrigirObservacaoDialog();
            dialog.ShowDialog(this);
        }

        private void MenuImprimirPorOrcamento_Click(object? sender, EventArgs e)
        {
            // Gate só no Integra: a impressão não precisa do banco de controle. Com o
            // controle offline o chip degrada para "Não controlado" — aceitável.
            if (!_db.IntegraConectado)
            {
                MessageBox.Show(this, $"Sem conexão com o banco {_db.Config.BancoIntegra}.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new ImprimirPorOrcamentoDialog(
                localizarControlado: orc => _pedidosTodos.FirstOrDefault(p => p.Orcamento == orc),
                // Decisão do usuário: impressão pela mini-janela NUNCA avança status
                // (pedidoParaAvanco: null) — o botão do grid continua avançando.
                imprimirSeparacao: (orc, owner) => ImprimirSeparacaoAsync(orc, null, owner),
                imprimirCopia: ImprimirCopiaPedidoAsync);
            dialog.ShowDialog(this);
        }

        private async void BtnConfig_Click(object? sender, EventArgs e)
        {
            using var dialog = new ConfigDialog(_db.Config);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            _db.AtualizarConfig(dialog.Config);

            // Antes: _dadosIniciaisCarregados = false, esperando a "próxima verificação de
            // conexão" recarregar. Mas StatusAlterado só dispara quando o status MUDA —
            // salvando com a conexão ok nada disparava, e com a flag em false o
            // AgendarProximoRefresh deixava de religar o timer e as notificações eram
            // descartadas: o auto-refresh morria até reabrir o app (e a filial/orçamento
            // mínimo novos não eram aplicados). Se o status mudar, Db_StatusAlterado cuida.
            if (!_dadosIniciaisCarregados) return;

            try
            {
                _assinaturaOrcamentos = "";
                _assinaturaPedidos = "";
                LimparDetalhes();
                await RecarregarDadosAsync();
                if (_db.IntegraConectado) AgendarProximoRefresh();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao recarregar após salvar a configuração", ex);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // SystemEvents é estático: sem desanexar, o form vaza
            Microsoft.Win32.SystemEvents.SessionSwitch -= SessionSwitchHandler;
            // O DatabaseService é singleton e sobrevive ao form: sem desanexar, um evento
            // de status/notificação posterior entraria num form já destruído.
            _db.StatusAlterado -= Db_StatusAlterado;
            _db.MudancaNotificada -= Db_MudancaNotificada;
            timerAutoRefresh.Stop();
            timerCoalescer.Stop();
            _db.Dispose();
            base.OnFormClosed(e);
        }

        // ================================================================
        // FILTROS DO GRID DE PEDIDOS
        // ================================================================

        private static string NormalizarVendedor(string? s) =>
            string.IsNullOrWhiteSpace(s) ? SemVendedor : s.Trim();

        private static string NormalizarRota(string? s) =>
            string.IsNullOrWhiteSpace(s) ? SemRota : s.Trim();

        private bool PassaFiltros(PedidoControlado p)
        {
            if (_filtroVendedores.Count > 0 && !_filtroVendedores.Contains(NormalizarVendedor(p.Vendedor)))
                return false;
            if (_filtroRotas.Count > 0 && !_filtroRotas.Contains(NormalizarRota(p.Rota)))
                return false;
            if (_filtroEtapas.Count > 0 && !_filtroEtapas.Contains(p.EtapaAtual))
                return false;
            return true;
        }

        private void AplicarFiltros()
        {
            var filtrados = _pedidosTodos.Where(PassaFiltros).ToList();

            // O AtualizarInPlace dispara ResetBindings (duas vezes quando há ordenação
            // ativa), e um Reset joga a célula corrente para a linha 0 e o scroll para o
            // topo. Sem preservar isso, refresh frequente tira a linha debaixo do cursor
            // do operador — e os botões de ação agem sobre CurrentRow.
            int orcamentoSelecionado = dgvPedidos.CurrentRow?.DataBoundItem is PedidoControlado sel
                ? sel.Orcamento : 0;
            int primeiraLinhaVisivel = dgvPedidos.FirstDisplayedScrollingRowIndex;
            int colunaSelecionada = dgvPedidos.CurrentCell?.ColumnIndex ?? 0;

            _suprimirSelectionChanged = true;
            try
            {
                if (dgvPedidos.DataSource is SortableBindingList<PedidoControlado> existente)
                {
                    existente.AtualizarInPlace(filtrados, p => p.Orcamento, (src, dst) => dst.CopiarDe(src));
                }
                else
                {
                    dgvPedidos.DataSource = new SortableBindingList<PedidoControlado>(filtrados);
                }

                RestaurarSelecaoPedidos(orcamentoSelecionado, primeiraLinhaVisivel, colunaSelecionada);
            }
            finally
            {
                _suprimirSelectionChanged = false;
            }

            int total = _pedidosTodos.Count;
            int mostrados = filtrados.Count;
            bool filtrando = _filtroVendedores.Count > 0 || _filtroRotas.Count > 0 || _filtroEtapas.Count > 0;
            if (filtrando)
                lblContagemPedidos.Text = $"{mostrados} de {total} registro{(total != 1 ? "s" : "")}";
            else
                lblContagemPedidos.Text = $"{total} registro{(total != 1 ? "s" : "")}";

            AtualizarIndicadoresVisuais();
            PersistirFiltros();
        }

        /// <summary>
        /// Recoloca a seleção no mesmo orçamento e devolve o scroll à posição anterior.
        /// Silencioso: falhas aqui nunca podem quebrar um refresh.
        /// </summary>
        private void RestaurarSelecaoPedidos(int orcamento, int primeiraLinhaVisivel, int coluna)
        {
            try
            {
                if (orcamento > 0)
                {
                    for (int i = 0; i < dgvPedidos.Rows.Count; i++)
                    {
                        if (dgvPedidos.Rows[i].DataBoundItem is not PedidoControlado p || p.Orcamento != orcamento)
                            continue;

                        if (dgvPedidos.CurrentRow?.Index != i)
                        {
                            dgvPedidos.ClearSelection();
                            dgvPedidos.Rows[i].Selected = true;
                            int col = Math.Clamp(coluna, 0, dgvPedidos.ColumnCount - 1);
                            if (dgvPedidos.Rows[i].Cells[col].Visible)
                                dgvPedidos.CurrentCell = dgvPedidos.Rows[i].Cells[col];
                        }
                        break;
                    }
                }

                if (primeiraLinhaVisivel >= 0 && primeiraLinhaVisivel < dgvPedidos.RowCount)
                    dgvPedidos.FirstDisplayedScrollingRowIndex = primeiraLinhaVisivel;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao restaurar seleção do grid de pedidos: {ex.Message}");
            }
        }

        private void PersistirFiltros()
        {
            FilterStore.SalvarDebounced(
                _filtroVendedores,
                _filtroRotas,
                _filtroEtapas.Select(e => e.ToString()));
        }

        private void AtualizarOpcoesDropdowns()
        {
            // Repopular destrói e recria os itens — se o usuário estiver com o dropdown
            // aberto, eles somem debaixo do cursor. Como vendedores/rotas quase nunca
            // mudam entre refreshes, sair cedo cobre a esmagadora maioria dos ciclos.
            var vendedores = _pedidosTodos.Select(p => NormalizarVendedor(p.Vendedor)).Distinct().Order();
            var rotas = _pedidosTodos.Select(p => NormalizarRota(p.Rota)).Distinct().Order();
            string assinatura = string.Join("\u001F", vendedores) + "\u001E" + string.Join("\u001F", rotas);
            if (assinatura == _assinaturaDropdowns) return;
            _assinaturaDropdowns = assinatura;

            PopularDropDownStrings(
                btnFiltroVendedor,
                _pedidosTodos.Select(p => NormalizarVendedor(p.Vendedor)),
                _filtroVendedores);

            PopularDropDownStrings(
                btnFiltroRota,
                _pedidosTodos.Select(p => NormalizarRota(p.Rota)),
                _filtroRotas);

            PopularDropDownEtapas(btnFiltroEtapa, _filtroEtapas);
        }

        private void PopularDropDownStrings(
            ToolStripDropDownButton botao,
            IEnumerable<string> opcoesDataset,
            HashSet<string> selecionados)
        {
            // Preserva itens macro (primeiros 2: "Marcar/Limpar" + Separator)
            int macrosCount = ContarMacros(botao);
            while (botao.DropDownItems.Count > macrosCount)
                botao.DropDownItems.RemoveAt(macrosCount);

            var todas = new HashSet<string>(opcoesDataset, StringComparer.CurrentCultureIgnoreCase);
            foreach (var s in selecionados) todas.Add(s); // manter fantasmas

            foreach (var v in todas.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                var item = new ToolStripMenuItem(v)
                {
                    CheckOnClick = true,
                    Checked = selecionados.Contains(v),
                    ForeColor = Theme.TextPrimary
                };
                var valor = v;
                item.CheckedChanged += (_, _) =>
                {
                    if (item.Checked) selecionados.Add(valor);
                    else selecionados.Remove(valor);
                    AplicarFiltros();
                };
                botao.DropDownItems.Add(item);
            }
        }

        private void PopularDropDownEtapas(ToolStripDropDownButton botao, HashSet<EtapaPedido> selecionados)
        {
            int macrosCount = ContarMacros(botao);
            while (botao.DropDownItems.Count > macrosCount)
                botao.DropDownItems.RemoveAt(macrosCount);

            // Ordem deliberada: fluxo natural do pedido
            var etapasEmOrdem = new[]
            {
                EtapaPedido.Aprovado,
                EtapaPedido.EmSeparacao,
                EtapaPedido.Conferido,
                EtapaPedido.LiberadoParaFaturamento,
                EtapaPedido.Faturado,
                EtapaPedido.NaoAprovado
            };

            foreach (var etapa in etapasEmOrdem)
            {
                var item = new ToolStripMenuItem(etapa.LabelAmigavel())
                {
                    CheckOnClick = true,
                    Checked = selecionados.Contains(etapa),
                    ForeColor = Theme.TextPrimary
                };
                var valor = etapa;
                item.CheckedChanged += (_, _) =>
                {
                    if (item.Checked) selecionados.Add(valor);
                    else selecionados.Remove(valor);
                    AplicarFiltros();
                };
                botao.DropDownItems.Add(item);
            }
        }

        private static int ContarMacros(ToolStripDropDownButton botao)
        {
            // Macros são: item macro + separator (sempre os 2 primeiros se presentes)
            int count = 0;
            if (botao.DropDownItems.Count > 0 && botao.DropDownItems[0].Tag as string == "macro") count++;
            if (botao.DropDownItems.Count > 1 && botao.DropDownItems[1] is ToolStripSeparator) count++;
            return count;
        }

        private void WireDropDownMacros()
        {
            // Adiciona (uma vez) o item "(Marcar todos / Limpar)" + separador em cada dropdown
            AdicionarMacro(btnFiltroVendedor, () => ToggleMacroStrings(btnFiltroVendedor, _filtroVendedores));
            AdicionarMacro(btnFiltroRota, () => ToggleMacroStrings(btnFiltroRota, _filtroRotas));
            AdicionarMacro(btnFiltroEtapa, () => ToggleMacroEtapas());

            btnLimparFiltros.Click += (_, _) => LimparTodosFiltros();
        }

        private static void AdicionarMacro(ToolStripDropDownButton botao, Action onClick)
        {
            var macro = new ToolStripMenuItem("(Marcar todos)")
            {
                Tag = "macro",
                ForeColor = Theme.TextSecondary
            };
            macro.Click += (_, _) => onClick();
            botao.DropDownItems.Add(macro);
            botao.DropDownItems.Add(new ToolStripSeparator());

            botao.DropDownOpening += (_, _) =>
            {
                bool algumMarcado = false;
                for (int i = ContarMacros(botao); i < botao.DropDownItems.Count; i++)
                {
                    if (botao.DropDownItems[i] is ToolStripMenuItem mi && mi.Checked) { algumMarcado = true; break; }
                }
                macro.Text = algumMarcado ? "(Limpar)" : "(Marcar todos)";
            };
        }

        private void ToggleMacroStrings(ToolStripDropDownButton botao, HashSet<string> selecionados)
        {
            int macrosCount = ContarMacros(botao);
            bool algumMarcado = selecionados.Count > 0;

            if (algumMarcado)
            {
                selecionados.Clear();
                for (int i = macrosCount; i < botao.DropDownItems.Count; i++)
                    if (botao.DropDownItems[i] is ToolStripMenuItem mi) mi.Checked = false;
            }
            else
            {
                for (int i = macrosCount; i < botao.DropDownItems.Count; i++)
                    if (botao.DropDownItems[i] is ToolStripMenuItem mi) mi.Checked = true;
            }
        }

        private void ToggleMacroEtapas()
        {
            int macrosCount = ContarMacros(btnFiltroEtapa);
            bool algumMarcado = _filtroEtapas.Count > 0;

            if (algumMarcado)
            {
                _filtroEtapas.Clear();
                for (int i = macrosCount; i < btnFiltroEtapa.DropDownItems.Count; i++)
                    if (btnFiltroEtapa.DropDownItems[i] is ToolStripMenuItem mi) mi.Checked = false;
            }
            else
            {
                for (int i = macrosCount; i < btnFiltroEtapa.DropDownItems.Count; i++)
                    if (btnFiltroEtapa.DropDownItems[i] is ToolStripMenuItem mi) mi.Checked = true;
            }
        }

        private void LimparTodosFiltros()
        {
            _filtroVendedores.Clear();
            _filtroRotas.Clear();
            _filtroEtapas.Clear();
            // Desmarcar itens visuais sem re-popular
            DesmarcarItens(btnFiltroVendedor);
            DesmarcarItens(btnFiltroRota);
            DesmarcarItens(btnFiltroEtapa);
            AplicarFiltros();
        }

        private static void DesmarcarItens(ToolStripDropDownButton botao)
        {
            int macrosCount = ContarMacros(botao);
            for (int i = macrosCount; i < botao.DropDownItems.Count; i++)
                if (botao.DropDownItems[i] is ToolStripMenuItem mi && mi.Checked) mi.Checked = false;
        }

        private void AtualizarIndicadoresVisuais()
        {
            AtualizarBotaoFiltro(btnFiltroVendedor, "Vendedor", _filtroVendedores.Count);
            AtualizarBotaoFiltro(btnFiltroRota, "Rota", _filtroRotas.Count);
            AtualizarBotaoFiltro(btnFiltroEtapa, "Etapa", _filtroEtapas.Count);
            btnLimparFiltros.Visible =
                _filtroVendedores.Count > 0 || _filtroRotas.Count > 0 || _filtroEtapas.Count > 0;
        }

        private static void AtualizarBotaoFiltro(ToolStripDropDownButton botao, string label, int qtd)
        {
            if (qtd > 0)
            {
                botao.Text = $"{label} ({qtd})";
                botao.Font = Theme.FontRegularBold;
                botao.ForeColor = Theme.Accent;
            }
            else
            {
                botao.Text = label;
                botao.Font = Theme.FontRegular;
                botao.ForeColor = Theme.TextPrimary;
            }
        }
    }

    public static class EtapaPedidoExt
    {
        public static string LabelAmigavel(this EtapaPedido e) => e switch
        {
            EtapaPedido.NaoAprovado => "(Não aprovado)",
            EtapaPedido.Aprovado => "Aprovados",
            EtapaPedido.EmSeparacao => "Em Separação",
            EtapaPedido.Conferido => "Conferidos",
            EtapaPedido.LiberadoParaFaturamento => "Lib. P/ Faturamento",
            EtapaPedido.Faturado => "Faturados",
            _ => e.ToString()
        };
    }

    public class OrcamentoResumo
    {
        public int Orcamento { get; set; }
        public string Nome { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
    }

    public enum EtapaPedido
    {
        NaoAprovado,
        Aprovado,
        EmSeparacao,
        Conferido,
        LiberadoParaFaturamento,
        Faturado
    }

    public class PedidoControlado
    {
        public int Orcamento { get; set; }
        public string Cliente { get; set; } = "";
        public string NomeFantasia { get; set; } = "";
        public string Rota { get; set; } = "";
        public string Vendedor { get; set; } = "";
        public bool Aprovado { get; set; }
        public bool EmSeparacao { get; set; }
        public bool Conferido { get; set; }
        public bool Liberado { get; set; }
        public bool Travado { get; set; }
        public bool EmFaturamento { get; set; }
        public int PedidoVenda { get; set; }

        /// <summary>Conferência registrada no Integra (estorc.status_conferencia = 'S'). Vem do enriquecimento, read-only.</summary>
        public bool ConferidoIntegra { get; set; }

        /// <summary>Conferência satisfeita por qualquer via (Integra tem precedência visual).</summary>
        public bool ConferidoEfetivo => Conferido || ConferidoIntegra;

        public string AprovadoTexto => Travado ? "TRAVADO" : (Aprovado ? "SIM" : "NÃO");
        public string EmSeparacaoTexto => Travado ? "TRAVADO" : (EmSeparacao ? "SIM" : "NÃO");
        public string ConferidoTexto => Travado ? "TRAVADO"
            : ConferidoIntegra ? "OK"
            : Conferido ? "MANUAL"
            : "NÃO";
        public string LiberadoTexto => Travado ? "TRAVADO" : (Liberado ? "SIM" : "NÃO");
        public string FaturadoTexto => PedidoVenda > 0 ? PedidoVenda.ToString() : (EmFaturamento ? "EM FATURAMENTO" : "NÃO");

        // Etapa atual discreta: flag marcada true com as posteriores em false.
        // "Lib. P/ Faturamento" engloba em_faturamento=true. "Faturado" = PedidoVenda>0.
        public EtapaPedido EtapaAtual
        {
            get
            {
                if (PedidoVenda > 0) return EtapaPedido.Faturado;
                if (Liberado) return EtapaPedido.LiberadoParaFaturamento;
                if (ConferidoEfetivo) return EtapaPedido.Conferido;
                if (EmSeparacao) return EtapaPedido.EmSeparacao;
                if (Aprovado) return EtapaPedido.Aprovado;
                return EtapaPedido.NaoAprovado;
            }
        }

        public void CopiarDe(PedidoControlado outro)
        {
            Cliente = outro.Cliente;
            NomeFantasia = outro.NomeFantasia;
            Rota = outro.Rota;
            Vendedor = outro.Vendedor;
            Aprovado = outro.Aprovado;
            EmSeparacao = outro.EmSeparacao;
            Conferido = outro.Conferido;
            Liberado = outro.Liberado;
            Travado = outro.Travado;
            EmFaturamento = outro.EmFaturamento;
            PedidoVenda = outro.PedidoVenda;
            ConferidoIntegra = outro.ConferidoIntegra;
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
