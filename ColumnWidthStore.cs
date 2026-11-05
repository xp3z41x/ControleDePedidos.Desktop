using System.Text.Json;

namespace CONTROLE_DE_PEDIDOS
{
    public static class ColumnWidthStore
    {
        private static readonly string DirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CONTROLE DE PEDIDOS");

        private static readonly string FilePath = Path.Combine(DirPath, "column-widths.json");

        private static Dictionary<string, Dictionary<string, int>> _data = [];
        // System.Timers.Timer dispara no ThreadPool: o Elapsed serializava _data enquanto
        // a UI chamava SetWidth (arrastando a coluna) → "Collection was modified",
        // engolida no catch, e a largura não era salva. Tudo que toca _data/_dirty
        // passa por este lock (incluindo o Flush do encerramento x Elapsed pendente).
        private static readonly object _sync = new();
        private static readonly System.Timers.Timer _debounce = new(500) { AutoReset = false };
        private static bool _dirty;

        static ColumnWidthStore()
        {
            _debounce.Elapsed += (_, _) => SalvarInterno();
        }

        public static void Carregar()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                var json = ArquivoAtomico.LerTexto(FilePath);
                var lido = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json) ?? [];
                lock (_sync) _data = lido;
            }
            catch
            {
                lock (_sync) _data = [];
            }
        }

        public static bool HasWidths(string gridKey)
        {
            lock (_sync)
                return _data.TryGetValue(gridKey, out var cols) && cols.Count > 0;
        }

        public static bool TryGetWidth(string gridKey, string columnName, out int width)
        {
            width = 0;
            lock (_sync)
                return _data.TryGetValue(gridKey, out var cols) && cols.TryGetValue(columnName, out width);
        }

        public static void SetWidth(string gridKey, string columnName, int width)
        {
            lock (_sync)
            {
                if (!_data.TryGetValue(gridKey, out var cols))
                {
                    cols = [];
                    _data[gridKey] = cols;
                }

                cols[columnName] = width;
                _dirty = true;
            }
            _debounce.Stop();
            _debounce.Start();
        }

        public static void Flush()
        {
            _debounce.Stop();
            SalvarInterno();
        }

        private static void SalvarInterno()
        {
            try
            {
                lock (_sync)
                {
                    if (!_dirty) return;
                    Directory.CreateDirectory(DirPath);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_data, options);
                    // Atômico: queda no meio da gravação deixava JSON truncado e o
                    // Carregar descartava TODAS as larguras salvas.
                    ArquivoAtomico.GravarTexto(FilePath, json);
                    _dirty = false;
                }
            }
            catch
            {
                // Larguras de colunas não são críticas — falha silenciosa
            }
        }
    }
}
