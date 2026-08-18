using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    public class RelatorioCargaPrintDoc : PrintDocument
    {
        private readonly List<PedidoCarga> _pedidos;
        private int _indicePagina;
        private int _numSequencial;
        private bool _preview;
        private PrintingHelper.GeometriaPagina? _geo;

        private static readonly Font FontTitulo = new("Segoe UI Semibold", 14f);
        private static readonly Font FontData = new("Segoe UI", 9f);
        private static readonly Font FontHeader = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontDados = new("Segoe UI", 8.5f);
        private static readonly Font FontRodape = new("Segoe UI Semibold", 9f);

        // Proporções das colunas (soma = 100%)
        private static readonly float[] ColPercent = [4f, 22f, 17f, 13f, 9f, 9f, 7f, 19f];
        private static readonly string[] ColHeaders = ["#", "NOME", "NOME FANTASIA", "CIDADE", "ORÇAMENTO", "Nº PEDIDO", "VOLUMES", "ASSINATURA"];
        private static readonly StringAlignment[] ColAlign =
        [
            StringAlignment.Center, StringAlignment.Near, StringAlignment.Near, StringAlignment.Near,
            StringAlignment.Center, StringAlignment.Center, StringAlignment.Center, StringAlignment.Center
        ];

        public RelatorioCargaPrintDoc(List<PedidoCarga> pedidos)
        {
            _pedidos = pedidos;
            // NÃO trocar para true: o alinhamento com o papel é feito manualmente em
            // GeometriaPagina.PrepararPagina (cujo ResetTransform anularia a compensação
            // do controller, além de deslocar o preview).
            OriginAtMargins = false;
            // Independência da impressora padrão do Windows: pré-seleciona uma
            // impressora A4-capable e força A4/Landscape/Margins. Ver PrintingHelper.
            PrintingHelper.AplicarConfigA4(this, landscape: true);
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            base.OnBeginPrint(e);
            _indicePagina = 0;
            _numSequencial = 0;
            _preview = e.PrintAction == PrintAction.PrintToPreview;
            // Uma medição por job — ver PrintingHelper.GeometriaPagina.
            _geo = PrintingHelper.GeometriaPagina.Medir(DefaultPageSettings);
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            base.OnPrintPage(e);
            var g = e.Graphics!;
            _geo ??= PrintingHelper.GeometriaPagina.Medir(e.PageSettings);
            // Alinha a origem ao canto do PAPEL na impressão real (o preview já está
            // nesse referencial) e recorta a área ao que a impressora consegue marcar.
            var area = _geo.PrepararPagina(e, _preview);
            if (_indicePagina == 0)
                PrintingHelper.LogarPagina(e, "Carga", area, _geo, _preview);

            float x = area.Left;
            float y = area.Top;
            float larguraTotal = area.Width;

            // Calcular larguras das colunas
            float[] colW = new float[ColPercent.Length];
            for (int i = 0; i < ColPercent.Length; i++)
                colW[i] = larguraTotal * ColPercent[i] / 100f;

            const float alturaLinha = 22f;
            const float alturaHeaderTabela = 26f;
            const float paddingCelula = 4f;

            // === TÍTULO ===
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("RELATÓRIO DE CARGA", FontTitulo, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 28), sfCenter);
            y += 28;

            // === DATA ===
            g.DrawString($"Data: {DateTime.Now:dd/MM/yyyy}", FontData, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 18), sfCenter);
            y += 24;

            // Linha separadora abaixo do título
            using var penBorda = new Pen(Color.Black, 1.2f);
            using var penLinha = new Pen(Color.FromArgb(180, 180, 180), 0.6f);
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 2;

            // === HEADER DA TABELA ===
            float headerY = y;
            DrawRow(g, colW, ColHeaders, FontHeader, x, y, alturaHeaderTabela, paddingCelula, ColAlign, true);
            y += alturaHeaderTabela;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 1;

            // Calcular espaço para rodapé na última página
            float rodapeAltura = 30f;
            float limiteY = area.Bottom - rodapeAltura;

            // === LINHAS DE DADOS ===
            bool ultimaPagina = false;
            int itensNestaPagina = 0;
            while (_indicePagina < _pedidos.Count)
            {
                // Verificar se cabe mais uma linha. Garantia de progresso: ao menos uma
                // linha por página, senão HasMorePages ficaria true para sempre (job
                // infinito de folhas em branco) se a área útil ficar mínima.
                if (y + alturaLinha > limiteY && itensNestaPagina > 0)
                    break;

                itensNestaPagina++;
                var p = _pedidos[_indicePagina];
                _numSequencial++;
                _indicePagina++;

                string[] valores =
                [
                    _numSequencial.ToString(),
                    p.Cliente,
                    p.NomeFantasia,
                    p.Cidade,
                    p.Orcamento.ToString(),
                    p.NumeroPedido,
                    string.IsNullOrWhiteSpace(p.Volumes) ? "" : p.Volumes,
                    "" // Assinatura: espaço em branco
                ];

                // Fundo alternado
                if (_numSequencial % 2 == 0)
                {
                    using var bgBrush = new SolidBrush(Color.FromArgb(245, 245, 245));
                    g.FillRectangle(bgBrush, x, y, larguraTotal, alturaLinha);
                }

                DrawRow(g, colW, valores, FontDados, x, y, alturaLinha, paddingCelula, ColAlign, false);

                y += alturaLinha;

                // Linha separadora sutil entre linhas
                g.DrawLine(penLinha, x, y, x + larguraTotal, y);
            }

            ultimaPagina = _indicePagina >= _pedidos.Count;

            // Linha inferior da tabela
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);

            // Bordas laterais da tabela
            float tabelaTop = headerY;
            g.DrawLine(penBorda, x, tabelaTop, x, y);
            g.DrawLine(penBorda, x + larguraTotal, tabelaTop, x + larguraTotal, y);

            // Linhas verticais entre colunas
            float vx = x;
            for (int i = 0; i < colW.Length - 1; i++)
            {
                vx += colW[i];
                g.DrawLine(penLinha, vx, tabelaTop, vx, y);
            }

            // === RODAPÉ (apenas última página) ===
            if (ultimaPagina)
            {
                y += 6;
                int totalVolumes = 0;
                foreach (var p in _pedidos)
                {
                    if (int.TryParse(p.Volumes?.Trim(), out int v))
                        totalVolumes += v;
                }

                g.DrawString($"TOTAL: {_pedidos.Count} pedido{(_pedidos.Count != 1 ? "s" : "")}",
                    FontRodape, Brushes.Black, x + 4, y);

                using var sfRight = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString($"TOTAL VOLUMES: {totalVolumes}",
                    FontRodape, Brushes.Black,
                    new RectangleF(x, y, larguraTotal - 4, 20), sfRight);
            }

            e.HasMorePages = !ultimaPagina;
        }

        private static void DrawRow(Graphics g, float[] colW, string[] valores, Font font,
            float x, float y, float altura, float padding, StringAlignment[] aligns, bool bold)
        {
            float cx = x;
            using var brush = new SolidBrush(Color.Black);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            for (int i = 0; i < valores.Length && i < colW.Length; i++)
            {
                sf.Alignment = aligns[i];
                var rect = new RectangleF(cx + padding, y, colW[i] - padding * 2, altura);
                g.DrawString(valores[i], font, brush, rect, sf);
                cx += colW[i];
            }
        }
    }
}
