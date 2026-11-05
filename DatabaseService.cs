using Npgsql;

namespace CONTROLE_DE_PEDIDOS
{
    public enum StatusConexao
    {
        Verificando,
        Conectado,
        Desconectado
    }

    /// <summary>Resultado de <see cref="DatabaseService.ExcluirPedidoComHistoricoAsync"/>.</summary>
    public enum ResultadoExclusao
    {
        Excluido,
        /// <summary>A linha não existe mais: outro usuário já excluiu.</summary>
        JaExcluido,
        /// <summary>A linha existe com estado diferente do esperado: outro usuário alterou.</summary>
        Alterado
    }

    /// <summary>
    /// Resultado do botão "Testar" do ConfigDialog, um item por banco. Erro null = OK.
    /// <paramref name="ControleSeraCriado"/>: o banco de controle ainda não existe, mas o
    /// servidor responde — o app o cria ao conectar, então não conta como falha.
    /// </summary>
    public sealed record ResultadoTesteConexao(string? ErroIntegra, string? ErroControle, bool ControleSeraCriado)
    {
        public bool IntegraOk => ErroIntegra == null;
        public bool ControleOk => ErroControle == null || ControleSeraCriado;
    }

    public class StatusConexaoEventArgs : EventArgs
    {
        public StatusConexao StatusIntegra { get; }
        public StatusConexao StatusControle { get; }
        public string Mensagem { get; }

        public StatusConexaoEventArgs(StatusConexao statusIntegra, StatusConexao statusControle, string mensagem)
        {
            StatusIntegra = statusIntegra;
            StatusControle = statusControle;
            Mensagem = mensagem;
        }
    }

    /// <summary>
    /// Conexao read-only ao integrapgsql. Nenhuma operacao de escrita e permitida.
    /// O PostgreSQL forcara erro se tentarem INSERT/UPDATE/DELETE via default_transaction_read_only=on.
    /// </summary>
    public sealed class IntegraReadOnlyConnection : IDisposable
    {
        private readonly NpgsqlConnection _conn;

        internal IntegraReadOnlyConnection(NpgsqlConnection conn)
        {
            _conn = conn;
        }

        public NpgsqlCommand CreateCommand()
        {
            return _conn.CreateCommand();
        }

        public void Dispose()
        {
            _conn.Dispose();
        }
    }

    public sealed class DatabaseService : IDisposable
    {
        private static DatabaseService? _instancia;
        private static readonly object _lock = new();

        private AppConfig _config;
        private System.Threading.Timer _timer;
        private volatile StatusConexao _statusIntegra = StatusConexao.Desconectado;
        private volatile StatusConexao _statusControle = StatusConexao.Desconectado;
        private readonly SynchronizationContext? _syncContext;
        private int _verificando;
        private bool _bancoControleGarantido;
        /// <summary>1 = a próxima verificação publica StatusAlterado mesmo sem mudança
        /// (após trocar a config o Form1 zera o estado e só recarrega ao receber o evento).</summary>
        private int _forcarNotificacaoStatus;
        private int _falhasGarantirBanco;

        // --- LISTEN/NOTIFY: avisa as outras instâncias quando alguém grava algo ---
        private const string CanalMudancas = "cp_mudou";
        /// <summary>Identifica esta instância no payload: quem gravou já atualizou a própria
        /// tela na hora, então ignora o próprio aviso e evita um refresh redundante.</summary>
        private static readonly string IdInstancia = Guid.NewGuid().ToString("N")[..8];
        private CancellationTokenSource? _ctsListener;
        private Task? _tarefaListener;

        public event EventHandler<StatusConexaoEventArgs>? StatusAlterado;

        /// <summary>
        /// Disparado (em thread de background) quando OUTRA instância grava algo no banco
        /// de controle. Quem assina deve marshalar para a UI.
        /// </summary>
        public event EventHandler<string>? MudancaNotificada;

        public StatusConexao StatusIntegra => _statusIntegra;
        public StatusConexao StatusControle => _statusControle;
        public bool IntegraConectado => _statusIntegra == StatusConexao.Conectado;
        public bool ControleConectado => _statusControle == StatusConexao.Conectado;
        public bool Conectado => IntegraConectado && ControleConectado;
        public AppConfig Config => _config;

        public static DatabaseService Instancia
        {
            get
            {
                lock (_lock)
                {
                    _instancia ??= new DatabaseService();
                    return _instancia;
                }
            }
        }

