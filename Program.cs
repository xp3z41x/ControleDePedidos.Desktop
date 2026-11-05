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
            Application.ThreadException += (_, e) =>
                Logger.Error("Exceção não tratada (UI thread)", e.Exception);
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
    }
}
