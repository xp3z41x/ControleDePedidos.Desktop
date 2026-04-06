using Npgsql;

namespace CONTROLE_DE_PEDIDOS
{
    public enum StatusConexao
    {
        Verificando,
        Conectado,
        Desconectado
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

        public event EventHandler<StatusConexaoEventArgs>? StatusAlterado;

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

        public void AtualizarConfig(AppConfig novaConfig)
        {
            _config = novaConfig;
            _config.Salvar();
            _bancoControleGarantido = false;
            _ = VerificarConexaoAsync();
        }

        public async Task<bool> VerificarConexaoAsync()
        {
            if (Interlocked.CompareExchange(ref _verificando, 1, 0) != 0)
                return Conectado;

            try
            {
                bool okIntegra = await TestarConexaoAsync(_config.ConnectionStringIntegra).ConfigureAwait(false);
                bool okControle = false;

                if (okIntegra)
                {
                    if (!_bancoControleGarantido)
                    {
                        await GarantirBancoControleAsync().ConfigureAwait(false);
                        _bancoControleGarantido = true;
                    }
                    okControle = await TestarConexaoAsync(_config.ConnectionStringControle).ConfigureAwait(false);
                }

                var novoIntegra = okIntegra ? StatusConexao.Conectado : StatusConexao.Desconectado;
                var novoControle = okControle ? StatusConexao.Conectado : StatusConexao.Desconectado;

                bool mudou = novoIntegra != _statusIntegra || novoControle != _statusControle;
                _statusIntegra = novoIntegra;
                _statusControle = novoControle;

                if (mudou)
                {
                    string mensagem;
                    if (okIntegra && okControle)
                        mensagem = $"Conectado - {_config.ServidorIP}";
                    else if (okIntegra)
                        mensagem = $"Parcial - {_config.ServidorIP} (sem {_config.BancoControle})";
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

        private async Task GarantirBancoControleAsync()
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
                    cmdCreate.CommandText = $"CREATE DATABASE \"{_config.BancoControle}\" ENCODING 'UTF8'";
                    await cmdCreate.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _bancoControleGarantido = false;
                System.Diagnostics.Debug.WriteLine($"Erro ao garantir a existência do banco de controle: {ex}");
            }
        }

        public async Task GarantirTabelasAsync()
        {
            if (!ControleConectado) return;

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
                    ALTER TABLE pedidos_controlados ADD COLUMN IF NOT EXISTS em_faturamento BOOLEAN NOT NULL DEFAULT FALSE";
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
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

        public async Task<int> ExecutarControleComLinhasAsync(Func<NpgsqlConnection, Task<int>> operacao)
        {
            if (!ControleConectado)
                throw new InvalidOperationException($"Sem conexão com o banco {_config.BancoControle}.");

            using var conn = new NpgsqlConnection(_config.ConnectionStringControle);
            await conn.OpenAsync().ConfigureAwait(false);
            return await operacao(conn).ConfigureAwait(false);
        }

        public async Task<bool> TestarConexao(AppConfig configTeste)
        {
            try
            {
                return await TestarConexaoAsync(configTeste.ConnectionStringIntegra).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TestarConexaoAsync(string connectionString)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync().ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
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
            _timer.Dispose();
        }
    }
}
