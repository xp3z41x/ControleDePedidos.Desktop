using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    public class RelatorioNfePrintDoc : PrintDocument
    {
        private readonly List<PedidoNfe> _pedidos;
        private int _indicePagina;
        private int _numSequencial;

        private static readonly Font FontTitulo = new("Segoe UI Semibold", 14f);
        private static readonly Font FontData = new("Segoe UI", 9f);
        private static readonly Font FontHeader = new("Segoe UI Semibold", 8.5f);
        private static readonly Font FontDados = new("Segoe UI", 8.5f);
        private static readonly Font FontRodape = new("Segoe UI Semibold", 9f);

        private static readonly float[] ColPercent = [5f, 30f, 25f, 12f, 12f, 16f];
        private static readonly string[] ColHeaders = ["#", "NOME", "NOME FANTASIA", "ORÇAMENTO", "Nº NF-e", "CONFERIDO"];
        private static readonly StringAlignment[] ColAlign =
        [
            StringAlignment.Center, StringAlignment.Near, StringAlignment.Near,
            StringAlignment.Center, StringAlignment.Center, StringAlignment.Center
        ];

        public RelatorioNfePrintDoc(List<PedidoNfe> pedidos)
        {
            _pedidos = pedidos;
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
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            base.OnPrintPage(e);
            var g = e.Graphics!;
            g.ResetTransform();
            g.PageUnit = GraphicsUnit.Display;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            var area = e.MarginBounds;
            float x = area.Left;
            float y = area.Top;
            float larguraTotal = area.Width;

            float[] colW = new float[ColPercent.Length];
            for (int i = 0; i < ColPercent.Length; i++)
                colW[i] = larguraTotal * ColPercent[i] / 100f;

            const float alturaLinha = 22f;
            const float alturaHeaderTabela = 26f;
            const float paddingCelula = 4f;

            // === TÍTULO ===
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("RELATÓRIO DE NF-e", FontTitulo, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 28), sfCenter);
            y += 28;

            // === DATA ===
            g.DrawString($"Data: {DateTime.Now:dd/MM/yyyy}", FontData, Brushes.Black,
                new RectangleF(x, y, larguraTotal, 18), sfCenter);
            y += 24;

            using var penBorda = new Pen(Color.Black, 1.2f);
            using var penLinha = new Pen(Color.FromArgb(180, 180, 180), 0.6f);
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 2;

            // === HEADER DA TABELA ===
            float headerY = y;
            DrawRow(g, colW, ColHeaders, FontHeader, x, y, alturaHeaderTabela, paddingCelula, ColAlign);
            y += alturaHeaderTabela;
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            y += 1;

            float rodapeAltura = 30f;
            float limiteY = area.Bottom - rodapeAltura;

            // === LINHAS DE DADOS ===
            while (_indicePagina < _pedidos.Count)
            {
                if (y + alturaLinha > limiteY)
                    break;

                var p = _pedidos[_indicePagina];
                _numSequencial++;
                _indicePagina++;

                string[] valores =
                [
                    _numSequencial.ToString(),
                    p.Cliente,
                    p.NomeFantasia,
                    p.Orcamento.ToString(),
                    p.NumeroNfe,
                    ""
                ];

                if (_numSequencial % 2 == 0)
                {
                    using var bgBrush = new SolidBrush(Color.FromArgb(245, 245, 245));
                    g.FillRectangle(bgBrush, x, y, larguraTotal, alturaLinha);
                }

                DrawRow(g, colW, valores, FontDados, x, y, alturaLinha, paddingCelula, ColAlign);

                y += alturaLinha;
                g.DrawLine(penLinha, x, y, x + larguraTotal, y);
            }

            bool ultimaPagina = _indicePagina >= _pedidos.Count;

            // Bordas da tabela
            g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            g.DrawLine(penBorda, x, headerY, x, y);
            g.DrawLine(penBorda, x + larguraTotal, headerY, x + larguraTotal, y);

            float vx = x;
            for (int i = 0; i < colW.Length - 1; i++)
            {
                vx += colW[i];
                g.DrawLine(penLinha, vx, headerY, vx, y);
            }

            // === RODAPÉ ===
            if (ultimaPagina)
            {
                y += 6;
                g.DrawString($"TOTAL: {_pedidos.Count} pedido{(_pedidos.Count != 1 ? "s" : "")}",
                    FontRodape, Brushes.Black, x + 4, y);
            }

            e.HasMorePages = !ultimaPagina;
        }

        private static void DrawRow(Graphics g, float[] colW, string[] valores, Font font,
            float x, float y, float altura, float padding, StringAlignment[] aligns)
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
