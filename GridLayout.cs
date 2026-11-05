using System.Runtime.CompilerServices;

namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Encolhimento proporcional de colunas: as larguras "preferidas" (persistidas no
    /// ColumnWidthStore ou definidas pelo auto-ajuste) são a meta; quando o espaço útil
    /// do grid é menor que a soma delas, todas as colunas encolhem proporcionalmente
    /// (respeitando MinimumWidth) para eliminar o scroll horizontal. Em janelas largas
    /// as preferidas são aplicadas exatas — o comportamento clássico é preservado.
    /// </summary>
    public static class GridLayout
    {
        private sealed class Estado
        {
            public readonly Dictionary<string, int> Preferidas = [];
            public string? GridKey;
            public bool Ajustando;      // larguras sendo aplicadas por código (não persistir)
            public bool AjustePendente; // coalescing de Resize/RowsAdded/RowsRemoved
            public int EscalaPreferidas; // Px(96) do DPI em que as Preferidas foram medidas
        }

        private static readonly ConditionalWeakTable<DataGridView, Estado> _estados = new();

        /// <summary>Registra os gatilhos de reajuste. Chamar uma única vez por grid.</summary>
        public static void Ativar(DataGridView dgv, string gridKey)
        {
            var st = _estados.GetOrCreateValue(dgv);
            st.GridKey = gridKey;

            void Agendar()
            {
                if (!dgv.IsHandleCreated || st.AjustePendente) return;
                st.AjustePendente = true;
                dgv.BeginInvoke(() => { st.AjustePendente = false; Aplicar(dgv); });
            }

            dgv.Resize += (_, _) => Agendar();
            // A scrollbar vertical aparece/some com a quantidade de linhas, sem Resize
            dgv.RowsAdded += (_, _) => Agendar();
            dgv.RowsRemoved += (_, _) => Agendar();
            // 1ª aplicação: restaura larguras salvas mesmo em grids que carregam com SkipAutoAdjust
            dgv.HandleCreated += (_, _) => Agendar();
        }

        /// <summary>True enquanto larguras estão sendo aplicadas por código (não persistir no store).</summary>
        public static bool EstaAjustando(DataGridView dgv) =>
            _estados.TryGetValue(dgv, out var st) && st.Ajustando;

        /// <summary>Registra a largura arrastada pelo usuário como nova preferência absoluta.</summary>
        public static void AtualizarPreferencia(DataGridView dgv, string coluna, int largura)
        {
            var st = _estados.GetOrCreateValue(dgv);
            SincronizarDpi(dgv, st);
            st.Preferidas[coluna] = largura;
        }

        /// <summary>
        /// As preferidas ficam em pixels do DPI em que foram medidas; se o DPI do grid mudou
        /// (PerMonitorV2: reconexão RDP/troca de monitor), reescalam na mesma proporção.
        /// </summary>
        private static void SincronizarDpi(DataGridView dgv, Estado st)
        {
            int escala = dgv.Px(96);
            if (st.EscalaPreferidas == escala) return;
            if (st.EscalaPreferidas > 0)
                foreach (var k in st.Preferidas.Keys.ToList())
                    st.Preferidas[k] = (int)Math.Round(st.Preferidas[k] * (double)escala / st.EscalaPreferidas);
            st.EscalaPreferidas = escala;
        }

        /// <summary>Snapshot: as larguras correntes das colunas viram as preferidas.</summary>
        public static void DefinirPreferencias(DataGridView dgv)
        {
            var st = _estados.GetOrCreateValue(dgv);
            SincronizarDpi(dgv, st);
            foreach (DataGridViewColumn col in dgv.Columns)
                if (col.Visible && col.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                    st.Preferidas[col.Name] = col.Width;
        }

        /// <summary>Executa uma ação que altera larguras sem que elas sejam persistidas.</summary>
        public static void ExecutarProgramatico(DataGridView dgv, Action acao)
        {
            var st = _estados.GetOrCreateValue(dgv);
            bool anterior = st.Ajustando;
            st.Ajustando = true;
            try { acao(); }
            finally { st.Ajustando = anterior; }
        }

        /// <summary>Aplica as larguras: preferidas exatas se couberem, senão encolhimento proporcional.</summary>
        public static void Aplicar(DataGridView dgv)
        {
            if (!dgv.IsHandleCreated || dgv.Columns.Count == 0) return;
            var st = _estados.GetOrCreateValue(dgv);
            if (st.Ajustando) return;
            SincronizarDpi(dgv, st);

            var cols = dgv.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible && c.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                .ToList();
            if (cols.Count == 0) return;

            // Preferências lazy: na 1ª aplicação usa a largura salva pelo usuário ou a corrente
            foreach (var c in cols)
            {
                if (st.Preferidas.ContainsKey(c.Name)) continue;
                // O store guarda pixels lógicos (96 DPI)
                st.Preferidas[c.Name] =
                    st.GridKey != null && ColumnWidthStore.TryGetWidth(st.GridKey, c.Name, out int salvo)
                        ? dgv.Px(salvo)
                        : c.Width;
            }

            // Largura útil: área cliente menos scrollbar vertical visível e reserva das colunas
            // Fill (mínimos em pixels lógicos, escalados). A folga de 2px evita que bordas internas do DGV disparem a scrollbar
            // horizontal quando a soma bate exatamente na largura do client.
            int util = dgv.ClientSize.Width - 2;
            if (dgv.Controls.OfType<VScrollBar>().Any(sb => sb.Visible))
                util -= SystemInformation.VerticalScrollBarWidth;
            foreach (DataGridViewColumn c in dgv.Columns)
                if (c.Visible && c.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
                    util -= Math.Max(c.MinimumWidth, dgv.Px(50));
            if (util <= 0) return;

            long somaPref = cols.Sum(c => (long)st.Preferidas[c.Name]);

            st.Ajustando = true;
            try
            {
                if (somaPref <= util)
                {
                    foreach (var c in cols)
                        if (c.Width != st.Preferidas[c.Name]) c.Width = st.Preferidas[c.Name];
                    return;
                }

                // Encolhimento proporcional com clamp iterativo nos mínimos: colunas que
                // bateriam abaixo do MinimumWidth ficam no mínimo e saem do rateio.
                var alvo = new Dictionary<DataGridViewColumn, int>();
                var livres = new List<DataGridViewColumn>(cols);
                int restante = util;
                bool clampou = true;
                while (clampou && livres.Count > 0)
                {
                    clampou = false;
                    long somaLivres = livres.Sum(c => (long)st.Preferidas[c.Name]);
                    if (somaLivres <= 0) break;
                    foreach (var c in livres.ToList())
                    {
                        int min = Math.Max(c.MinimumWidth, dgv.Px(30));
                        int prop = (int)(st.Preferidas[c.Name] * (long)restante / somaLivres);
                        if (prop < min)
                        {
                            alvo[c] = min;
                            restante -= min;
                            livres.Remove(c);
                            clampou = true;
                        }
                    }
                }

                if (livres.Count > 0)
                {
                    long somaLivres = livres.Sum(c => (long)st.Preferidas[c.Name]);
                    int usado = 0;
                    for (int i = 0; i < livres.Count; i++)
                    {
                        var c = livres[i];
                        int w = i == livres.Count - 1
                            ? restante - usado // última coluna absorve o arredondamento
                            : (int)(st.Preferidas[c.Name] * (long)restante / somaLivres);
                        alvo[c] = Math.Max(w, Math.Max(c.MinimumWidth, dgv.Px(30)));
                        usado += alvo[c];
                    }
                }

                foreach (var (c, w) in alvo)
                    if (c.Width != w) c.Width = w;
            }
            finally
            {
                st.Ajustando = false;
            }
        }
    }
}
