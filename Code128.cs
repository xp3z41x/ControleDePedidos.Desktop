namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Gerador de código de barras Code 128 subtipo C (apenas dígitos), desenhado
    /// via GDI+ sem dependências externas. Usado no Relatório de Separação para
    /// codificar o número do orçamento.
    /// </summary>
    public static class Code128
    {
        // Padrões oficiais Code 128: cada string traz as larguras (em módulos) dos
        // elementos alternando barra/espaço, começando por barra. Símbolos de dados
        // têm 11 módulos; o stop (índice 106) tem 13.
        // Índices 0..102 = valores de dados; 103=Start A, 104=Start B, 105=Start C, 106=Stop.
        private static readonly string[] Padroes =
        [
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
            "114131", "311141", "411131", "211412", "211214", "211232",
            "2331112"
        ];

        private const int StartC = 105;
        private const int Stop = 106;

        /// <summary>
        /// Codifica uma string de dígitos em Code 128-C e retorna a sequência de
        /// larguras em módulos, alternando barra/espaço, começando por barra.
        /// Comprimento ímpar recebe '0' à esquerda (o leitor devolve o zero, mas o
        /// valor numérico permanece o mesmo).
        /// </summary>
        public static int[] CodificarC(string digitos)
        {
            if (string.IsNullOrEmpty(digitos))
                throw new ArgumentException("Código vazio.", nameof(digitos));
            foreach (var c in digitos)
                if (c is < '0' or > '9')
                    throw new ArgumentException($"Code 128-C aceita apenas dígitos: '{digitos}'.", nameof(digitos));

            if (digitos.Length % 2 != 0)
                digitos = "0" + digitos;

            var simbolos = new List<int> { StartC };
            for (int i = 0; i < digitos.Length; i += 2)
                simbolos.Add((digitos[i] - '0') * 10 + (digitos[i + 1] - '0'));

            // Checksum: start pesa 1; cada símbolo de dados pesa sua posição (1-based).
            int soma = StartC;
            for (int i = 1; i < simbolos.Count; i++)
                soma += simbolos[i] * i;
            simbolos.Add(soma % 103);
            simbolos.Add(Stop);

            var larguras = new List<int>();
            foreach (var s in simbolos)
                foreach (var ch in Padroes[s])
                    larguras.Add(ch - '0');
            return [.. larguras];
        }

        /// <summary>
        /// Desenha o código de barras em (x, y). Unidades seguem o PageUnit atual do
        /// Graphics (no relatório: Display, 1/100"). Retorna a largura total desenhada
        /// (sem quiet zones — reserve ≥ 10 módulos livres de cada lado).
        /// </summary>
        public static float Desenhar(Graphics g, string digitos, float x, float y, float larguraModulo, float altura)
        {
            var larguras = CodificarC(digitos);
            float cx = x;
            for (int i = 0; i < larguras.Length; i++)
            {
                float w = larguras[i] * larguraModulo;
                if (i % 2 == 0)
                    g.FillRectangle(Brushes.Black, cx, y, w, altura);
                cx += w;
            }
            return cx - x;
        }

        /// <summary>Largura total (na unidade do módulo) que <see cref="Desenhar"/> ocupará.</summary>
        public static float MedirLargura(string digitos, float larguraModulo)
        {
            int totalModulos = 0;
            foreach (var m in CodificarC(digitos))
                totalModulos += m;
            return totalModulos * larguraModulo;
        }
    }
}
