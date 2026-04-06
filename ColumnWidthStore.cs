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

                var json = File.ReadAllText(FilePath);
                _data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(json) ?? [];
            }
            catch
            {
                _data = [];
            }
        }

        public static bool HasWidths(string gridKey)
        {
            return _data.ContainsKey(gridKey) && _data[gridKey].Count > 0;
        }

        public static bool TryGetWidth(string gridKey, string columnName, out int width)
        {
            width = 0;
            return _data.TryGetValue(gridKey, out var cols) && cols.TryGetValue(columnName, out width);
        }

        public static void SetWidth(string gridKey, string columnName, int width)
        {
            if (!_data.TryGetValue(gridKey, out var cols))
            {
                cols = [];
                _data[gridKey] = cols;
            }

            cols[columnName] = width;
            _dirty = true;
            _debounce.Stop();
            _debounce.Start();
        }

        public static void Flush()
        {
            _debounce.Stop();
            if (_dirty) SalvarInterno();
        }

        private static void SalvarInterno()
        {
            try
            {
                Directory.CreateDirectory(DirPath);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_data, options);
                File.WriteAllText(FilePath, json);
                _dirty = false;
            }
            catch
            {
                // Larguras de colunas não são críticas — falha silenciosa
            }
        }
    }
}
