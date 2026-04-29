using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Helper para garantir que documentos A4 sejam impressos corretamente
    /// independentemente da impressora padrão do Windows do usuário
    /// (que pode ser térmica, de etiqueta, ou outra que não suporta A4).
    ///
    /// Por padrão, <see cref="PrintDocument"/> herda <see cref="PrinterSettings"/>
    /// da impressora default do Windows. Se essa for uma térmica 10x15, o preview
    /// e a impressão calculam layout para 4x6 polegadas — quebrando relatórios A4.
    /// Esta classe pré-seleciona uma impressora A4-capable no construtor da PrintDoc.
    /// </summary>
    public static class PrintingHelper
    {
        // Heurística para excluir impressoras claramente não-A4 (térmicas, etiqueta, POS).
        private static readonly string[] PalavrasTermicas =
            ["thermal", "térmica", "termica", "pos ", "label", "zebra",
             "bematech", "epson tm", "daruma", "elgin", "10x15", "4x6"];

        /// <summary>
        /// PaperSize padrão A4: 210x297mm = 827x1169 centésimos de polegada.
        /// </summary>
        public static PaperSize CriarPaperSizeA4() =>
            new("A4", 827, 1169) { RawKind = (int)PaperKind.A4 };

        /// <summary>
        /// Aplica configurações A4 (paper size, landscape, margins) ao documento
        /// E seleciona a primeira impressora instalada que suporte A4, evitando
        /// térmicas/etiquetadoras quando possível. Idempotente — pode ser chamado
        /// várias vezes (ex: no construtor + após PrintDialog).
        /// </summary>
        public static void AplicarConfigA4(PrintDocument doc, bool landscape)
        {
            var nomeImpressoraA4 = EncontrarImpressoraA4Capaz();
            if (nomeImpressoraA4 != null)
            {
                doc.PrinterSettings.PrinterName = nomeImpressoraA4;
            }
            else
            {
                Logger.Warn("Nenhuma impressora A4-capable encontrada. Usando impressora padrão do Windows.");
            }

            // Trocar PrinterName recarrega DefaultPageSettings — sempre re-aplicar nossos defaults
            doc.DefaultPageSettings.PaperSize = CriarPaperSizeA4();
            doc.DefaultPageSettings.Landscape = landscape;
            doc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        }

        /// <summary>
        /// Retorna o nome da primeira impressora instalada que suporta A4,
        /// preferindo as que NÃO parecem ser térmicas/etiquetadoras.
        /// Null se nenhuma suporta.
        /// </summary>
        public static string? EncontrarImpressoraA4Capaz()
        {
            string? primeiraA4 = null;
            foreach (string nome in PrinterSettings.InstalledPrinters)
            {
                try
                {
                    var ps = new PrinterSettings { PrinterName = nome };
                    bool suportaA4 = ps.PaperSizes.Cast<PaperSize>()
                        .Any(p => p.Kind == PaperKind.A4);
                    if (!suportaA4) continue;

                    primeiraA4 ??= nome;

                    bool pareceTermica = PalavrasTermicas.Any(palavra =>
                        nome.Contains(palavra, StringComparison.OrdinalIgnoreCase));
                    if (!pareceTermica) return nome;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Falha ao consultar impressora '{nome}': {ex.Message}");
                }
            }
            return primeiraA4; // pode ser null se nada suporta A4
        }

        /// <summary>
        /// Retorna true se a impressora atualmente atribuída ao documento
        /// declara suporte a A4 nas suas PaperSizes.
        /// </summary>
        public static bool ImpressoraSuportaA4(PrintDocument doc)
        {
            try
            {
                return doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
                    .Any(p => p.Kind == PaperKind.A4);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Loga estado de impressão para diagnóstico (printer name, paper size,
        /// margens, orientação). Chamar antes de Print() ou Preview.
        /// </summary>
        public static void LogarEstado(PrintDocument doc, string contexto)
        {
            try
            {
                var ps = doc.DefaultPageSettings.PaperSize;
                var m = doc.DefaultPageSettings.Margins;
                Logger.Info(
                    $"{contexto} | Printer='{doc.PrinterSettings.PrinterName}' | " +
                    $"PaperKind={ps.Kind} ({ps.Width}x{ps.Height}) | " +
                    $"Landscape={doc.DefaultPageSettings.Landscape} | " +
                    $"Margins=L{m.Left},R{m.Right},T{m.Top},B{m.Bottom}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao logar estado de impressão: {ex.Message}");
            }
        }
    }
}
