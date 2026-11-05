using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    public class SeparacaoCabecalho
    {
        public int Orcamento { get; set; }
        public string VendedorCodigo { get; set; } = "";
        public string VendedorNome { get; set; } = "";
        public string Observacao { get; set; } = "";
        public string ClienteCodigo { get; set; } = "";
        public string ClienteNome { get; set; } = "";
        public string Fantasia { get; set; } = "";
        public string Endereco { get; set; } = "";
        public string Cidade { get; set; } = "";
        public string Estado { get; set; } = "";
        /// <summary>estorc.data_inc + hora_inc já formatados ("dd/MM/yyyy HH:MM"); "—" quando ausentes.</summary>
        public string DataPedido { get; set; } = "";
    }

    public class SeparacaoItem
    {
        public int Item { get; set; }
        public string Matricula { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Referencia { get; set; } = "";
        public string Marca { get; set; } = "";
        public decimal Quantidade { get; set; }
        public string Local { get; set; } = "";
        public decimal EstoqueFisico { get; set; }
        public decimal SaldoAtual { get; set; }
        /// <summary>Unidade de medida do produto (UN, PR, PA, KT...), de cadpro.unidade.</summary>
        public string Unidade { get; set; } = "";
    }

    public class RelatorioSeparacaoPrintDoc : PrintDocument
    {
        private readonly SeparacaoCabecalho _cab;
        private readonly List<SeparacaoItem> _itens;
        private readonly bool _casasDecimais;
        private int _indiceItem;
        private int _numeroPagina;
        private bool _impressaoReal;   // REGRA DE NEGÓCIO: dispara o avanço de status
        private bool _preview;         // MODO DE RENDERIZAÇÃO: separado de propósito —
                                       // layout não pode depender de regra de negócio
        private PrintingHelper.GeometriaPagina? _geo;

        /// <summary>Disparado uma vez após impressão física bem-sucedida (preview não conta).</summary>
        public event Action? ImpressaoRealConcluida;

        private static readonly Font FontTitulo = new("Segoe UI Semibold", 14f);
        private static readonly Font FontSubtitulo = new("Segoe UI Semibold", 10f);
        private static readonly Font FontCabecalho = new("Segoe UI", 8.5f);
        private static readonly Font FontHeader = new("Segoe UI Semibold", 7.5f);
        private static readonly Font FontDados = new("Segoe UI", 7.5f);
        private static readonly Font FontAssinatura = new("Segoe UI", 8f);

        // Data/hora REGISTRADA NO INTEGRA (não o relógio da impressão) — ver _cab.DataPedido.
        private const string RotuloData = "Data:";

        // Barcode Code 128-C: módulo 15 mil (~0,38mm) e altura 45 (1/100")
        private const float BarcodeModulo = 1.5f;
        private const float BarcodeAltura = 45f;
        private const float QuietZone = 15f;

        // Proporções das colunas (soma = 100%)
        // QTD com 6%: comporta "2000,00" (quantidade de 4 dígitos com casas decimais)
        private static readonly float[] ColPercent = [3.5f, 5f, 10f, 29f, 10f, 8f, 9f, 6f, 4.5f, 7.5f, 7.5f];
        private static readonly string[] ColHeaders = ["", "ITEM", "MATRÍCULA", "DESCRIÇÃO", "REFERÊNCIA", "MARCA", "LOCAL", "QTD", "UN", "FÍSICO", "SALDO"];
        private static readonly StringAlignment[] ColAlign =
        [
            StringAlignment.Center, StringAlignment.Center, StringAlignment.Near, StringAlignment.Near,
            StringAlignment.Near, StringAlignment.Near, StringAlignment.Near,
            StringAlignment.Far, StringAlignment.Center, StringAlignment.Far, StringAlignment.Far
        ];

        public RelatorioSeparacaoPrintDoc(SeparacaoCabecalho cab, List<SeparacaoItem> itens, bool paisagem, bool casasDecimais)
        {
            _cab = cab;
            _itens = itens;
            _casasDecimais = casasDecimais;
            // NÃO trocar para true: o ResetTransform em GeometriaPagina.PrepararPagina
            // anularia a compensação do controller, e o preview do WinForms passaria a
            // aplicar -hardMargin sobre um metafile cuja origem já é o canto do papel.
            // O alinhamento é feito manualmente ali.
            OriginAtMargins = false;
            PrintingHelper.AplicarConfigA4(this, landscape: paisagem);
            // Margens reduzidas (6,4mm) para caber o máximo de produtos por folha;
            // dentro da área imprimível típica de impressoras laser/jato de tinta.
            DefaultPageSettings.Margins = new Margins(25, 25, 25, 25);
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            base.OnBeginPrint(e);
            _indiceItem = 0;
            _numeroPagina = 0;
            // Preview renderiza com PrintToPreview; impressão física (inclusive
            // "Microsoft Print to PDF") chega como PrintToPrinter/PrintToFile.
            _impressaoReal = e.PrintAction is PrintAction.PrintToPrinter or PrintAction.PrintToFile;
            _preview = e.PrintAction == PrintAction.PrintToPreview;
            // Uma medição por JOB (cada leitura cria um DC de impressora). OnBeginPrint
            // roda DEPOIS do PrintDialog, então já reflete a impressora escolhida.
            _geo = PrintingHelper.GeometriaPagina.Medir(DefaultPageSettings);
        }

        protected override void OnEndPrint(PrintEventArgs e)
        {
            base.OnEndPrint(e);
            if (_impressaoReal && !e.Cancel)
                ImpressaoRealConcluida?.Invoke();
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            base.OnPrintPage(e);
            _numeroPagina++;

            var g = e.Graphics!;
            _geo ??= PrintingHelper.GeometriaPagina.Medir(e.PageSettings);
            // Faz ResetTransform + PageUnit + AntiAlias, alinha a origem ao canto do
            // PAPEL (preview e impressão real ficam idênticos) e recorta a área para
            // dentro do que a impressora consegue marcar. Ver PrintingHelper.GeometriaPagina.
            var area = _geo.PrepararPagina(e, _preview);
            if (_numeroPagina == 1)
                PrintingHelper.LogarPagina(e, "Separação", area, _geo, _preview);

            float x = area.Left;
            float y = area.Top;
            float larguraTotal = area.Width;

            float[] colW = new float[ColPercent.Length];
            for (int i = 0; i < ColPercent.Length; i++)
                colW[i] = larguraTotal * ColPercent[i] / 100f;

            const float alturaLinhaMin = 18f;
            const float alturaHeaderTabela = 22f;
            const float paddingCelula = 4f;

            using var penBorda = new Pen(Color.Black, 1.2f);
            using var penLinha = new Pen(Color.FromArgb(180, 180, 180), 0.6f);

            if (_numeroPagina == 1)
            {
                y = DesenharCabecalhoCompleto(g, penBorda, x, y, larguraTotal);
                y = DesenharAssinaturas(g, penBorda, x, y, larguraTotal);
            }
            else
            {
                y = DesenharCabecalhoResumido(g, penBorda, x, y, larguraTotal);
            }

            // === TABELA DE ITENS ===
            float tabelaTop = y;
            g.DrawLine(penBorda, x, tabelaTop, x + larguraTotal, tabelaTop);
            RelatorioTabela.DrawRow(g, colW, ColHeaders, FontHeader, x, y, alturaHeaderTabela, paddingCelula, ColAlign, quebrarLinha: false);
            y += alturaHeaderTabela;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 1;

            string fmtNumero = _casasDecimais ? "0.00" : "0";
            float limiteY = area.Bottom;

            int itensNestaPagina = 0;
            while (_indiceItem < _itens.Count)
            {
                var item = _itens[_indiceItem];
                string[] valores =
                [
                    "", // checkbox desenhado abaixo
                    item.Item.ToString(),
                    item.Matricula,
                    item.Descricao,
                    item.Referencia,
                    item.Marca,
                    item.Local,
                    item.Quantidade.ToString(fmtNumero),
                    item.Unidade,
                    item.EstoqueFisico.ToString(fmtNumero),
                    item.SaldoAtual.ToString(fmtNumero)
                ];

                // Altura dinâmica: dados completos, com quebra de linha por coluna
                float alturaLinha = RelatorioTabela.MedirAlturaLinha(g, colW, valores, FontDados, paddingCelula, alturaLinhaMin);
                // Sempre desenhar ao menos UMA linha por página: se a área útil ficar
                // menor que uma linha, quebrar aqui deixaria HasMorePages=true para
                // sempre — job infinito cuspindo folhas em branco.
                if (y + alturaLinha > limiteY && itensNestaPagina > 0)
                    break;

                itensNestaPagina++;
                _indiceItem++;

                // Coluna 0: quadrado vazio para o separador marcar de caneta
                const float box = 11f;
                g.DrawRectangle(penBorda,
                    x + (colW[0] - box) / 2f, y + (alturaLinha - box) / 2f, box, box);

                RelatorioTabela.DrawRow(g, colW, valores, FontDados, x, y, alturaLinha, paddingCelula, ColAlign, quebrarLinha: true);
                y += alturaLinha;
                g.DrawLine(penLinha, x, y, x + larguraTotal, y);
            }

            // Bordas da tabela
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            g.DrawLine(penBorda, x, tabelaTop, x, y);
            g.DrawLine(penBorda, x + larguraTotal, tabelaTop, x + larguraTotal, y);
            float vx = x;
            for (int i = 0; i < colW.Length - 1; i++)
            {
                vx += colW[i];
                g.DrawLine(penLinha, vx, tabelaTop, vx, y);
            }

            e.HasMorePages = _indiceItem < _itens.Count;
        }

        private float DesenharCabecalhoCompleto(Graphics g, Pen penBorda, float x, float y, float larguraTotal)
        {
            string digitos = _cab.Orcamento.ToString();
            float bcLargura = Code128.MedirLargura(digitos, BarcodeModulo);
            float bcX = x + larguraTotal - bcLargura - QuietZone;

            // Título e número do pedido à esquerda (sem invadir a área do barcode)
            float larguraTexto = bcX - QuietZone - x;
            g.DrawString("SEPARAÇÃO DE MERCADORIAS", FontTitulo, Brushes.Black,
                new RectangleF(x, y, larguraTexto, 28));
            g.DrawString($"PEDIDO/ORÇAMENTO: {_cab.Orcamento}", FontSubtitulo, Brushes.Black,
                new RectangleF(x, y + 30, larguraTexto, 20));
            g.DrawString($"{RotuloData} {_cab.DataPedido}", FontCabecalho, Brushes.Black,
                new RectangleF(x, y + 52, larguraTexto, 16));

            // Barcode à direita com número humano-legível abaixo
            Code128.Desenhar(g, digitos, bcX, y, BarcodeModulo, BarcodeAltura);
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString(digitos, FontCabecalho, Brushes.Black,
                new RectangleF(bcX, y + BarcodeAltura + 2, bcLargura, 16), sfCenter);

            y += 68;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 5;

            // Dados do vendedor e do cliente
            using var sfLinha = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            string vendedor = string.IsNullOrWhiteSpace(_cab.VendedorNome)
                ? _cab.VendedorCodigo
                : $"{_cab.VendedorCodigo} — {_cab.VendedorNome}";
            g.DrawString($"VENDEDOR: {vendedor}", FontCabecalho, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 16), sfLinha);
            y += 16;

            string cliente = $"CLIENTE: {_cab.ClienteCodigo} — {_cab.ClienteNome}";
            if (!string.IsNullOrWhiteSpace(_cab.Fantasia))
                cliente += $"  ({_cab.Fantasia})";
            g.DrawString(cliente, FontCabecalho, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 16), sfLinha);
            y += 16;

            string cidadeUf = string.Join("/", new[] { _cab.Cidade, _cab.Estado }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            g.DrawString($"ENDEREÇO: {_cab.Endereco}", FontCabecalho, Brushes.Black,
                new RectangleF(x, y, larguraTotal * 0.62f, 16), sfLinha);
            using var sfDireita = new StringFormat(sfLinha) { Alignment = StringAlignment.Far };
            g.DrawString($"CIDADE/UF: {cidadeUf}", FontCabecalho, Brushes.Black,
                new RectangleF(x + larguraTotal * 0.62f, y, larguraTotal * 0.38f, 16), sfDireita);
            y += 16;

            // Observação com wrap em até 3 linhas. Teto pela MÉTRICA da fonte (3 linhas
            // tipográficas completas): a constante 54 antiga deixava ~meia 4ª linha
            // visível, cortada ao meio e sem reticências.
            if (!string.IsNullOrWhiteSpace(_cab.Observacao))
            {
                string obs = $"OBS: {_cab.Observacao}";
                using var sfObs = new StringFormat { Trimming = StringTrimming.EllipsisWord };
                float alturaObs = Math.Min(FontCabecalho.GetHeight(g) * 3f + 2f,
                    g.MeasureString(obs, FontCabecalho, (int)larguraTotal).Height + 2f);
                g.DrawString(obs, FontCabecalho, Brushes.Black,
                    new RectangleF(x, y, larguraTotal, alturaObs), sfObs);
                y += alturaObs;
            }

            y += 4;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            return y + 4;
        }

        private float DesenharCabecalhoResumido(Graphics g, Pen penBorda, float x, float y, float larguraTotal)
        {
            using var sf = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.DrawString($"PEDIDO {_cab.Orcamento} — {_cab.ClienteNome} — pág. {_numeroPagina}",
                FontHeader, Brushes.Black, new RectangleF(x, y, larguraTotal, 18), sf);
            y += 20;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            return y + 4;
        }

        /// <summary>Bloco de assinaturas entre o cabeçalho e a tabela de produtos (página 1).</summary>
        private static float DesenharAssinaturas(Graphics g, Pen penBorda, float x, float y, float larguraTotal)
        {
            // BALCÃO menor: geralmente assinado com números curtos (001, 002...)
            float[] larguras = [260f, 260f, 120f];
            string[] rotulos = ["SEPARADOR", "CONFERENTE", "BALCÃO"];

            float somaLarguras = larguras.Sum();
            float gap = Math.Max(20f, (larguraTotal - somaLarguras) / (larguras.Length - 1));
            float escala = somaLarguras + gap * (larguras.Length - 1) > larguraTotal
                ? larguraTotal / (somaLarguras + gap * (larguras.Length - 1))
                : 1f;

            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            float cx = x;
            float linhaY = y + 22;
            for (int i = 0; i < larguras.Length; i++)
            {
                float w = larguras[i] * escala;
                g.DrawLine(penBorda, cx, linhaY, cx + w, linhaY);
                g.DrawString(rotulos[i], FontAssinatura, Brushes.Black,
                    new RectangleF(cx, linhaY + 3, w, 15), sfCenter);
                cx += w + gap * escala;
            }

            return linhaY + 26;
        }

        // MedirAlturaLinha e DrawRow foram extraídos para PrintingHelper.RelatorioTabela,
        // compartilhados com a Cópia de Pedido.
    }
}
