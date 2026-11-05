namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Logger rotativo, thread-safe, com escrita silenciosa (nunca derruba o app).
    /// Arquivos ficam em %LocalAppData%\CONTROLE DE PEDIDOS\logs\.
    /// Rotaciona ao atingir 1 MB. Mantem ate 5 arquivos historicos
    /// (app.log, app.1.log, app.2.log, app.3.log, app.4.log, app.5.log).
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CONTROLE DE PEDIDOS", "logs");

        private static readonly string LogPath = Path.Combine(LogDir, "app.log");

        private const long TamanhoMaxBytes = 1_048_576; // 1 MB
        private const int MaxRotacoes = 5;

        private static readonly object _lock = new();

        public static void Info(string mensagem) => Gravar("INFO", mensagem, null);
        public static void Warn(string mensagem, Exception? ex = null) => Gravar("WARN", mensagem, ex);
        public static void Error(string mensagem, Exception? ex = null) => Gravar("ERROR", mensagem, ex);

        private static void Gravar(string nivel, string mensagem, Exception? ex)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    RotacionarSeNecessario();

                    // Identifica o processo: no RDS há 10+ instâncias gravando no MESMO
                    // arquivo, e sem isso é impossível saber de qual sessão veio a linha.
                    var linha = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{nivel}] [{Environment.ProcessId}] {mensagem}";
                    // ex.ToString() inclui InnerException(s) — antes só a externa era gravada,
                    // e wrappers (AggregateException, IOException do config, TargetInvocation)
                    // escondiam a causa real.
                    if (ex != null)
                        linha += $"{Environment.NewLine}  -> {ex}";

                    EscreverComRetry(linha + Environment.NewLine);
                }
            }
            catch
            {
                // Nunca derrubar o app por falha de log.
            }
        }

        /// <summary>
        /// Grava permitindo que outros processos tenham o arquivo aberto. O lock acima é
        /// só intra-processo: com várias instâncias na mesma máquina (RDS), o
        /// File.AppendAllText original falhava por arquivo em uso e a linha era perdida
        /// em silêncio. Aqui compartilhamos o handle e ainda tentamos de novo.
        /// </summary>
        private static void EscreverComRetry(string texto)
        {
            const int tentativas = 8;
            for (int i = 0; i < tentativas; i++)
            {
                try
                {
                    using var fs = new FileStream(LogPath, FileMode.Append, FileAccess.Write,
                                                  FileShare.ReadWrite | FileShare.Delete);
                    using var sw = new StreamWriter(fs);
                    sw.Write(texto);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Windows lança as duas em disputa de arquivo entre processos.
                    if (i == tentativas - 1) return;   // desiste em silêncio: log nunca derruba o app
                    // Jitter no backoff: sem ele as instâncias que colidiram tentam de
                    // novo no mesmo instante e colidem outra vez.
                    Thread.Sleep(8 + i * 12 + Random.Shared.Next(0, 20));
                }
            }
        }

        private static void RotacionarSeNecessario()
        {
            if (!File.Exists(LogPath)) return;

            var info = new FileInfo(LogPath);
            if (info.Length < TamanhoMaxBytes) return;

            // Apaga o mais antigo, se atingiu o limite de rotacoes.
            var maisAntigo = Path.Combine(LogDir, $"app.{MaxRotacoes}.log");
            if (File.Exists(maisAntigo))
            {
                try { File.Delete(maisAntigo); } catch { /* arquivo em uso? ignora */ }
            }

            // Shift: app.(N-1).log -> app.N.log, ..., app.1.log -> app.2.log
            for (int i = MaxRotacoes - 1; i >= 1; i--)
            {
                var origem = Path.Combine(LogDir, $"app.{i}.log");
                var destino = Path.Combine(LogDir, $"app.{i + 1}.log");
                if (File.Exists(origem))
                {
                    try { File.Move(origem, destino); } catch { /* falha parcial - continua */ }
                }
            }

            // Move o atual: app.log -> app.1.log
            try { File.Move(LogPath, Path.Combine(LogDir, "app.1.log")); } catch { /* segue adiante */ }
        }
    }
}
