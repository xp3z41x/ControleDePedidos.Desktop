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
                Logger.Error("Exceção não tratada (AppDomain)", e.ExceptionObject as Exception);

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
