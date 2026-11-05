using System.Text.Json;

namespace CONTROLE_DE_PEDIDOS
{
    public static class FilterStore
    {
        private static readonly string DirPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CONTROLE DE PEDIDOS");

        private static readonly string FilePath = Path.Combine(DirPath, "filters.json");

        private static FiltrosPersistidos _data = new();
        // System.Windows.Forms.Timer garante que Tick roda no UI thread (message pump).
        // Isso evita race com SalvarDebounced(), que e sempre chamado de handlers UI.
        private static readonly System.Windows.Forms.Timer _debounce = new() { Interval = 500 };
        private static bool _dirty;

        static FilterStore()
        {
            _debounce.Tick += (_, _) =>
            {
                _debounce.Stop();
                SalvarInterno();
            };
        }

        public static void Carregar()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                var json = ArquivoAtomico.LerTexto(FilePath);
                _data = JsonSerializer.Deserialize<FiltrosPersistidos>(json) ?? new FiltrosPersistidos();
            }
            catch
            {
                _data = new FiltrosPersistidos();
            }
        }

        public static IReadOnlyCollection<string> Vendedores => _data.Vendedores;
        public static IReadOnlyCollection<string> Rotas => _data.Rotas;
        public static IReadOnlyCollection<string> Etapas => _data.Etapas;

        public static void SalvarDebounced(
            IEnumerable<string> vendedores,
            IEnumerable<string> rotas,
            IEnumerable<string> etapas)
        {
            _data = new FiltrosPersistidos
            {
                Vendedores = vendedores.ToList(),
                Rotas = rotas.ToList(),
                Etapas = etapas.ToList()
            };
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
                // Atômico: queda no meio da gravação deixava JSON truncado e o
                // Carregar descartava todos os filtros salvos.
                ArquivoAtomico.GravarTexto(FilePath, json);
                _dirty = false;
            }
            catch
            {
                // Filtros não são críticos — falha silenciosa
            }
        }

        public class FiltrosPersistidos
        {
            public List<string> Vendedores { get; set; } = [];
            public List<string> Rotas { get; set; } = [];
            public List<string> Etapas { get; set; } = [];
        }
    }
}