        private DatabaseService()
        {
            _syncContext = SynchronizationContext.Current;
            _config = AppConfig.Carregar();
            _timer = new System.Threading.Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Iniciar(int intervaloMs = 15000)
        {
            _timer.Change(0, intervaloMs);
        }

        // ==================================================================
        // NOTIFICAÇÃO ENTRE INSTÂNCIAS (LISTEN/NOTIFY do PostgreSQL)
        // Com 10+ usuários no mesmo servidor, esperar o próximo ciclo do timer para ver
        // o que outro operador acabou de fazer é o que mais incomoda. O NOTIFY entrega
        // isso em ~1ms e custa praticamente nada: a conexão fica ociosa esperando.
        // Só vale para o banco PRÓPRIO — o banco do Integra é read-only e intocável.
        // ==================================================================

        /// <summary>
        /// Avisa as outras instâncias que algo mudou. Silencioso por design: uma falha
        /// aqui não pode derrubar a operação que acabou de ser gravada com sucesso —
        /// o refresh periódico continua sendo a rede de segurança.
        /// </summary>
        public async Task NotificarMudancaAsync(string motivo)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionStringControle);
                await conn.OpenAsync().ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT pg_notify(@canal, @payload)";
                cmd.Parameters.AddWithValue("canal", CanalMudancas);
                cmd.Parameters.AddWithValue("payload", $"{motivo}|{IdInstancia}");
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao notificar mudança ('{motivo}'): {ex.Message}");
            }
        }

        /// <summary>Versão fire-and-forget para usar nos handlers de UI.</summary>
        public void NotificarMudanca(string motivo) => _ = NotificarMudancaAsync(motivo);

        /// <summary>
        /// Abre a escuta de notificações. A conexão é DEDICADA e sem pooling: ao devolver
        /// uma conexão ao pool o Npgsql emite DISCARD ALL, que cancela o LISTEN
        /// silenciosamente. Reconecta sozinha em caso de queda.
        /// </summary>
        public void IniciarEscutaMudancas()
        {
            if (_tarefaListener != null) return;
            // Token capturado AGORA: ler _ctsListener dentro da lambda pegaria null (ou
            // o CTS de outro ciclo) se a escuta fosse parada/reiniciada antes do Task.Run rodar.
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            _ctsListener = cts;
            _tarefaListener = Task.Run(() => LoopEscutaAsync(token));
        }

        /// <summary>
        /// Encerra a escuta sem bloquear a UI. Usado ao trocar a configuração: a conexão
        /// dedicada continuava no servidor/banco ANTIGO (o WaitAsync não volta sozinho),
        /// e o IniciarEscutaMudancas seguinte não fazia nada porque a tarefa existia.
        /// O Form1 religa a escuta quando a nova conexão é confirmada.
        /// </summary>
        private void CancelarEscutaMudancas()
        {
            var cts = _ctsListener;
            var tarefa = _tarefaListener;
            _ctsListener = null;
            _tarefaListener = null;
            if (cts == null) return;

            try { cts.Cancel(); } catch { /* já descartado */ }
            if (tarefa != null)
                _ = tarefa.ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
            else
                cts.Dispose();
        }

        private async Task LoopEscutaAsync(CancellationToken ct)
        {
            int falhasSeguidas = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!ControleConectado)
                    {
                        await Task.Delay(5000, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Pooling=false é obrigatório (DISCARD ALL mataria o LISTEN);
                    // Keepalive evita que firewall/SO derrubem a conexão ociosa —
                    // o default do Npgsql é 0 (desligado).
                    var csb = new NpgsqlConnectionStringBuilder(_config.ConnectionStringControle)
                    {
                        Pooling = false,
                        KeepAlive = 30,
                        ApplicationName = "ControleDePedidos-Listener"
                    };

                    await using var conn = new NpgsqlConnection(csb.ConnectionString);
                    conn.Notification += (_, args) =>
                    {
                        try
                        {
                            string payload = args.Payload ?? "";
                            // Ignora o próprio aviso: quem gravou já atualizou a tela
                            if (payload.EndsWith($"|{IdInstancia}", StringComparison.Ordinal)) return;

                            int sep = payload.IndexOf('|');
                            MudancaNotificada?.Invoke(this, sep >= 0 ? payload[..sep] : payload);
                        }
                        catch (Exception ex) { Logger.Warn($"Erro no handler de notificação: {ex.Message}"); }
                    };

                    await conn.OpenAsync(ct).ConfigureAwait(false);
                    await using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"LISTEN {CanalMudancas}";
                        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }

                    if (falhasSeguidas > 0)
                    {
                        Logger.Info("Escuta de mudanças restabelecida.");
                        // NOTIFY não é enfileirado para quem não está escutando: o que
                        // outros operadores gravaram durante a queda se perdeu. Pede um
                        // refresh (o dirty-check do Form1 descarta se nada mudou) em vez
                        // de esperar o timer de segurança, que pode estar em backoff.
                        try { MudancaNotificada?.Invoke(this, "reconexao"); }
                        catch (Exception ex) { Logger.Warn($"Erro no handler de notificação: {ex.Message}"); }
                    }
                    falhasSeguidas = 0;

                    // Bloqueia até chegar notificação (ou a conexão cair)
                    while (!ct.IsCancellationRequested)
                        await conn.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    falhasSeguidas++;
                    // Só loga as primeiras falhas: se o banco cair de vez, não adianta
                    // encher o log a cada tentativa.
                    if (falhasSeguidas <= 3)
                        Logger.Warn($"Escuta de mudanças caiu ({ex.GetType().Name}: {ex.Message}); reconectando.");

                    int espera = Math.Min(30000, 2000 * falhasSeguidas);   // backoff até 30s
                    try { await Task.Delay(espera, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private void PararEscutaMudancas()
        {
            try
            {
                _ctsListener?.Cancel();
                _tarefaListener?.Wait(TimeSpan.FromSeconds(2));
            }
            catch { /* encerrando o app — nada a fazer */ }
            finally
            {
                _ctsListener?.Dispose();
                _ctsListener = null;
                _tarefaListener = null;
            }
        }

        public void AtualizarConfig(AppConfig novaConfig)
        {
            _config = novaConfig;
            try
            {
                _config.Salvar();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao salvar config.ini", ex);
            }
            _bancoControleGarantido = false;
            CancelarEscutaMudancas();
            // O Form1 zera _dadosIniciaisCarregados e espera um StatusAlterado para
            // recarregar. Se a conexão seguia igual (ex.: trocou só a Filial), nenhum
            // evento saía e a tela ficava sem auto-refresh e sem notificações até reabrir.
            Interlocked.Exchange(ref _forcarNotificacaoStatus, 1);
            _ = VerificarConexaoAsync();
        }

        public async Task<bool> VerificarConexaoAsync()
        {
            if (Interlocked.CompareExchange(ref _verificando, 1, 0) != 0)
                return Conectado;

            try
            {
                // Os dois bancos são testados de forma INDEPENDENTE (e em paralelo). Antes o
                // controle só era testado se o Integra respondesse: com o Integra fora o app
                // inteiro ficava "Desconectado", inclusive as telas que só usam o controle.
                var tarefaIntegra = TestarConexaoAsync(_config.ConnectionStringIntegra);
                var tarefaControle = Task.Run(async () =>
                {
                    // Só marca como garantido se deu certo: antes o true era gravado mesmo
                    // após falha (o catch interno zerava e a linha seguinte sobrescrevia),
                    // e o banco nunca mais era criado até trocar a configuração.
                    if (!_bancoControleGarantido)
                        _bancoControleGarantido = await GarantirBancoControleAsync().ConfigureAwait(false);
                    return await TestarConexaoAsync(_config.ConnectionStringControle).ConfigureAwait(false);
                });
                bool okIntegra = await tarefaIntegra.ConfigureAwait(false);
                bool okControle = await tarefaControle.ConfigureAwait(false);

                var novoIntegra = okIntegra ? StatusConexao.Conectado : StatusConexao.Desconectado;
                var novoControle = okControle ? StatusConexao.Conectado : StatusConexao.Desconectado;

                bool mudou = novoIntegra != _statusIntegra || novoControle != _statusControle;
                _statusIntegra = novoIntegra;
                _statusControle = novoControle;

                if (Interlocked.Exchange(ref _forcarNotificacaoStatus, 0) == 1)
                    mudou = true;

                if (mudou)
                {
                    string mensagem;
                    if (okIntegra && okControle)
                        mensagem = $"Conectado - {_config.ServidorIP}";
                    else if (okIntegra)
                        mensagem = $"Parcial - {_config.ServidorIP} (sem {_config.BancoControle})";
                    else if (okControle)
                        mensagem = $"Parcial - {_config.ServidorIP} (sem {_config.BancoIntegra})";
                    else
                        mensagem = $"Desconectado - {_config.ServidorIP}";

                    NotificarStatus(novoIntegra, novoControle, mensagem);
                }

                return okIntegra && okControle;
            }
            finally
            {
                Interlocked.Exchange(ref _verificando, 0);
            }
        }

        private async Task<bool> GarantirBancoControleAsync()
        {
            try
            {
                using var conn = new NpgsqlConnection(_config.ConnectionStringServidor);
                await conn.OpenAsync().ConfigureAwait(false);

                using var cmdCheck = conn.CreateCommand();
                cmdCheck.CommandText = "SELECT 1 FROM pg_database WHERE datname = @nome";
                cmdCheck.Parameters.AddWithValue("nome", _config.BancoControle);

                if (await cmdCheck.ExecuteScalarAsync().ConfigureAwait(false) == null)
                {
                    using var cmdCreate = conn.CreateCommand();
                    // Identificador não aceita parâmetro: escapa aspas duplas (nome vem do INI).
                    var nome = _config.BancoControle.Replace("\"", "\"\"");
                    cmdCreate.CommandText = $"CREATE DATABASE \"{nome}\" ENCODING 'UTF8'";
                    await cmdCreate.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
                _falhasGarantirBanco = 0;
                return true;
            }
            catch (Exception ex)
            {
                // Agora retenta a cada ciclo (15s): loga a 1ª falha e depois ~a cada 10 min,
                // para uma falha persistente (ex.: sem acesso ao banco postgres) não inundar o log.
                if (_falhasGarantirBanco++ % 40 == 0)
                    Logger.Error("Erro ao garantir a existência do banco de controle", ex);
                return false;
            }
        }

        public async Task GarantirTabelasAsync()
        {
            if (!ControleConectado) return;

            // O Form1 chama sem await (_ = GarantirTabelasAsync()): uma exceção aqui virava
            // Task não observada e sumia sem log — ex.: corrida de CREATE ... IF NOT EXISTS
            // entre várias instâncias subindo juntas (23505 em pg_type), ou falta de permissão.
            try
            {
                await ExecutarControleAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS cadastro_rotas (
                            id SERIAL PRIMARY KEY,
                            nome VARCHAR(200) NOT NULL UNIQUE
                        );
                        CREATE TABLE IF NOT EXISTS orcamentos_ignorados (
                            orcamento INTEGER PRIMARY KEY,
                            motivo VARCHAR(300),
                            data_inclusao TIMESTAMP NOT NULL DEFAULT NOW()
                        );
                        CREATE TABLE IF NOT EXISTS pedidos_controlados (
                            orcamento INTEGER PRIMARY KEY,
                            cliente VARCHAR(300) NOT NULL DEFAULT '',
                            nome_fantasia VARCHAR(300) NOT NULL DEFAULT '',
                            rota VARCHAR(200) NOT NULL DEFAULT '',
                            aprovado BOOLEAN NOT NULL DEFAULT FALSE,
                            em_separacao BOOLEAN NOT NULL DEFAULT FALSE,
                            conferido BOOLEAN NOT NULL DEFAULT FALSE,
                            liberado BOOLEAN NOT NULL DEFAULT FALSE,
                            travado BOOLEAN NOT NULL DEFAULT FALSE,
                            data_aprovacao TIMESTAMP NOT NULL DEFAULT NOW()
                        );
                        ALTER TABLE pedidos_controlados ADD COLUMN IF NOT EXISTS travado BOOLEAN NOT NULL DEFAULT FALSE;
                        ALTER TABLE pedidos_controlados ADD COLUMN IF NOT EXISTS em_faturamento BOOLEAN NOT NULL DEFAULT FALSE;
                        CREATE TABLE IF NOT EXISTS historico_exclusoes (
                            id SERIAL PRIMARY KEY,
                            orcamento INTEGER NOT NULL,
                            cliente VARCHAR(300) NOT NULL DEFAULT '',
                            nome_fantasia VARCHAR(300) NOT NULL DEFAULT '',
                            rota VARCHAR(200) NOT NULL DEFAULT '',
                            vendedor VARCHAR(200) NOT NULL DEFAULT '',
                            pedido_venda INTEGER NOT NULL DEFAULT 0,
                            nota_fiscal VARCHAR(20) NOT NULL DEFAULT '',
                            status VARCHAR(40) NOT NULL DEFAULT '',
                            aprovado BOOLEAN NOT NULL DEFAULT FALSE,
                            em_separacao BOOLEAN NOT NULL DEFAULT FALSE,
                            conferido BOOLEAN NOT NULL DEFAULT FALSE,
                            liberado BOOLEAN NOT NULL DEFAULT FALSE,
                            travado BOOLEAN NOT NULL DEFAULT FALSE,
                            em_faturamento BOOLEAN NOT NULL DEFAULT FALSE,
                            tipo_exclusao VARCHAR(10) NOT NULL DEFAULT '',
                            usuario_windows VARCHAR(100) NOT NULL DEFAULT '',
                            data_exclusao TIMESTAMP NOT NULL DEFAULT NOW()
                        );
                        CREATE INDEX IF NOT EXISTS ix_historico_exclusoes_data
                            ON historico_exclusoes (data_exclusao DESC);
                        CREATE INDEX IF NOT EXISTS ix_historico_exclusoes_orcamento
                            ON historico_exclusoes (orcamento)";
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao garantir as tabelas do banco de controle", ex);
            }
        }

        // =====================================================================
        // INTEGRA - SOMENTE LEITURA (SELECT)
        // A conexao usa default_transaction_read_only=on no PostgreSQL.
        // A API so expoe consulta. Qualquer tentativa de escrita sera barrada.
        // IMPORTANTE: Roda 100% em background via Task.Run - nunca bloqueia UI.
        // =====================================================================

        public async Task<T?> ConsultarIntegraAsync<T>(Func<IntegraReadOnlyConnection, Task<T>> consulta)
        {
            if (!IntegraConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoIntegra}.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringIntegra);
            await conn.OpenAsync().ConfigureAwait(false);
            using var readOnly = new IntegraReadOnlyConnection(conn);
            return await consulta(readOnly).ConfigureAwait(false);
        }

        // =====================================================================
        // INTEGRA - ESCRITA (USO EXCEPCIONAL)
        // Unico ponto de escrita em integrapgsql. Usado SOMENTE pelo fluxo
        // "Corrigir Observacao". Nao adicionar novos consumidores sem revisao.
        // =====================================================================
        public async Task<int> AtualizarObservacaoEstmovAsync(int pedido, string novaObservacao)
        {
            if (!IntegraConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoIntegra}.");
            if (_config.FilialFiscal <= 0)
                throw new InvalidOperationException("Filial Fiscal não configurada.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringIntegraEscrita);
            await conn.OpenAsync().ConfigureAwait(false);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE estmov
                   SET observacao = @obs
                 WHERE filial = @filial
                   AND pedido = @pedido";
            cmd.Parameters.AddWithValue("obs", novaObservacao);
            cmd.Parameters.AddWithValue("filial", _config.FilialFiscal);
            cmd.Parameters.AddWithValue("pedido", pedido);
            return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // =====================================================================
        // CONTROLE - LEITURA E ESCRITA
        // Unico banco onde a aplicacao pode gravar dados.
        // IMPORTANTE: Roda 100% em background via Task.Run - nunca bloqueia UI.
        // =====================================================================

        public async Task<T?> ExecutarControleAsync<T>(Func<NpgsqlConnection, Task<T>> operacao)
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringControle);
            await conn.OpenAsync().ConfigureAwait(false);
            return await operacao(conn).ConfigureAwait(false);
        }

        public async Task ExecutarControleAsync(Func<NpgsqlConnection, Task> operacao)
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringControle);
            await conn.OpenAsync().ConfigureAwait(false);
            await operacao(conn).ConfigureAwait(false);
        }

        /// <summary>
        /// Executa uma operação de ESCRITA no banco de controle e devolve as linhas
        /// afetadas. Quando algo é de fato gravado, avisa as outras instâncias pelo canal
        /// de notificação — centralizar aqui garante que nenhum ponto de escrita fique
        /// sem avisar (são 8 fluxos diferentes: avançar, retornar, travar, destravar,
        /// assumir, largar, alterar rota, excluir).
        /// </summary>
        public async Task<int> ExecutarControleComLinhasAsync(Func<NpgsqlConnection, Task<int>> operacao)
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            int linhas;
            using (var conn = new NpgsqlConnection(_config.ConnectionStringControle))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                linhas = await operacao(conn).ConfigureAwait(false);
            }

            if (linhas > 0)
                await NotificarMudancaAsync("pedidos").ConfigureAwait(false);

            return linhas;
        }

        // =====================================================================
        // HISTORICO DE EXCLUSOES
        // Lookup de NFes no integra (best-effort) + exclusao atomica
        // (DELETE pedidos_controlados + INSERT historico_exclusoes) em
        // transacao local, com locking otimista. Conflito (outro usuario ja
        // excluiu ou alterou o pedido) e devolvido como ResultadoExclusao e
        // nesse caso o historico NAO recebe registro falso.
        // =====================================================================

        /// <summary>
        /// Busca em batch as notas fiscais vinculadas aos orcamentos no integra.
        /// Retorna dict vazio silenciosamente se integra desconectado, filial fiscal
        /// invalida, lista vazia, ou qualquer erro na consulta (com log Warn).
        /// Nunca lanca — e best-effort para alimentar o historico.
        /// </summary>
        public async Task<Dictionary<int, string>> BuscarNotasFiscaisAsync(IEnumerable<int> orcamentos)
        {
            var lista = orcamentos?.Distinct().ToList() ?? new List<int>();
            if (lista.Count == 0 || !IntegraConectado || _config.FilialFiscal <= 0)
                return new Dictionary<int, string>();

            try
            {
                var resultado = await ConsultarIntegraAsync(async conn =>
                {
                    var dict = new Dictionary<int, string>();
                    using var cmd = conn.CreateCommand();
                    var paramList = string.Join(",", lista.Select((_, i) => $"@p{i}"));
                    cmd.CommandText = $@"
                        SELECT observacao, nota_fiscal
                          FROM estmov
                         WHERE filial = @filial
                           AND observacao IN ({paramList})";
                    cmd.Parameters.AddWithValue("filial", _config.FilialFiscal);
                    for (int i = 0; i < lista.Count; i++)
                        cmd.Parameters.AddWithValue($"p{i}", lista[i].ToString());

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (await reader.IsDBNullAsync(0).ConfigureAwait(false)) continue;
                        if (await reader.IsDBNullAsync(1).ConfigureAwait(false)) continue;
                        var obs = reader.GetValue(0)?.ToString()?.Trim() ?? "";
                        if (!int.TryParse(obs, out var orc)) continue;
                        dict[orc] = reader.GetValue(1)?.ToString() ?? "";
                    }
                    return dict;
                }).ConfigureAwait(false);
                return resultado ?? new Dictionary<int, string>();
            }
            catch (Exception ex)
            {
                Logger.Warn("Falha ao buscar notas fiscais para historico de exclusao", ex);
                return new Dictionary<int, string>();
            }
        }

        /// <summary>
        /// Executa DELETE do pedido em pedidos_controlados e INSERT no historico
        /// atomicamente numa transacao, com locking otimista: o DELETE só acontece se a
        /// linha ainda tiver o estado que a tela mostrava (flags de status + rota) — o
        /// mesmo padrão dos UPDATEs. Antes, um pedido que outro operador travou ou pôs em
        /// faturamento entre o último refresh e o clique era apagado mesmo assim.
        /// Em conflito o INSERT é pulado (histórico não recebe registro falso) e o retorno
        /// diz se a linha sumiu (<see cref="ResultadoExclusao.JaExcluido"/>) ou mudou
        /// (<see cref="ResultadoExclusao.Alterado"/>).
        /// </summary>
        public async Task<ResultadoExclusao> ExcluirPedidoComHistoricoAsync(ExclusaoRegistro reg)
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringControle);
            await conn.OpenAsync().ConfigureAwait(false);
            using var tx = await conn.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                // WHERE com o estado esperado (locking otimista). RETURNING: o histórico
                // registra a linha REAL apagada (cliente/nome fantasia podem ter sido
                // reenriquecidos depois do refresh da tela). Vendedor, pedido_venda e NF-e
                // (que não existem nesta tabela) continuam vindo do `reg`.
                int linhas = 0;
                using (var cmdDel = conn.CreateCommand())
                {
                    cmdDel.Transaction = tx;
                    cmdDel.CommandText = @"
                        DELETE FROM pedidos_controlados
                         WHERE orcamento = @orc
                           AND rota = @rota
                           AND aprovado = @apr AND em_separacao = @sep AND conferido = @cnf
                           AND liberado = @lib AND travado = @trv AND em_faturamento = @fat
                        RETURNING cliente, nome_fantasia, rota,
                                  aprovado, em_separacao, conferido, liberado, travado, em_faturamento";
                    cmdDel.Parameters.AddWithValue("orc", reg.Orcamento);
                    cmdDel.Parameters.AddWithValue("rota", reg.Rota ?? "");
                    cmdDel.Parameters.AddWithValue("apr", reg.Aprovado);
                    cmdDel.Parameters.AddWithValue("sep", reg.EmSeparacao);
                    cmdDel.Parameters.AddWithValue("cnf", reg.Conferido);
                    cmdDel.Parameters.AddWithValue("lib", reg.Liberado);
                    cmdDel.Parameters.AddWithValue("trv", reg.Travado);
                    cmdDel.Parameters.AddWithValue("fat", reg.EmFaturamento);
                    using var rd = await cmdDel.ExecuteReaderAsync().ConfigureAwait(false);
                    if (await rd.ReadAsync().ConfigureAwait(false))
                    {
                        linhas = 1;   // orcamento é PK: no máximo uma linha
                        reg = reg with
                        {
                            Cliente       = rd.GetString(0),
                            NomeFantasia  = rd.GetString(1),
                            Rota          = rd.GetString(2),
                            Aprovado      = rd.GetBoolean(3),
                            EmSeparacao   = rd.GetBoolean(4),
                            Conferido     = rd.GetBoolean(5),
                            Liberado      = rd.GetBoolean(6),
                            Travado       = rd.GetBoolean(7),
                            EmFaturamento = rd.GetBoolean(8),
                        };
                        reg = reg with
                        {
                            Status = ExclusaoRegistro.CalcularStatus(
                                reg.Aprovado, reg.EmSeparacao, reg.Conferido,
                                reg.Liberado, reg.Travado, reg.EmFaturamento, reg.PedidoVenda)
                        };
                    }
                }

                if (linhas == 0)
                {
                    // Nada apagado: a linha sumiu ou mudou de estado? Lido na mesma
                    // transação só para dar a mensagem certa ao operador.
                    bool existe;
                    using (var cmdExiste = conn.CreateCommand())
                    {
                        cmdExiste.Transaction = tx;
                        cmdExiste.CommandText = "SELECT 1 FROM pedidos_controlados WHERE orcamento = @orc";
                        cmdExiste.Parameters.AddWithValue("orc", reg.Orcamento);
                        existe = await cmdExiste.ExecuteScalarAsync().ConfigureAwait(false) != null;
                    }
                    await tx.RollbackAsync().ConfigureAwait(false);
                    return existe ? ResultadoExclusao.Alterado : ResultadoExclusao.JaExcluido;
                }

                using (var cmdIns = conn.CreateCommand())
                {
                    cmdIns.Transaction = tx;
                    cmdIns.CommandText = @"
                        INSERT INTO historico_exclusoes
                        (orcamento, cliente, nome_fantasia, rota, vendedor, pedido_venda,
                         nota_fiscal, status, aprovado, em_separacao, conferido, liberado,
                         travado, em_faturamento, tipo_exclusao, usuario_windows)
                        VALUES
                        (@orc, @cli, @nf, @rot, @vnd, @pv, @nfe, @sta,
                         @apr, @sep, @cnf, @lib, @trv, @fat, @tip, @usr)";
                    cmdIns.Parameters.AddWithValue("orc", reg.Orcamento);
                    cmdIns.Parameters.AddWithValue("cli", reg.Cliente ?? "");
                    cmdIns.Parameters.AddWithValue("nf",  reg.NomeFantasia ?? "");
                    cmdIns.Parameters.AddWithValue("rot", reg.Rota ?? "");
                    cmdIns.Parameters.AddWithValue("vnd", reg.Vendedor ?? "");
                    cmdIns.Parameters.AddWithValue("pv",  reg.PedidoVenda);
                    cmdIns.Parameters.AddWithValue("nfe", reg.NotaFiscal ?? "");
                    cmdIns.Parameters.AddWithValue("sta", reg.Status ?? "");
                    cmdIns.Parameters.AddWithValue("apr", reg.Aprovado);
                    cmdIns.Parameters.AddWithValue("sep", reg.EmSeparacao);
                    cmdIns.Parameters.AddWithValue("cnf", reg.Conferido);
                    cmdIns.Parameters.AddWithValue("lib", reg.Liberado);
                    cmdIns.Parameters.AddWithValue("trv", reg.Travado);
                    cmdIns.Parameters.AddWithValue("fat", reg.EmFaturamento);
                    cmdIns.Parameters.AddWithValue("tip", reg.TipoExclusao ?? "");
                    cmdIns.Parameters.AddWithValue("usr", Environment.UserName ?? "");
                    await cmdIns.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                await tx.CommitAsync().ConfigureAwait(false);
                await NotificarMudancaAsync("exclusao").ConfigureAwait(false);
                return ResultadoExclusao.Excluido;
            }
            catch
            {
                try { await tx.RollbackAsync().ConfigureAwait(false); } catch { /* swallow */ }
                throw;
            }
        }

        /// <summary>
        /// Lista todos os registros do historico de exclusoes ordenados
        /// por data de exclusao (mais recentes primeiro).
        /// </summary>
        public async Task<List<HistoricoExclusaoItem>> ListarHistoricoExclusoesAsync()
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            var resultado = await ExecutarControleAsync(async conn =>
            {
                var lista = new List<HistoricoExclusaoItem>();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, orcamento, cliente, nome_fantasia, rota, vendedor,
                           pedido_venda, nota_fiscal, status,
                           aprovado, em_separacao, conferido, liberado, travado, em_faturamento,
                           tipo_exclusao, usuario_windows, data_exclusao
                      FROM historico_exclusoes
                     ORDER BY data_exclusao DESC";
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    lista.Add(new HistoricoExclusaoItem
                    {
                        Id             = reader.GetInt32(0),
                        Orcamento      = reader.GetInt32(1),
                        Cliente        = reader.GetString(2),
                        NomeFantasia   = reader.GetString(3),
                        Rota           = reader.GetString(4),
                        Vendedor       = reader.GetString(5),
                        PedidoVenda    = reader.GetInt32(6),
                        NotaFiscal     = reader.GetString(7),
                        Status         = reader.GetString(8),
                        Aprovado       = reader.GetBoolean(9),
                        EmSeparacao    = reader.GetBoolean(10),
                        Conferido      = reader.GetBoolean(11),
                        Liberado       = reader.GetBoolean(12),
                        Travado        = reader.GetBoolean(13),
                        EmFaturamento  = reader.GetBoolean(14),
                        TipoExclusao   = reader.GetString(15),
                        UsuarioWindows = reader.GetString(16),
                        DataExclusao   = reader.GetDateTime(17),
                    });
                }
                return lista;
            }).ConfigureAwait(false);

            return resultado ?? new List<HistoricoExclusaoItem>();
        }

        /// <summary>
        /// DELETE destrutivo: remove definitivamente do historico_exclusoes
        /// todos os registros com data_exclusao anterior ao corte informado.
        /// Retorna o numero de linhas removidas.
        /// </summary>
        public async Task<int> LimparHistoricoExclusoesAntigoAsync(DateTime corte)
        {
            return await ExecutarControleComLinhasAsync(async conn =>
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM historico_exclusoes WHERE data_exclusao < @corte";
                cmd.Parameters.AddWithValue("corte", corte);
                return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Botão "Testar" do ConfigDialog: testa os DOIS bancos com a config digitada, sem
        /// alterar nada. Antes só o Integra era testado — erro no nome do banco de controle
        /// passava no teste. Banco de controle inexistente com o servidor acessível não é
        /// erro: <see cref="GarantirBancoControleAsync"/> o cria ao conectar.
        /// </summary>
        public async Task<ResultadoTesteConexao> TestarConexao(AppConfig configTeste)
        {
            var tarefaIntegra = TestarConexaoComErroAsync(configTeste.ConnectionStringIntegra);
            var tarefaControle = TestarConexaoComErroAsync(configTeste.ConnectionStringControle);
            string? erroIntegra = await tarefaIntegra.ConfigureAwait(false);
            string? erroControle = await tarefaControle.ConfigureAwait(false);

            bool controleSeraCriado = false;
            if (erroControle != null)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(configTeste.ConnectionStringServidor);
                    await conn.OpenAsync().ConfigureAwait(false);
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT 1 FROM pg_database WHERE datname = @nome";
                    cmd.Parameters.AddWithValue("nome", configTeste.BancoControle);
                    controleSeraCriado = await cmd.ExecuteScalarAsync().ConfigureAwait(false) == null;
                }
                catch
                {
                    // Servidor inacessível também pelo banco postgres: fica o erro original.
                }
            }

            return new ResultadoTesteConexao(erroIntegra, erroControle, controleSeraCriado);
        }

        private static async Task<bool> TestarConexaoAsync(string connectionString) =>
            await TestarConexaoComErroAsync(connectionString).ConfigureAwait(false) == null;

        /// <summary>Abre a conexão e roda SELECT 1. Devolve null se OK ou a mensagem de erro.</summary>
        private static async Task<string?> TestarConexaoComErroAsync(string connectionString)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private void TimerCallback(object? state)
        {
            _ = VerificarConexaoAsync();
        }

        private void NotificarStatus(StatusConexao statusIntegra, StatusConexao statusControle, string mensagem)
        {
            var args = new StatusConexaoEventArgs(statusIntegra, statusControle, mensagem);

            if (_syncContext != null)
                _syncContext.Post(_ => StatusAlterado?.Invoke(this, args), null);
            else
                StatusAlterado?.Invoke(this, args);
        }

        public void Dispose()
        {
            PararEscutaMudancas();
            _timer.Dispose();
        }
    }
}
