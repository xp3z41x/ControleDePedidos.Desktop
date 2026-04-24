namespace CONTROLE_DE_PEDIDOS
{
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
        public static readonly Color StatusNaoBg = Color.FromArgb(60, 22, 22);
        public static readonly Color StatusNaoFg = Color.FromArgb(248, 81, 73);
        public static readonly Color StatusTravadoBg = Color.FromArgb(60, 50, 10);
        public static readonly Color StatusTravadoFg = Color.FromArgb(210, 153, 34);
        public static readonly Color StatusEmFaturamentoBg = Color.FromArgb(20, 50, 60);
        public static readonly Color StatusEmFaturamentoFg = Color.FromArgb(100, 200, 220);
        public static readonly Font FontStatus = new("Segoe UI Semibold", 9f);

        // Fonts
        public static readonly Font FontRegular = new("Segoe UI", 9.5f);
        public static readonly Font FontRegularBold = new("Segoe UI Semibold", 9.5f);
        public static readonly Font FontSmall = new("Segoe UI", 8.5f);
        public static readonly Font FontTitle = new("Segoe UI Semibold", 12f);
        public static readonly Font FontHeader = new("Segoe UI Semibold", 9f);
        public static readonly Font FontIcon = new("Segoe UI", 13f);
        public static readonly Font FontDetalheLabel = new("Segoe UI", 7.5f);
        public static readonly Font FontDetalheValor = new("Segoe UI", 10f);
        public static readonly Font FontDetalheDestaque = new("Segoe UI Semibold", 13f);
        public static readonly Font FontSecaoTitulo = new("Segoe UI Semibold", 8f);

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

        public static void EstilizarGrid(DataGridView dgv, string gridKey)
        {
            EstilizarGrid(dgv);
            dgv.Tag = gridKey;

            dgv.ColumnWidthChanged += (_, e) =>
            {
                var col = dgv.Columns[e.Column.Index];
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) return;
                ColumnWidthStore.SetWidth(gridKey, col.Name, col.Width);
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

                dgv.BeginInvoke(() => AutoAjustarColunas(dgv));
            };
            dgv.ColumnHeadersHeight = 36;
            dgv.RowTemplate.Height = 32;

            dgv.DefaultCellStyle.BackColor = BgPanel;
            dgv.DefaultCellStyle.ForeColor = TextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = GridSelection;
            dgv.DefaultCellStyle.SelectionForeColor = TextPrimary;
            dgv.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);

            dgv.AlternatingRowsDefaultCellStyle.BackColor = GridRowAlt;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = GridSelection;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = BgCard;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            dgv.ColumnHeadersDefaultCellStyle.Font = FontHeader;
            dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 6, 8, 6);
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }

        public static void AutoAjustarColunas(DataGridView dgv)
        {
            if (dgv.Columns.Count == 0 || dgv.RowCount == 0) return;

            // Se existem larguras salvas pelo usuário, aplicá-las e pular auto-sizing
            if (dgv.Tag is string gridKey && ColumnWidthStore.HasWidths(gridKey))
            {
                foreach (DataGridViewColumn col in dgv.Columns)
                {
                    if (!col.Visible) continue;
                    if (ColumnWidthStore.TryGetWidth(gridKey, col.Name, out int savedWidth))
                        col.Width = savedWidth;
                }
                return;
            }

            using var g = dgv.CreateGraphics();

            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!col.Visible) continue;

                // Medir header
                var headerSize = g.MeasureString(col.HeaderText ?? "", dgv.ColumnHeadersDefaultCellStyle.Font ?? dgv.Font);
                int maxWidth = (int)headerSize.Width + 24; // padding

                // Medir conteudo das celulas (amostra das primeiras 50 linhas)
                int rowsToCheck = Math.Min(dgv.RowCount, 50);
                for (int i = 0; i < rowsToCheck; i++)
                {
                    var val = dgv.Rows[i].Cells[col.Index].FormattedValue?.ToString();
                    if (string.IsNullOrEmpty(val)) continue;
                    var cellFont = dgv.Rows[i].Cells[col.Index].InheritedStyle.Font ?? dgv.Font;
                    var cellSize = g.MeasureString(val, cellFont);
                    int cellWidth = (int)cellSize.Width + 24;
                    if (cellWidth > maxWidth) maxWidth = cellWidth;
                }

                int minWidth = col.MinimumWidth > 0 ? col.MinimumWidth : 50;
                col.Width = Math.Max(maxWidth, minWidth);
            }
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
            using var pen = new Pen(Theme.Accent, 2f);
            // Desenhar um check-mark simples
            var pts = new[]
            {
                new Point(rc.Left + 3, rc.Top + rc.Height / 2),
                new Point(rc.Left + rc.Width / 2 - 1, rc.Bottom - 4),
                new Point(rc.Right - 3, rc.Top + 3)
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
            if (e.Vertical)
            {
                int x = rc.Width / 2;
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, x, 6, x, rc.Height - 6);
            }
            else
            {
                int y = rc.Height / 2;
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 6, y, rc.Width - 6, y);
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
