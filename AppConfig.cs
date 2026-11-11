namespace CONTROLE_DE_PEDIDOS
{
    public class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");

        public string ServidorIP { get; set; } = "127.0.0.1";
        public string Porta { get; set; } = "5432";
        public string Usuario { get; set; } = "postgres";
        public string Senha { get; set; } = "";
        public string BancoIntegra { get; set; } = "integrapgsql";
        public string BancoControle { get; set; } = "controlepedidos";
        public int Filial { get; set; } = 1;
        public int OrcamentoMinimo { get; set; } = 0;
        public int FilialFiscal { get; set; } = 1;
        public bool SeparacaoPaisagem { get; set; } = false;
        public bool SeparacaoOcultarCancelados { get; set; } = true;
        public bool SeparacaoOrdenarPorLocal { get; set; } = false;
        public bool SeparacaoCasasDecimais { get; set; } = false;
        public bool CopiaPaisagem { get; set; } = false;
        public bool CopiaOrdenarAlfabetica { get; set; } = false;
        /// <summary>Cópia de Pedido COM desconto: imprime a % (no unitário e no total) e o
        /// aviso de arredondamento. Desligado = só os valores em reais.</summary>
        public bool CopiaExibirPercentualDesconto { get; set; } = true;

        // Dados da empresa impressos no cabecalho da Copia de Pedido.
        // Texto livre; campo vazio e simplesmente omitido do relatorio.
        public string EmpresaRazaoSocial { get; set; } = "";
        public string EmpresaCnpj { get; set; } = "";
        public string EmpresaInscricaoEstadual { get; set; } = "";
        public string EmpresaEndereco { get; set; } = "";
        public string EmpresaCidadeUfCep { get; set; } = "";
        public string EmpresaTelefone { get; set; } = "";
        public string EmpresaEmail { get; set; } = "";
        public string EmpresaWhatsApp { get; set; } = "";

        // SOMENTE LEITURA - opcoes forcam read-only no nivel da sessao PostgreSQL
        public string ConnectionStringIntegra =>
            MontarConnectionString(BancoIntegra, somenteLeitura: true);

        // ESCRITA EXCEPCIONAL - usar apenas no fluxo "Corrigir Observação".
        public string ConnectionStringIntegraEscrita =>
            MontarConnectionString(BancoIntegra, somenteLeitura: false);

        public string ConnectionStringControle =>
            MontarConnectionString(BancoControle, somenteLeitura: false);

        public string ConnectionStringServidor =>
            MontarConnectionString("postgres", somenteLeitura: false);

        /// <summary>
        /// Monta a connection string pelo builder do Npgsql, que escapa cada valor.
        /// Interpolar direto quebrava com ';' / '=' / aspas na senha (ou no usuário): o
        /// trecho após o ';' virava OUTRO parâmetro — inclusive podendo sobrescrever
        /// o Options do read-only do Integra. Nunca lança: porta inválida (só possível
        /// editando o INI à mão; o ConfigDialog valida) cai no padrão 5432.
        /// </summary>
        private string MontarConnectionString(string database, bool somenteLeitura)
        {
            var csb = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = ServidorIP,
                Port = int.TryParse(Porta, out var porta) && porta is >= 1 and <= 65535 ? porta : 5432,
                Database = database,
                Username = Usuario,
                Password = Senha,
                Timeout = 5,
                SslMode = Npgsql.SslMode.Prefer
            };
            if (somenteLeitura)
                csb.Options = "-c default_transaction_read_only=on";
            return csb.ConnectionString;
        }

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
                $"FilialFiscal={FilialFiscal}",
                "",
                "[Separacao]",
                $"SeparacaoPaisagem={SeparacaoPaisagem}",
                $"SeparacaoOcultarCancelados={SeparacaoOcultarCancelados}",
                $"SeparacaoOrdenarPorLocal={SeparacaoOrdenarPorLocal}",
                $"SeparacaoCasasDecimais={SeparacaoCasasDecimais}",
                "",
                "[CopiaPedido]",
                $"CopiaPaisagem={CopiaPaisagem}",
                $"CopiaOrdenarAlfabetica={CopiaOrdenarAlfabetica}",
                $"CopiaExibirPercentualDesconto={CopiaExibirPercentualDesconto}",
                "",
                "[Empresa]",
                $"EmpresaRazaoSocial={EmpresaRazaoSocial}",
                $"EmpresaCnpj={EmpresaCnpj}",
                $"EmpresaInscricaoEstadual={EmpresaInscricaoEstadual}",
                $"EmpresaEndereco={EmpresaEndereco}",
                $"EmpresaCidadeUfCep={EmpresaCidadeUfCep}",
                $"EmpresaTelefone={EmpresaTelefone}",
                $"EmpresaEmail={EmpresaEmail}",
                $"EmpresaWhatsApp={EmpresaWhatsApp}"
            };
            try
            {
                // Atômico: o config.ini fica ao lado do EXE e é lido por TODAS as sessões
                // do RDS. WriteAllLines truncava e reescrevia no lugar — uma instância
                // abrindo nesse instante recebia IOException (o app nem subia) ou lia
                // um arquivo incompleto (chaves faltando = padrões, ex.: Filial=1).
                ArquivoAtomico.GravarTexto(ConfigPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                throw new IOException($"Não foi possível gravar '{ConfigPath}': {ex.Message}", ex);
            }
        }

        public static AppConfig Carregar()
        {
            var config = new AppConfig();

            if (!File.Exists(ConfigPath))
            {
                try { config.Salvar(); }
                catch { /* primeiro acesso sem disco/permissao - segue com padrao em memoria */ }
                return config;
            }

            foreach (var line in ArquivoAtomico.LerLinhas(ConfigPath))
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
                    // Sem Trim: espaços nas pontas fazem parte da senha (antes eram cortados
                    // e a senha ficava impossível de usar). O Salvar grava "Senha=<valor>".
                    case "Senha": config.Senha = parts[1]; break;
                    case "BancoIntegra": config.BancoIntegra = value; break;
                    case "BancoControle": config.BancoControle = value; break;
                    case "Filial": if (int.TryParse(value, out var filial)) config.Filial = filial; break;
                    case "OrcamentoMinimo": if (int.TryParse(value, out var orcMin)) config.OrcamentoMinimo = orcMin; break;
                    case "FilialFiscal": if (int.TryParse(value, out var filialFiscal)) config.FilialFiscal = filialFiscal; break;
                    case "SeparacaoPaisagem": if (bool.TryParse(value, out var sepPais)) config.SeparacaoPaisagem = sepPais; break;
                    case "SeparacaoOcultarCancelados": if (bool.TryParse(value, out var sepCanc)) config.SeparacaoOcultarCancelados = sepCanc; break;
                    case "SeparacaoOrdenarPorLocal": if (bool.TryParse(value, out var sepLocal)) config.SeparacaoOrdenarPorLocal = sepLocal; break;
                    case "SeparacaoCasasDecimais": if (bool.TryParse(value, out var sepDec)) config.SeparacaoCasasDecimais = sepDec; break;
                    case "CopiaPaisagem": if (bool.TryParse(value, out var copPais)) config.CopiaPaisagem = copPais; break;
                    case "CopiaOrdenarAlfabetica": if (bool.TryParse(value, out var copAlfa)) config.CopiaOrdenarAlfabetica = copAlfa; break;
                    case "CopiaExibirPercentualDesconto": if (bool.TryParse(value, out var copPct)) config.CopiaExibirPercentualDesconto = copPct; break;
                    case "EmpresaRazaoSocial": config.EmpresaRazaoSocial = value; break;
                    case "EmpresaCnpj": config.EmpresaCnpj = value; break;
                    case "EmpresaInscricaoEstadual": config.EmpresaInscricaoEstadual = value; break;
                    case "EmpresaEndereco": config.EmpresaEndereco = value; break;
                    case "EmpresaCidadeUfCep": config.EmpresaCidadeUfCep = value; break;
                    case "EmpresaTelefone": config.EmpresaTelefone = value; break;
                    case "EmpresaEmail": config.EmpresaEmail = value; break;
                    case "EmpresaWhatsApp": config.EmpresaWhatsApp = value; break;
                    // compatibilidade com config antiga
                    case "BancoDados": config.BancoIntegra = value; break;
                }
            }

            return config;
        }
    }

    /// <summary>
    /// Leitura/gravação de arquivos pequenos compartilhados entre instâncias (RDS).
    /// A gravação vai para um temporário e substitui o destino de uma vez: quem lê vê o
    /// arquivo antigo ou o novo, nunca um pela metade. Tentativas curtas absorvem a
    /// disputa momentânea com outra instância lendo/gravando o mesmo arquivo.
    /// </summary>
    internal static class ArquivoAtomico
    {
        private const int Tentativas = 10;   // ~1,5 s no pior caso

        public static void GravarTexto(string caminho, string conteudo)
        {
            var temp = $"{caminho}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temp, conteudo, new System.Text.UTF8Encoding(false));
                for (int i = 0; ; i++)
                {
                    try
                    {
                        File.Move(temp, caminho, overwrite: true);
                        return;
                    }
                    catch (Exception ex) when (i < Tentativas - 1
                                               && ex is IOException or UnauthorizedAccessException)
                    {
                        Thread.Sleep(15 + i * 25 + Random.Shared.Next(0, 20));
                    }
                }
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* sobra .tmp inofensiva */ }
            }
        }

        public static string LerTexto(string caminho)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read,
                                                  FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);
                    return sr.ReadToEnd();
                }
                catch (Exception ex) when (i < Tentativas - 1
                                           && ex is IOException or UnauthorizedAccessException
                                           && ex is not FileNotFoundException and not DirectoryNotFoundException)
                {
                    Thread.Sleep(15 + i * 25 + Random.Shared.Next(0, 20));
                }
            }
        }

        public static string[] LerLinhas(string caminho) =>
            LerTexto(caminho).ReplaceLineEndings("\n").Split('\n');
    }
}
