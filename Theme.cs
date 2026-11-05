namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Escala de layout por DPI (PerMonitorV2). Toda medida fixa de UI (Size, Location,
    /// Padding, alturas de faixas/linhas de grid) é escrita em pixels LÓGICOS a 96 DPI e
    /// convertida aqui para o DPI corrente do controle — fontes em pontos já crescem
    /// sozinhas, o resto não. A 100% é a identidade (visual idêntico ao de antes).
    /// Linhas de 1 px (bordas/separadores) NÃO passam por aqui: ficam com 1 px em qualquer DPI.
    /// </summary>
    public static class Escala
    {
#if SIMULAR_DPI
        // Só no harness de verificação (nunca definido no build do app): simula a escala
        // multiplicando fontes e medidas, já que o display RDP não permite trocar o DPI.
        public static readonly float FatorSimulado =
            float.TryParse(Environment.GetEnvironmentVariable("CP_DPI_SIMULADO"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) && f > 0 ? f : 1f;
#endif

        /// <summary>Pixels lógicos (96 DPI) → pixels do DPI corrente do controle.</summary>
        public static int Px(this Control c, int logico)
        {
#if SIMULAR_DPI
            return (int)Math.Round(logico * (double)c.DeviceDpi * FatorSimulado / 96.0);
#else
            return c.LogicalToDeviceUnits(logico);
#endif
        }

        public static Size Px(this Control c, Size s) => new(c.Px(s.Width), c.Px(s.Height));
        public static Point Px(this Control c, Point p) => new(c.Px(p.X), c.Px(p.Y));
        public static Padding Px(this Control c, Padding p) =>
            new(c.Px(p.Left), c.Px(p.Top), c.Px(p.Right), c.Px(p.Bottom));

        /// <summary>Pixels do DPI corrente → lógicos (persistência independente de DPI).</summary>
        public static int ParaLogico(this Control c, int px) =>
            Math.Max(1, (int)Math.Round(px * 96.0 / Math.Max(1, c.Px(96))));

        /// <summary>Fonte em pontos (o Windows já a escala pelo DPI).</summary>
        public static Font Fonte(string familia, float pontos)
        {
#if SIMULAR_DPI
            pontos *= FatorSimulado;
#endif
            return new Font(familia, pontos);
        }
    }

    /// <summary>
    /// Base dos diálogos/forms montados em código: <c>Dpi(...)</c> converte medidas lógicas
    /// (96 DPI) para o DPI corrente — mesma convenção do Form1.
    /// </summary>
    public class FormEscalavel : Form
    {
        protected int Dpi(int v) => this.Px(v);
        protected Size Dpi(int largura, int altura) => this.Px(new Size(largura, altura));
        protected Point DpiPt(int x, int y) => this.Px(new Point(x, y));
        protected Padding Dpi(int esquerda, int topo, int direita, int baixo) =>
            this.Px(new Padding(esquerda, topo, direita, baixo));
    }

    public static class Theme
    {
        // Backgrounds
        public static readonly Color BgDark = Color.FromArgb(14, 17, 23);
        public static readonly Color BgPanel = Color.FromArgb(22, 27, 34);
        public static readonly Color BgCard = Color.FromArgb(30, 37, 46);
        public static readonly Color BgInput = Color.FromArgb(38, 46, 58);
        public static readonly Color BgStatusBar = Color.FromArgb(10, 13, 18);

        // Accent
        public static readonly Color Accent = Color.FromArgb(56, 132, 244);
        public static readonly Color AccentHover = Color.FromArgb(70, 145, 255);
        public static readonly Color AccentDark = Color.FromArgb(30, 70, 140);

        // Text
        public static readonly Color TextPrimary = Color.FromArgb(230, 237, 243);
        public static readonly Color TextSecondary = Color.FromArgb(139, 148, 158);
        public static readonly Color TextMuted = Color.FromArgb(89, 98, 109);

        // Status
        public static readonly Color Success = Color.FromArgb(63, 185, 80);
        public static readonly Color Warning = Color.FromArgb(210, 153, 34);
        public static readonly Color Error = Color.FromArgb(248, 81, 73);

        // Borders
        public static readonly Color Border = Color.FromArgb(48, 54, 61);
        public static readonly Color BorderLight = Color.FromArgb(58, 65, 74);

        // Grid
        public static readonly Color GridRowAlt = Color.FromArgb(26, 32, 40);
        public static readonly Color GridSelection = Color.FromArgb(36, 68, 120);

        // Status cells
        public static readonly Color StatusSimBg = Color.FromArgb(22, 60, 32);
        public static readonly Color StatusSimFg = Color.FromArgb(63, 185, 80);
        public static readonly Color StatusManualBg = Color.FromArgb(31, 75, 45);
        public static readonly Color StatusManualFg = Color.FromArgb(126, 231, 135);
        public static readonly Color StatusNaoBg = Color.FromArgb(60, 22, 22);
        public static readonly Color StatusNaoFg = Color.FromArgb(248, 81, 73);
        public static readonly Color StatusTravadoBg = Color.FromArgb(60, 50, 10);
        public static readonly Color StatusTravadoFg = Color.FromArgb(210, 153, 34);
        public static readonly Color StatusEmFaturamentoBg = Color.FromArgb(20, 50, 60);
        public static readonly Color StatusEmFaturamentoFg = Color.FromArgb(100, 200, 220);
        public static readonly Font FontStatus = Escala.Fonte("Segoe UI Semibold", 9f);

        // Fonts
        public static readonly Font FontRegular = Escala.Fonte("Segoe UI", 9.5f);
        public static readonly Font FontRegularBold = Escala.Fonte("Segoe UI Semibold", 9.5f);
        public static readonly Font FontSmall = Escala.Fonte("Segoe UI", 8.5f);
        public static readonly Font FontTitle = Escala.Fonte("Segoe UI Semibold", 12f);
        public static readonly Font FontHeader = Escala.Fonte("Segoe UI Semibold", 9f);
        public static readonly Font FontIcon = Escala.Fonte("Segoe UI", 13f);
        public static readonly Font FontDetalheLabel = Escala.Fonte("Segoe UI", 7.5f);
        public static readonly Font FontDetalheValor = Escala.Fonte("Segoe UI", 10f);
        public static readonly Font FontDetalheDestaque = Escala.Fonte("Segoe UI Semibold", 13f);
        public static readonly Font FontSecaoTitulo = Escala.Fonte("Segoe UI Semibold", 8f);

        public static void EstilizarBotao(Button btn, bool primario = false)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = primario ? 0 : 1;
            btn.FlatAppearance.BorderColor = Border;
            btn.BackColor = primario ? Accent : BgCard;
            btn.ForeColor = TextPrimary;
            btn.Font = FontRegular;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = primario ? AccentHover : BgInput;
            btn.FlatAppearance.MouseDownBackColor = primario ? AccentDark : BgPanel;
        }

        public static void EstilizarTextBox(TextBox txt)
        {
            txt.BackColor = BgInput;
            txt.ForeColor = TextPrimary;
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.Font = FontRegular;
        }

        /// <summary>
        /// Garante que o form caiba na área útil do monitor do Owner (ou o próprio),
        /// reduzindo o ClientSize se necessário e mantendo-o centrado e contido na tela.
        /// Chamar no Load (o Owner já está definido pelo ShowDialog).
        /// </summary>
        public static void LimitarAoWorkingArea(Form f, int margem = 48)
        {
            var tela = Screen.FromControl(f.Owner ?? f).WorkingArea;
            margem = f.Px(margem);
            var moldura = new Size(f.Width - f.ClientSize.Width, f.Height - f.ClientSize.Height);
            int maxW = tela.Width - margem - moldura.Width;
            int maxH = tela.Height - margem - moldura.Height;

            // MinimumSize não pode impedir o clamp
            if (f.MinimumSize.Width > 0 || f.MinimumSize.Height > 0)
                f.MinimumSize = new Size(
                    Math.Min(f.MinimumSize.Width, maxW + moldura.Width),
                    Math.Min(f.MinimumSize.Height, maxH + moldura.Height));

            var novo = new Size(Math.Min(f.ClientSize.Width, maxW), Math.Min(f.ClientSize.Height, maxH));
            if (novo != f.ClientSize) f.ClientSize = novo;

            // Recentrar no Owner (o CenterParent foi computado com o tamanho antigo) e conter na tela
            if (f.Owner is { } o)
                f.Location = new Point(o.Left + (o.Width - f.Width) / 2, o.Top + (o.Height - f.Height) / 2);
            f.Location = new Point(
                Math.Max(tela.Left, Math.Min(f.Left, tela.Right - f.Width)),
                Math.Max(tela.Top, Math.Min(f.Top, tela.Bottom - f.Height)));
        }

        public static void EstilizarGrid(DataGridView dgv, string gridKey)
        {
            EstilizarGrid(dgv);
            dgv.Tag = gridKey;
            GridLayout.Ativar(dgv, gridKey);

            dgv.ColumnWidthChanged += (_, e) =>
            {
                var col = dgv.Columns[e.Column.Index];
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) return;
                // Larguras aplicadas pelo encolhimento proporcional não são preferência
                // do usuário — persistir apenas o drag manual.
                if (GridLayout.EstaAjustando(dgv)) return;
                GridLayout.AtualizarPreferencia(dgv, col.Name, col.Width);
                // Persistida em pixels LÓGICOS: a mesma preferência vale em qualquer DPI
                ColumnWidthStore.SetWidth(gridKey, col.Name, dgv.ParaLogico(col.Width));
            };
        }

        public static void EstilizarGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = BgPanel;
            dgv.GridColor = Border;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.Font = FontRegular;
            dgv.RowHeadersVisible = false;
            dgv.EnableHeadersVisualStyles = false;
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.AllowUserToResizeColumns = true;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Ao trocar DataSource: habilitar sort, garantir None, auto-ajustar
            dgv.DataSourceChanged += (_, _) =>
            {
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (col.SortMode == DataGridViewColumnSortMode.NotSortable)
                        col.SortMode = DataGridViewColumnSortMode.Automatic;

                    // Garantir que todas as colunas estejam em modo manual (exceto Fill)
                    if (col.AutoSizeMode != DataGridViewAutoSizeColumnMode.None
                        && col.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                        col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }

                // Durante refresh controlado, pular auto-ajuste (estado será restaurado manualmente)
                if (dgv is OptimizedDataGridView odgv && odgv.SkipAutoAdjust)
                    return;

                // Sem handle na cadeia (ex.: grid num form host ainda não exibido),
                // BeginInvoke lança — o GridLayout reaplica no HandleCreated.
                if (dgv.IsHandleCreated)
                    dgv.BeginInvoke(() => AutoAjustarColunas(dgv));
            };
            AplicarMetricasGrid(dgv);
            // PerMonitorV2: DPI mudou com o app aberto (reconexão RDP de outro cliente,
            // troca de monitor) — reaplica as métricas no DPI novo depois do reescalonamento
            // do próprio WinForms (valores absolutos: idempotente).
            dgv.DpiChangedAfterParent += (_, _) =>
            {
                if (dgv.IsHandleCreated) dgv.BeginInvoke(() => AplicarMetricasGrid(dgv));
                else AplicarMetricasGrid(dgv);
            };

            dgv.DefaultCellStyle.BackColor = BgPanel;
            dgv.DefaultCellStyle.ForeColor = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = GridSelection;
            dgv.DefaultCellStyle.SelectionForeColor = TextPrimary;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = GridRowAlt;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = GridSelection;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = BgCard;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            dgv.ColumnHeadersDefaultCellStyle.Font = FontHeader;
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            // Título nunca quebra linha: sem espaço, o texto é abreviado com reticências
            dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // Pintura manual dos headers: o DataGridView reserva espaço para o glifo de
            // ordenação à direita, então o MiddleCenter nativo centraliza o texto só na
            // área restante (desvio à esquerda que varia com a largura da coluna).
            // Aqui o texto centraliza na célula INTEIRA e o glifo é desenhado por cima.
            dgv.CellPainting += (_, e) =>
            {
                if (e.RowIndex != -1 || e.ColumnIndex < 0) return;

                e.PaintBackground(e.ClipBounds, true);

                string texto = e.FormattedValue?.ToString() ?? "";
                var fonte = e.CellStyle?.Font ?? FontHeader;
                var corTexto = e.CellStyle?.ForeColor ?? TextPrimary;
                TextRenderer.DrawText(e.Graphics!, texto, fonte, e.CellBounds, corTexto,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix);

                var glifo = dgv.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection;
                if (glifo != SortOrder.None)
                {
                    int cx = e.CellBounds.Right - dgv.Px(14);
                    int cy = e.CellBounds.Top + e.CellBounds.Height / 2;
                    int dx = dgv.Px(4), d2 = dgv.Px(2), d3 = dgv.Px(3);
                    using var brush = new SolidBrush(TextSecondary);
                    Point[] seta = glifo == SortOrder.Ascending
                        ? [new Point(cx - dx, cy + d2), new Point(cx + dx, cy + d2), new Point(cx, cy - d3)]
                        : [new Point(cx - dx, cy - d2), new Point(cx + dx, cy - d2), new Point(cx, cy + d3)];
                    e.Graphics!.FillPolygon(brush, seta);
                }

                e.Handled = true;
            };
        }

        /// <summary>
        /// Alturas de cabeçalho/linha e paddings de célula no DPI corrente (lógicos: 36/32,
        /// 8-4-8-4 e 8-6-8-6). Chamado na estilização e a cada mudança de DPI.
        /// </summary>
        public static void AplicarMetricasGrid(DataGridView dgv)
        {
            int alturaLinha = dgv.Px(32);
            dgv.ColumnHeadersHeight = dgv.Px(36);
            dgv.RowTemplate.Height = alturaLinha;
            dgv.DefaultCellStyle.Padding = dgv.Px(new Padding(8, 4, 8, 4));
            dgv.ColumnHeadersDefaultCellStyle.Padding = dgv.Px(new Padding(8, 6, 8, 6));
            // Linhas já criadas não seguem o RowTemplate (só relevante na troca de DPI)
            foreach (DataGridViewRow r in dgv.Rows)
                if (r.Height != alturaLinha) r.Height = alturaLinha;
        }

        public static void AutoAjustarColunas(DataGridView dgv)
        {
            if (dgv.Columns.Count == 0 || dgv.RowCount == 0) return;

            // Se existem larguras salvas pelo usuário, elas viram as preferidas e o
            // encolhimento proporcional decide se cabem exatas ou escaladas
            if (dgv.Tag is string gridKey && ColumnWidthStore.HasWidths(gridKey))
            {
                GridLayout.ExecutarProgramatico(dgv, () =>
                {
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        if (!col.Visible) continue;
                        if (ColumnWidthStore.TryGetWidth(gridKey, col.Name, out int savedWidth))
                            col.Width = dgv.Px(savedWidth);
                    }
                });
                GridLayout.DefinirPreferencias(dgv);
                GridLayout.Aplicar(dgv);
                return;
            }

            // Teto do auto-fit por conteúdo (escalado por DPI): sem ele, um texto muito
            // longo cria preferências absurdas que estouram qualquer tela
            int teto = dgv.LogicalToDeviceUnits(400);

            GridLayout.ExecutarProgramatico(dgv, () =>
            {
                using var g = dgv.CreateGraphics();

                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (!col.Visible) continue;

                    // Medir header
                    var headerSize = g.MeasureString(col.HeaderText ?? "", dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font);
                    int folga = dgv.Px(24); // padding
                    int maxWidth = (int)headerSize.Width + folga;

                    // Medir conteudo das celulas (amostra das primeiras 50 linhas)
                    int rowsToCheck = Math.Min(dgv.RowCount, 50);
                    for (int i = 0; i < rowsToCheck; i++)
                    {
                        var val = dgv.Rows[i].Cells[col.Index].FormattedValue?.ToString();
                        if (string.IsNullOrEmpty(val)) continue;
                        var cellFont = dgv.Rows[i].Cells[col.Index].InheritedStyle.Font ?? dgv.Font;
                        var cellSize = g.MeasureString(val, cellFont);
                        int cellWidth = (int)cellSize.Width + folga;
                        if (cellWidth > maxWidth) maxWidth = cellWidth;
                    }

                    int minWidth = col.MinimumWidth > 0 ? col.MinimumWidth : dgv.Px(50);
                    col.Width = Math.Clamp(maxWidth, minWidth, Math.Max(teto, minWidth));
                }
            });
            GridLayout.DefinirPreferencias(dgv);
            GridLayout.Aplicar(dgv);
        }
    }

    public class ThemeToolStripRenderer : ToolStripProfessionalRenderer
    {
        public ThemeToolStripRenderer() : base(new ThemeMenuColors()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Theme.BgCard);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // sem borda — o painel já tem uma linha inferior separadora
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            PintarItemDropDown(e);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            PintarItemDropDown(e);
        }

        private static void PintarItemDropDown(ToolStripItemRenderEventArgs e)
        {
            var rc = new Rectangle(Point.Empty, e.Item.Size);
            Color bg;
            if (e.Item is ToolStripDropDownItem dd && dd.Pressed)
                bg = Theme.BgPanel;
            else if (e.Item.Selected)
                bg = Theme.BgInput;
            else
                bg = Theme.BgCard;
            using var brush = new SolidBrush(bg);
            e.Graphics.FillRectangle(brush, rc);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rc = new Rectangle(Point.Empty, e.Item.Size);
            var color = e.Item.Selected ? Theme.BgInput : Theme.BgPanel;
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, rc);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Tag as string == "filtro-ativo")
                e.TextColor = Theme.Accent;
            else if (e.Item.Selected)
                e.TextColor = Theme.TextPrimary;
            else
                e.TextColor = Theme.TextSecondary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var rc = e.ImageRectangle;
            var ts = e.ToolStrip;
            int Px(int v) => ts?.Px(v) ?? v;
            using var pen = new Pen(Theme.Accent, Px(2));
            // Desenhar um check-mark simples
            var pts = new[]
            {
                new Point(rc.Left + Px(3), rc.Top + rc.Height / 2),
                new Point(rc.Left + rc.Width / 2 - Px(1), rc.Bottom - Px(4)),
                new Point(rc.Right - Px(3), rc.Top + Px(3))
            };
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(pen, pts);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextSecondary;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var rc = new Rectangle(Point.Empty, e.Item.Size);
            int m = e.ToolStrip?.Px(6) ?? 6;
            if (e.Vertical)
            {
                int x = rc.Width / 2;
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, x, m, x, rc.Height - m);
            }
            else
            {
                int y = rc.Height / 2;
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, m, y, rc.Width - m, y);
            }
        }
    }

    public class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemeMenuRenderer() : base(new ThemeMenuColors()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rc = new Rectangle(Point.Empty, e.Item.Size);
            var color = e.Item.Selected ? Theme.BgInput : Theme.BgPanel;
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, rc);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected ? Theme.TextPrimary : Theme.TextSecondary;
            base.OnRenderItemText(e);
        }
    }

    public class ThemeMenuColors : ProfessionalColorTable
    {
        public override Color MenuBorder => Theme.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Theme.BgInput;
        public override Color MenuStripGradientBegin => Theme.BgPanel;
        public override Color MenuStripGradientEnd => Theme.BgPanel;
        public override Color MenuItemSelectedGradientBegin => Theme.BgInput;
        public override Color MenuItemSelectedGradientEnd => Theme.BgInput;
        public override Color MenuItemPressedGradientBegin => Theme.BgCard;
        public override Color MenuItemPressedGradientEnd => Theme.BgCard;
        public override Color ImageMarginGradientBegin => Theme.BgPanel;
        public override Color ImageMarginGradientMiddle => Theme.BgPanel;
        public override Color ImageMarginGradientEnd => Theme.BgPanel;
        public override Color ToolStripDropDownBackground => Theme.BgPanel;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
    }
}
