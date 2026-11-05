namespace CONTROLE_DE_PEDIDOS
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Captura excecoes nao tratadas para o log rotativo.
            Application.ThreadException += (_, e) => TratarExcecaoUi(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Logger.Error($"Exceção não tratada (AppDomain, encerrando={e.IsTerminating})"
                             + (e.ExceptionObject is Exception ? "" : $": {e.ExceptionObject}"),
                             e.ExceptionObject as Exception);
            // Fire-and-forget (_ = XxxAsync()) é padrão no app: sem isto, uma falha numa
            // dessas Tasks sumia sem deixar rastro no log (no .NET Core ela não derruba o
            // processo, só é descartada quando o GC finaliza a Task).
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Logger.Warn("Exceção em Task não observada", e.Exception);
                e.SetObserved();
            };

            Logger.Info("=== Aplicação iniciada ===");

            // HighDpiMode vem do csproj (<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>).
            ApplicationConfiguration.Initialize();
            ColumnWidthStore.Carregar();
            FilterStore.Carregar();
            Application.ApplicationExit += (_, _) =>
            {
                ColumnWidthStore.Flush();
                FilterStore.Flush();
                Logger.Info("=== Aplicação encerrada ===");
            };
            Application.Run(new Form1());
        }

        // ================================================================
        // EXCEÇÕES NA THREAD DE UI
        // ================================================================

        /// <summary>Mesmo caminho do Logger (que não o expõe) — só para exibir ao operador.</summary>
        private static readonly string CaminhoLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CONTROLE DE PEDIDOS", "logs", "app.log");

        /// <summary>A mesma exceção repetida dentro desta janela (contada a partir do fechamento da caixa) não reabre a caixa.</summary>
        private static readonly TimeSpan JanelaRepeticao = TimeSpan.FromSeconds(30);

        private static bool _caixaErroAberta;
        private static string? _assinaturaUltimoErro;
        private static DateTime _ultimoErroUtc;

        /// <summary>
        /// Loga e avisa o operador. Antes só logava: a ação simplesmente "não acontecia"
        /// e ninguém sabia por quê. Anti-spam: no máximo uma caixa aberta (ou agendada)
        /// por vez — as exceções que chegarem nesse meio-tempo só vão para o log — e a
        /// mesma exceção em rajada (ex.: falha num Paint/timer) não reabre a caixa.
        /// </summary>
        private static void TratarExcecaoUi(Exception ex)
        {
            Logger.Error("Exceção não tratada (UI thread)", ex);

            if (_caixaErroAberta) return;

            string assinatura = $"{ex.GetType().FullName}|{ex.Message}|{ex.TargetSite}";
            var agora = DateTime.UtcNow;
            if (assinatura == _assinaturaUltimoErro && agora - _ultimoErroUtc < JanelaRepeticao)
            {
                _ultimoErroUtc = agora;   // rajada contínua continua suprimida
                return;
            }
            _assinaturaUltimoErro = assinatura;

            string mensagem =
                "Ocorreu um erro inesperado e a última operação pode não ter sido concluída.\n\n" +
                $"{ex.GetType().Name}: {ex.Message}\n\n" +
                "O sistema continua aberto. Se o problema persistir, informe o suporte e " +
                "envie o arquivo de log:\n" + CaminhoLog;

            _caixaErroAberta = true;
            // A caixa NÃO pode abrir dentro deste handler: o WinForms descarta (sem chamar
            // o handler, logo sem log) toda exceção que ocorre enquanto o ThreadException
            // ainda está em execução — e o loop modal do MessageBox continua rodando
            // timers/eventos. Verificado com harness: um erro lançado com a caixa aberta
            // sumia por completo. Postando, o handler retorna na hora e as próximas
            // exceções chegam aqui (e vão para o log) enquanto a caixa está aberta.
            var contexto = SynchronizationContext.Current;
            if (contexto != null)
                contexto.Post(_ => MostrarCaixaErro(mensagem), null);
            else
                MostrarCaixaErro(mensagem);
        }

        private static void MostrarCaixaErro(string mensagem)
        {
            try
            {
                var owner = OwnerSeguro();
                if (owner != null)
                    MessageBox.Show(owner, mensagem, "Erro inesperado",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(mensagem, "Erro inesperado",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception exCaixa)
            {
                Logger.Warn("Falha ao exibir a caixa de erro inesperado", exCaixa);
            }
            finally
            {
                _caixaErroAberta = false;
                _ultimoErroUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Form ativo que pode servir de owner (o diálogo modal aberto, se houver — ver
        /// invariante de owner). Sem um form utilizável, null: MessageBox sem owner é
        /// melhor que uma caixa presa a um form em descarte ou de outra thread.
        /// </summary>
        private static Form? OwnerSeguro()
        {
            var form = Form.ActiveForm;
            if (form == null || form.IsDisposed || form.Disposing || !form.IsHandleCreated
                || !form.Visible || form.InvokeRequired)
                return null;
            return form;
        }
    }
}
