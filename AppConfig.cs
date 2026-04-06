namespace CONTROLE_DE_PEDIDOS
{
    public class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public string ServidorIP { get; set; } = "127.0.0.1";
        public string Porta { get; set; } = "5432";
        public string Usuario { get; set; } = "postgres";
        public string Senha { get; set; } = "pg@atualDev";
        public string BancoIntegra { get; set; } = "integrapgsql";
        public string BancoControle { get; set; } = "controlepedidos";
        public int Filial { get; set; } = 5;
        public int OrcamentoMinimo { get; set; } = 0;
        public int FilialFiscal { get; set; } = 0;

        // SOMENTE LEITURA - opcoes forcam read-only no nivel da sessao PostgreSQL
        public string ConnectionStringIntegra =>
            $"Host={ServidorIP};Port={Porta};Database={BancoIntegra};Username={Usuario};Password={Senha};Timeout=5;SSL Mode=Prefer;Options=-c default_transaction_read_only=on";

        public string ConnectionStringControle =>
            $"Host={ServidorIP};Port={Porta};Database={BancoControle};Username={Usuario};Password={Senha};Timeout=5;SSL Mode=Prefer";

        public string ConnectionStringServidor =>
            $"Host={ServidorIP};Port={Porta};Database=postgres;Username={Usuario};Password={Senha};Timeout=5;SSL Mode=Prefer";

        public void Salvar()
        {
            var lines = new List<string>
            {
                "[Servidor]",
                $"IP={ServidorIP}",
                $"Porta={Porta}",
                $"Usuario={Usuario}",
                $"Senha={Senha}",
                "",
                "[Bancos]",
                $"BancoIntegra={BancoIntegra}",
                $"BancoControle={BancoControle}",
                "",
                "[Filtros]",
                $"Filial={Filial}",
                $"OrcamentoMinimo={OrcamentoMinimo}",
                "",
                "[Fiscal]",
                $"FilialFiscal={FilialFiscal}"
            };
            File.WriteAllLines(ConfigPath, lines);
        }

        public static AppConfig Carregar()
        {
            var config = new AppConfig();

            if (!File.Exists(ConfigPath))
            {
                config.Salvar();
                return config;
            }

            foreach (var line in File.ReadAllLines(ConfigPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                    continue;

                var parts = line.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "IP": config.ServidorIP = value; break;
                    case "Porta": config.Porta = value; break;
                    case "Usuario": config.Usuario = value; break;
                    case "Senha": config.Senha = value; break;
                    case "BancoIntegra": config.BancoIntegra = value; break;
                    case "BancoControle": config.BancoControle = value; break;
                    case "Filial": if (int.TryParse(value, out var filial)) config.Filial = filial; break;
                    case "OrcamentoMinimo": if (int.TryParse(value, out var orcMin)) config.OrcamentoMinimo = orcMin; break;
                    case "FilialFiscal": if (int.TryParse(value, out var filialFiscal)) config.FilialFiscal = filialFiscal; break;
                    // compatibilidade com config antiga
                    case "BancoDados": config.BancoIntegra = value; break;
                }
            }

            return config;
        }
    }
}
