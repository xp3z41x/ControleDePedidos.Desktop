using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;

namespace CONTROLE_DE_PEDIDOS
{
    public class CopiaPedidoCabecalho
    {
        public int Orcamento { get; set; }
        public string VendedorCodigo { get; set; } = "";
        public string VendedorNome { get; set; } = "";
        public string Observacao { get; set; } = "";
        public string ClienteCodigo { get; set; } = "";
        public string ClienteNome { get; set; } = "";
        public string Fantasia { get; set; } = "";
        /// <summary>Endereço já composto: "rua, nº, bairro".</summary>
        public string Endereco { get; set; } = "";
        public string Cidade { get; set; } = "";
        public string Estado { get; set; } = "";
        public string Cep { get; set; } = "";
        /// <summary>'F' = pessoa física (CPF/RG), 'J' = jurídica (CNPJ/IE).</summary>
        public string Natureza { get; set; } = "";
        /// <summary>Dígitos crus de cadcli.cpf: CPF (11) ou CNPJ (14).</summary>
        public string CpfCnpj { get; set; } = "";
        /// <summary>cadcli.identidade: RG (natureza F) ou Inscrição Estadual (J).</summary>
        public string Identidade { get; set; } = "";
        public string Ddd { get; set; } = "";
        public string Fone { get; set; } = "";
        public string DddCelular { get; set; } = "";
        public string Celular { get; set; } = "";
        public string Email { get; set; } = "";
        public string Texto01 { get; set; } = "";   // forma de pagamento
        public string Texto02 { get; set; } = "";   // número do pedido
        public string Texto03 { get; set; } = "";   // volumes
        public string Texto04 { get; set; } = "";   // peso
        public decimal VrTotal { get; set; }
        /// <summary>estorc.vr_negociado — abatimento interno de créditos etc. Zero = sem
        /// negociação; diferente de zero imprime a linha VALOR NEGOCIADO sob o TOTAL.</summary>
        public decimal VrNegociado { get; set; }
        /// <summary>estorc.data_inc + hora_inc já formatados ("dd/MM/yyyy HH:MM"); "—" quando ausentes.</summary>
        public string DataPedido { get; set; } = "";
        /// <summary>Preenchido depois via BuscarNotasFiscaisAsync; vazio imprime "—".</summary>
        public string NotaFiscal { get; set; } = "";
    }

    public class CopiaPedidoItem
    {
        public int Item { get; set; }
        public string Matricula { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Marca { get; set; } = "";
        public string Referencia { get; set; } = "";
        public decimal Quantidade { get; set; }
        /// <summary>estior.vr_unitario — unitário JÁ COM desconto (o que o cliente paga).</summary>
        public decimal VrUnitario { get; set; }
        /// <summary>estior.vr_total — total do item JÁ COM desconto (= Quantidade × VrUnitario).</summary>
        public decimal VrTotal { get; set; }
        /// <summary>estior.vr_prazo — unitário ORIGINAL, antes do desconto (o "Valor a Prazo"
        /// do impresso do próprio Integra). Nunca menor que VrUnitario na filial 5 (nov/2026).
        /// Igual a VrUnitario = item sem desconto.</summary>
        public decimal VrOriginal { get; set; }
    }

    /// <summary>
    /// Snapshot dos campos [Empresa] do config — desacopla o PrintDoc do AppConfig.
    /// Campo vazio é simplesmente omitido do cabeçalho.
    /// </summary>
    public class EmpresaDados
    {
        public string RazaoSocial { get; set; } = "";
        public string Cnpj { get; set; } = "";
        public string InscricaoEstadual { get; set; } = "";
        public string Endereco { get; set; } = "";
        public string CidadeUfCep { get; set; } = "";
        public string Telefone { get; set; } = "";
        public string Email { get; set; } = "";
        public string WhatsApp { get; set; } = "";

        public static EmpresaDados De(AppConfig c) => new()
        {
            RazaoSocial = c.EmpresaRazaoSocial,
            Cnpj = c.EmpresaCnpj,
            InscricaoEstadual = c.EmpresaInscricaoEstadual,
            Endereco = c.EmpresaEndereco,
            CidadeUfCep = c.EmpresaCidadeUfCep,
            Telefone = c.EmpresaTelefone,
            Email = c.EmpresaEmail,
            WhatsApp = c.EmpresaWhatsApp
        };
    }

    /// <summary>
    /// Cópia de Pedido — documento entregue ao cliente. Sem valor fiscal (frase legal
    /// obrigatória no topo) e SEM efeitos colaterais: ao contrário do Relatório de
    /// Separação, imprimir uma cópia nunca altera status de pedido.
    /// </summary>
    public class RelatorioCopiaPedidoPrintDoc : PrintDocument
    {
        private readonly CopiaPedidoCabecalho _cab;
        private readonly List<CopiaPedidoItem> _itens;
        private readonly EmpresaDados _empresa;
        private readonly decimal _somaItens;
        private readonly bool _temNegociado;

        // Data/hora REGISTRADA NO INTEGRA (não o relógio da impressão) — ver _cab.DataPedido.
        private const string RotuloData = "Data:";

        // Estado por job (zerado em OnBeginPrint; preview e impressão real recalculam)
        private int _numeroPagina;
        private bool _preview;
        private PrintingHelper.GeometriaPagina? _geo;
        private int? _totalPaginas;
        private float[]? _alturasLinhas;
        private EstadoPaginacao _estado = new();

        /// <summary>Posição da renderização entre páginas. Uma instância para o job real
        /// e outra descartável para a simulação — o MESMO LayoutPagina avança ambas.</summary>
        private sealed class EstadoPaginacao
        {
            public int Item;         // próximo item a imprimir
            public bool TotalOk;     // linha TOTAL já emitida
            public bool RodapeOk;    // rodapé emitido = documento completo
        }

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private static readonly Font FontTitulo = new("Segoe UI Semibold", 14f);
        private static readonly Font FontSubtitulo = new("Segoe UI Semibold", 10f);
        private static readonly Font FontCabecalho = new("Segoe UI", 8.5f);
        private static readonly Font FontEmpresa = new("Segoe UI", 8f);
        private static readonly Font FontFaixaLegal = new("Segoe UI Semibold", 8f);
        private static readonly Font FontHeader = new("Segoe UI Semibold", 7.5f);
        private static readonly Font FontDados = new("Segoe UI", 7.5f);
        private static readonly Font FontTotal = new("Segoe UI Semibold", 8.5f);

        private const string FraseLegal =
            "NÃO É DOCUMENTO FISCAL - NÃO É VÁLIDO COMO RECIBO E COMO GARANTIA DE MERCADORIA - NÃO COMPROVA PAGAMENTO";

        // Barcode Code 128-C: mesmos parâmetros da separação
        private const float BarcodeModulo = 1.5f;
        private const float BarcodeAltura = 45f;
        private const float QuietZone = 15f;

        private const float AlturaLinhaMin = 18f;
        private const float AlturaHeaderTabela = 22f;
        private const float PaddingCelula = 4f;
        private const float AlturaLinhaTotal = 22f;

        /// <summary>Bloco de fechamento do grid: linha TOTAL, mais a linha VALOR
        /// NEGOCIADO quando existir. Usado também no lookahead da paginação — as duas
        /// linhas nunca se separam do último item.</summary>
        private float AlturaBlocoTotais => AlturaLinhaTotal * (_temNegociado ? 2f : 1f);
        private const float AlturaRodape = 50f;
        private const float AlturaFaixaPagina = 16f;   // reserva no pé p/ "Página X de Y"
        // Slot da logomarca: aspecto ~2,38 da ARTE RECORTADA (Resources/logo-distribuidora.png
        // já vem sem as bordas vazias do arquivo original — o conteúdo preenche o slot inteiro).
        private const float LogoSlotW = 238f;
        private const float LogoSlotH = 100f;

        // Proporções das colunas (soma = 100%). VR. UNITÁRIO/TOTAL com 12%:
        // comportam "1.234.567,89"; QTD 9% comporta "20.000,00".
        private static readonly float[] ColPercent = [10f, 33f, 12f, 12f, 9f, 12f, 12f];
        private static readonly string[] ColHeaders =
            ["MATRÍCULA", "DESCRIÇÃO", "MARCA", "REFERÊNCIA", "QTD", "VR. UNITÁRIO", "VR. TOTAL"];
        private static readonly StringAlignment[] ColAlign =
        [
            StringAlignment.Near, StringAlignment.Near, StringAlignment.Near, StringAlignment.Near,
            StringAlignment.Far, StringAlignment.Far, StringAlignment.Far
        ];

        /// <summary>
        /// Logomarca embutida no EXE (Resources\logo-distribuidora.png). O MemoryStream
        /// NÃO pode ser descartado: Image.FromStream mantém referência ao stream pela
        /// vida da imagem. Recurso ausente/corrompido → relatório sai sem logo.
        /// </summary>
        private static readonly Lazy<Image?> LogoEmpresa = new(() =>
        {
            try
            {
                using var s = typeof(RelatorioCopiaPedidoPrintDoc).Assembly
                    .GetManifestResourceStream("logo-distribuidora.png");
                if (s == null)
                {
                    Logger.Warn("Recurso 'logo-distribuidora.png' não encontrado; Cópia de Pedido sairá sem logomarca.");
                    return null;
                }
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                return Image.FromStream(ms);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Logomarca indisponível: {ex.Message}");
                return null;
            }
        });

        public RelatorioCopiaPedidoPrintDoc(CopiaPedidoCabecalho cab, List<CopiaPedidoItem> itens,
            EmpresaDados empresa, bool paisagem)
        {
            _cab = cab;
            _itens = itens;
            _empresa = empresa;
            _somaItens = itens.Sum(i => i.VrTotal);
            _temNegociado = cab.VrNegociado != 0m;
            // NÃO trocar para true: o ResetTransform em GeometriaPagina.PrepararPagina
            // anularia a compensação do controller (ver comentário na separação).
            OriginAtMargins = false;
            // Margens ficam nas 40 (10mm) do AplicarConfigA4: documento de cliente
            // pede respiro visual; densidade de itens não é objetivo aqui.
            PrintingHelper.AplicarConfigA4(this, landscape: paisagem);
        }

        protected override void OnBeginPrint(PrintEventArgs e)
        {
            base.OnBeginPrint(e);
            _numeroPagina = 0;
            _estado = new EstadoPaginacao();
            _totalPaginas = null;
            _alturasLinhas = null;
            _preview = e.PrintAction == PrintAction.PrintToPreview;
            // Uma medição por JOB; OnBeginPrint roda depois do PrintDialog.
            _geo = PrintingHelper.GeometriaPagina.Medir(DefaultPageSettings);
        }

        protected override void OnEndPrint(PrintEventArgs e)
        {
            base.OnEndPrint(e);
            // Sem regra de negócio (a Cópia nunca avança status). Só sentinela de
            // deriva entre a simulação de paginação e a renderização de verdade.
            if (_totalPaginas is int previsto && !e.Cancel && _numeroPagina != previsto)
                Logger.Warn($"Cópia de Pedido {_cab.Orcamento}: simulação previu {previsto} página(s), renderizou {_numeroPagina}.");
        }

        protected override void OnPrintPage(PrintPageEventArgs e)
        {
            base.OnPrintPage(e);
            _numeroPagina++;

            var g = e.Graphics!;
            _geo ??= PrintingHelper.GeometriaPagina.Medir(e.PageSettings);
            var area = _geo.PrepararPagina(e, _preview);
            if (_numeroPagina == 1)
            {
                PrintingHelper.LogarPagina(e, "Cópia de Pedido", area, _geo, _preview);
                // Total de páginas ANTES de qualquer desenho: "Página X de Y" já sai
                // correto na própria página 1. Usa o mesmo Graphics e o mesmo código
                // de layout — deriva entre simulação e render é impossível por construção.
                SimularPaginacao(g, area);
            }

            LayoutPagina(g, area, _estado, _numeroPagina, medir: false);
            e.HasMorePages = !_estado.RodapeOk;
        }

        private void SimularPaginacao(Graphics g, RectangleF area)
        {
            float[] colW = CalcularColunas(area.Width);
            _alturasLinhas = new float[_itens.Count];
            for (int i = 0; i < _itens.Count; i++)
                _alturasLinhas[i] = RelatorioTabela.MedirAlturaLinha(g, colW, ValoresItem(_itens[i]),
                    FontDados, PaddingCelula, AlturaLinhaMin);

            var sim = new EstadoPaginacao();
            int paginas = 0;
            // Trava dura espelhando o guard "ao menos 1 item por página" do laço real.
            while (!sim.RodapeOk && paginas < 999)
                LayoutPagina(g, area, sim, ++paginas, medir: true);
            _totalPaginas = paginas;
        }

        /// <summary>
        /// Desenha (medir=false) OU só mede (medir=true) uma página, avançando o
        /// EstadoPaginacao. TODO g.Draw*/Fill* fica sob "if (!medir)"; toda a
        /// aritmética de y roda nos dois modos — é isso que garante que a simulação
        /// pagina exatamente como a renderização.
        /// </summary>
        private void LayoutPagina(Graphics g, RectangleF area, EstadoPaginacao st, int pagina, bool medir)
        {
            float x = area.Left;
            float y = area.Top;
            float larguraTotal = area.Width;
            float[] colW = CalcularColunas(larguraTotal);
            float limiteY = area.Bottom - AlturaFaixaPagina;

            using var penBorda = new Pen(Color.Black, 1.2f);
            using var penLinha = new Pen(Color.FromArgb(180, 180, 180), 0.6f);

            // Defensivo: sem itens não há linha TOTAL — deixa o rodapé sair direto
            // (o handler nem constrói o documento com lista vazia).
            if (_itens.Count == 0) st.TotalOk = true;
            // Página que começa só com o rodapé pendente (itens e TOTAL já emitidos).
            bool soRodapeNestaPagina = st.Item >= _itens.Count && st.TotalOk;

            if (pagina == 1)
            {
                y = DesenharFaixaLegal(g, x, y, larguraTotal, medir);
                y = DesenharCabecalhoEmpresa(g, penBorda, x, y, larguraTotal, medir);
                y = DesenharBlocoCliente(g, penBorda, x, y, larguraTotal, medir);
            }
            else
            {
                y = DesenharCabecalhoResumido(g, penBorda, x, y, larguraTotal, medir);
            }

            // === TABELA DE ITENS ===
            if (st.Item < _itens.Count)
            {
                float tabelaTop = y;
                if (!medir)
                {
                    g.DrawLine(penBorda, x, tabelaTop, x + larguraTotal, tabelaTop);
                    RelatorioTabela.DrawRow(g, colW, ColHeaders, FontHeader, x, y,
                        AlturaHeaderTabela, PaddingCelula, ColAlign, quebrarLinha: false);
                }
                y += AlturaHeaderTabela;
                if (!medir) g.DrawLine(penBorda, x, y, x + larguraTotal, y);
                y += 1;

                int itensNestaPagina = 0;
                while (st.Item < _itens.Count)
                {
                    float alturaLinha = _alturasLinhas![st.Item];
                    // Lookahead: o ÚLTIMO item precisa caber junto com o bloco de totais
                    // (TOTAL + eventual VALOR NEGOCIADO), para ele nunca abrir página
                    // sozinho (órfão).
                    float alturaNecessaria = alturaLinha
                        + (st.Item == _itens.Count - 1 ? AlturaBlocoTotais : 0f);
                    // Sempre ao menos UMA linha por página: sem isso, área degenerada
                    // deixaria HasMorePages=true para sempre (job infinito).
                    if (y + alturaNecessaria > limiteY && itensNestaPagina > 0)
                        break;

                    itensNestaPagina++;

                    if (!medir)
                    {
                        RelatorioTabela.DrawRow(g, colW, ValoresItem(_itens[st.Item]), FontDados,
                            x, y, alturaLinha, PaddingCelula, ColAlign, quebrarLinha: true);
                    }
                    y += alturaLinha;
                    if (!medir) g.DrawLine(penLinha, x, y, x + larguraTotal, y);
                    st.Item++;
                }

                if (!medir)
                {
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
                }

                // Linha TOTAL fecha o grid na mesma página do último item (garantido
                // pelo lookahead acima).
                if (st.Item >= _itens.Count && !st.TotalOk)
                {
                    y = DesenharLinhaTotal(g, penBorda, colW, x, y, larguraTotal, medir);
                    st.TotalOk = true;
                }
            }

            // Rodapé do pedido: só depois de itens + TOTAL, e só se couber inteiro;
            // senão HasMorePages continua true e ele abre a próxima página sozinho.
            // Garantia de progresso (espelho do guard itensNestaPagina > 0): numa página
            // de continuação em que só resta o rodapé, ele sai mesmo sem caber — senão,
            // com área útil pequena, nenhuma página o comporta e o job real nunca
            // termina (a simulação para em 999, a impressão não).
            if (st.Item >= _itens.Count && st.TotalOk && !st.RodapeOk)
            {
                if (y + 6f + AlturaRodape <= limiteY || (pagina > 1 && soRodapeNestaPagina))
                {
                    DesenharRodapePedido(g, penBorda, x, y + 6f, larguraTotal, medir);
                    st.RodapeOk = true;
                }
            }

            if (!medir)
            {
                using var sfPagina = new StringFormat { Alignment = StringAlignment.Far };
                string rotulo = _totalPaginas is int total
                    ? $"Página {pagina} de {total}"
                    : $"Página {pagina}";
                g.DrawString(rotulo, FontDados, Brushes.Black,
                    new RectangleF(x, area.Bottom - 15f, larguraTotal, 15f), sfPagina);
            }
        }

        private static float[] CalcularColunas(float larguraTotal)
        {
            var colW = new float[ColPercent.Length];
            for (int i = 0; i < ColPercent.Length; i++)
                colW[i] = larguraTotal * ColPercent[i] / 100f;
            return colW;
        }

        private static string[] ValoresItem(CopiaPedidoItem item) =>
        [
            item.Matricula,
            item.Descricao,
            item.Marca,
            item.Referencia,
            item.Quantidade.ToString("N2", PtBr),
            item.VrUnitario.ToString("N2", PtBr),
            item.VrTotal.ToString("N2", PtBr)
        ];

        /// <summary>
        /// Altura para desenhar o texto com quebra, entre 1 e maxLinhas linhas
        /// tipográficas COMPLETAS (Font.GetHeight) — retângulo nunca menor que a
        /// linha da fonte, que era o que cortava a parte de baixo de "g", "q" etc.
        /// </summary>
        private static float MedirTexto(Graphics g, string texto, Font fonte, float largura, int maxLinhas)
        {
            float lh = fonte.GetHeight(g);
            if (string.IsNullOrEmpty(texto)) return lh + 1f;
            float medida = g.MeasureString(texto, fonte, (int)Math.Max(1f, largura)).Height;
            return Math.Min(Math.Max(medida + 1f, lh + 1f), lh * maxLinhas + 2f);
        }

        private static float DesenharFaixaLegal(Graphics g, float x, float y, float larguraTotal, bool medir)
        {
            float alturaTexto = g.MeasureString(FraseLegal, FontFaixaLegal, (int)(larguraTotal - 12f)).Height;
            float altura = Math.Max(20f, alturaTexto + 6f);
            if (!medir)
            {
                g.FillRectangle(Brushes.Black, x, y, larguraTotal, altura);
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(FraseLegal, FontFaixaLegal, Brushes.White,
                    new RectangleF(x + 6f, y, larguraTotal - 12f, altura), sf);
            }
            return y + altura + 6f;
        }

        private float DesenharCabecalhoEmpresa(Graphics g, Pen penBorda, float x, float y, float larguraTotal, bool medir)
        {
            string digitos = _cab.Orcamento.ToString();
            float bcLargura = Code128.MedirLargura(digitos, BarcodeModulo);
            // Bloco direito largo o bastante para "PEDIDO Nº ..." em 14pt: com 180 fixo,
            // orçamentos de 6+ dígitos saíam truncados ("PEDIDO Nº 7654…") no Graphics
            // da impressora. Medido no MESMO Graphics nos dois modos (sem deriva).
            string tituloPedido = $"PEDIDO Nº {_cab.Orcamento}";
            float larguraTitulo = g.MeasureString(tituloPedido, FontTitulo).Width + 4f;
            float larguraDireita = Math.Min(larguraTotal * 0.45f,
                Math.Max(Math.Max(bcLargura + QuietZone * 2f, 180f), larguraTitulo));
            float xDireita = x + larguraTotal - larguraDireita;

            // Esquerda: logomarca (slot some quando o recurso não existe)
            float larguraLogo = DesenharLogo(g, x, y, medir);
            float xEmpresa = x + (larguraLogo > 0f ? larguraLogo + 12f : 0f);
            float larguraEmpresa = xDireita - QuietZone - xEmpresa;

            // Quebra em até 2 linhas por campo, reticências só como último recurso;
            // alturas SEMPRE pela métrica da fonte (MedirTexto) — retângulo apertado
            // era o que decapitava os descendentes de "g"/"q".
            using var sfQuebra = new StringFormat { Trimming = StringTrimming.EllipsisWord };

            // Meio: dados da empresa (campos vazios do config são omitidos)
            float yEmpresa = y;
            if (!string.IsNullOrWhiteSpace(_empresa.RazaoSocial))
            {
                float h = MedirTexto(g, _empresa.RazaoSocial, FontSubtitulo, larguraEmpresa, 2);
                if (!medir) g.DrawString(_empresa.RazaoSocial, FontSubtitulo, Brushes.Black,
                    new RectangleF(xEmpresa, yEmpresa, larguraEmpresa, h), sfQuebra);
                yEmpresa += h + 1f;
            }
            foreach (string linha in LinhasEmpresa())
            {
                float h = MedirTexto(g, linha, FontEmpresa, larguraEmpresa, 2);
                if (!medir) g.DrawString(linha, FontEmpresa, Brushes.Black,
                    new RectangleF(xEmpresa, yEmpresa, larguraEmpresa, h), sfQuebra);
                yEmpresa += h;
            }

            // Direita: identificação do documento + barcode (uma linha cada, altura da fonte)
            float lhSubtitulo = FontSubtitulo.GetHeight(g) + 1f;
            float lhTitulo = FontTitulo.GetHeight(g) + 1f;
            float lhEmpresaFonte = FontEmpresa.GetHeight(g) + 1f;
            float yDireita = y;
            using var sfDireita = new StringFormat
            {
                Alignment = StringAlignment.Far,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
            if (!medir) g.DrawString("CÓPIA DE PEDIDO", FontSubtitulo, Brushes.Black,
                new RectangleF(xDireita, yDireita, larguraDireita, lhSubtitulo), sfDireita);
            yDireita += lhSubtitulo;
            if (!medir) g.DrawString(tituloPedido, FontTitulo, Brushes.Black,
                new RectangleF(xDireita, yDireita, larguraDireita, lhTitulo), sfDireita);
            yDireita += lhTitulo;
            if (!medir) g.DrawString($"{RotuloData} {_cab.DataPedido}", FontEmpresa, Brushes.Black,
                new RectangleF(xDireita, yDireita, larguraDireita, lhEmpresaFonte), sfDireita);
            yDireita += lhEmpresaFonte + 2f;
            float bcX = x + larguraTotal - bcLargura - QuietZone;
            if (!medir)
            {
                Code128.Desenhar(g, digitos, bcX, yDireita, BarcodeModulo, BarcodeAltura);
                using var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString(digitos, FontEmpresa, Brushes.Black,
                    new RectangleF(bcX, yDireita + BarcodeAltura + 2f, bcLargura, lhEmpresaFonte), sfCenter);
            }
            yDireita += BarcodeAltura + 2f + lhEmpresaFonte + 2f;

            float yLogo = larguraLogo > 0f ? y + LogoSlotH : y;
            float yFim = Math.Max(Math.Max(yLogo, yEmpresa), yDireita) + 4f;
            if (!medir) g.DrawLine(penBorda, x, yFim, x + larguraTotal, yFim);
            return yFim + 6f;
        }

        /// <summary>Linhas secundárias do bloco da empresa, já sem campos vazios.</summary>
        private IEnumerable<string> LinhasEmpresa()
        {
            string docs = JuntarNaoVazios(" • ",
                PrefixarSeNaoVazio("CNPJ: ", _empresa.Cnpj),
                PrefixarSeNaoVazio("IE: ", _empresa.InscricaoEstadual));
            if (docs.Length > 0) yield return docs;
            if (!string.IsNullOrWhiteSpace(_empresa.Endereco)) yield return _empresa.Endereco.Trim();
            if (!string.IsNullOrWhiteSpace(_empresa.CidadeUfCep)) yield return _empresa.CidadeUfCep.Trim();
            string contato = JuntarNaoVazios(" • ",
                PrefixarSeNaoVazio("Fone: ", _empresa.Telefone),
                PrefixarSeNaoVazio("E-mail: ", _empresa.Email));
            if (contato.Length > 0) yield return contato;
            string whats = PrefixarSeNaoVazio("WhatsApp: ", _empresa.WhatsApp);
            if (whats.Length > 0) yield return whats;
        }

        private static float DesenharLogo(Graphics g, float x, float y, bool medir)
        {
            var logo = LogoEmpresa.Value;
            if (logo == null) return 0f;
            float esc = Math.Min(LogoSlotW / logo.Width, LogoSlotH / logo.Height);
            float w = logo.Width * esc;
            float h = logo.Height * esc;
            if (!medir)
            {
                var im = g.InterpolationMode;
                var po = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(logo, x, y + (LogoSlotH - h) / 2f, w, h);
                g.InterpolationMode = im;
                g.PixelOffsetMode = po;
            }
            return LogoSlotW;
        }

        private float DesenharBlocoCliente(Graphics g, Pen penBorda, float x, float y, float larguraTotal, bool medir)
        {
            const float pad = 6f;
            float topo = y;
            float yIn = y + pad;
            float xEsq = x + pad;
            float larguraEsq = larguraTotal * 0.55f - pad * 2f;
            float xDir = x + larguraTotal * 0.55f + pad;
            float larguraDir = larguraTotal * 0.45f - pad * 2f;

            // Quebra em até 2 linhas por campo (nome+fantasia e endereço longos não
            // são mais cortados); altura de cada PAR de colunas = lado mais alto,
            // sempre pela métrica da fonte (MedirTexto) — nada de decapitar "g"/"q".
            using var sfQuebra = new StringFormat { Trimming = StringTrimming.EllipsisWord };

            string cliente = $"CLIENTE: {_cab.ClienteCodigo} — {_cab.ClienteNome}";
            if (!string.IsNullOrWhiteSpace(_cab.Fantasia))
                cliente += $"  ({_cab.Fantasia})";

            (string rotuloDoc, string rotuloId) = _cab.Natureza switch
            {
                "F" => ("CPF", "RG"),
                "J" => ("CNPJ", "IE"),
                _ => ("CPF/CNPJ", "RG/IE")
            };
            string doc = FormatarCpfCnpj(_cab.CpfCnpj, _cab.Natureza);
            string linhaDoc = JuntarNaoVazios("   ",
                $"{rotuloDoc}: {(doc.Length > 0 ? doc : "—")}",
                PrefixarSeNaoVazio($"{rotuloId}: ", _cab.Identidade));

            string fones = JuntarNaoVazios(" / ",
                FormatarFone(_cab.Ddd, _cab.Fone),
                FormatarFone(_cab.DddCelular, _cab.Celular));
            string linhaFone = $"TELEFONE: {(fones.Length > 0 ? fones : "—")}";

            string endereco = _cab.Endereco;
            string cep = FormatarCep(_cab.Cep);
            if (cep.Length > 0) endereco = JuntarNaoVazios(" — ", endereco, $"CEP {cep}");
            string linhaEndereco = $"ENDEREÇO: {(endereco.Length > 0 ? endereco : "—")}";
            string cidadeUf = string.Join("/", new[] { _cab.Cidade, _cab.Estado }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            string linhaCidade = $"CIDADE/UF: {(cidadeUf.Length > 0 ? cidadeUf : "—")}";
            string linhaEmail = $"E-MAIL: {(string.IsNullOrWhiteSpace(_cab.Email) ? "—" : _cab.Email.Trim())}";

            string[] esquerda = [cliente, linhaDoc, linhaFone];
            string[] direita = [linhaEndereco, linhaCidade, linhaEmail];
            for (int i = 0; i < esquerda.Length; i++)
            {
                float h = Math.Max(MedirTexto(g, esquerda[i], FontCabecalho, larguraEsq, 2),
                                   MedirTexto(g, direita[i], FontCabecalho, larguraDir, 2));
                if (!medir)
                {
                    g.DrawString(esquerda[i], FontCabecalho, Brushes.Black,
                        new RectangleF(xEsq, yIn, larguraEsq, h), sfQuebra);
                    g.DrawString(direita[i], FontCabecalho, Brushes.Black,
                        new RectangleF(xDir, yIn, larguraDir, h), sfQuebra);
                }
                yIn += h + 1f;
            }

            string vendedor = string.IsNullOrWhiteSpace(_cab.VendedorNome)
                ? _cab.VendedorCodigo
                : $"{_cab.VendedorCodigo} — {_cab.VendedorNome}";
            string textoVendedor = $"VENDEDOR: {vendedor}";
            string textoNf = $"Nº NOTA FISCAL: {(string.IsNullOrWhiteSpace(_cab.NotaFiscal) ? "—" : _cab.NotaFiscal)}";
            float larguraVend = larguraTotal * 0.62f - pad * 2f;
            float larguraNf = larguraTotal * 0.38f - pad;
            float hUltima = Math.Max(MedirTexto(g, textoVendedor, FontCabecalho, larguraVend, 2),
                                     MedirTexto(g, textoNf, FontCabecalho, larguraNf, 1));
            if (!medir)
            {
                g.DrawString(textoVendedor, FontCabecalho, Brushes.Black,
                    new RectangleF(xEsq, yIn, larguraVend, hUltima), sfQuebra);
                using var sfNf = new StringFormat
                {
                    Alignment = StringAlignment.Far,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(textoNf, FontCabecalho, Brushes.Black,
                    new RectangleF(x + larguraTotal * 0.62f, yIn, larguraNf, hUltima), sfNf);
            }
            yIn += hUltima + 1f;

            if (!string.IsNullOrWhiteSpace(_cab.Observacao))
            {
                string obs = $"OBSERVAÇÃO: {_cab.Observacao}";
                float alturaObs = MedirTexto(g, obs, FontCabecalho, larguraTotal - pad * 2f, 3);
                if (!medir)
                {
                    g.DrawString(obs, FontCabecalho, Brushes.Black,
                        new RectangleF(xEsq, yIn, larguraTotal - pad * 2f, alturaObs), sfQuebra);
                }
                yIn += alturaObs;
            }

            float baixo = yIn + pad;
            if (!medir) g.DrawRectangle(penBorda, x, topo, larguraTotal, baixo - topo);
            return baixo + 8f;
        }

        private float DesenharCabecalhoResumido(Graphics g, Pen penBorda, float x, float y, float larguraTotal, bool medir)
        {
            if (!medir)
            {
                using var sf = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString($"CÓPIA DE PEDIDO {_cab.Orcamento} — {_cab.ClienteNome} (continuação)",
                    FontHeader, Brushes.Black, new RectangleF(x, y, larguraTotal, 18f), sf);
            }
            y += 20f;
            if (!medir) g.DrawLine(penBorda, x, y, x + larguraTotal, y);
            return y + 4f;
        }

        private float DesenharLinhaTotal(Graphics g, Pen penBorda, float[] colW, float x, float y, float larguraTotal, bool medir)
        {
            if (!medir)
            {
                float larguraRotulo = colW.Take(colW.Length - 1).Sum();
                float alturaBloco = AlturaBlocoTotais;
                using var bg = new SolidBrush(Color.FromArgb(232, 232, 232));
                g.FillRectangle(bg, x, y, larguraTotal, alturaBloco);
                g.DrawRectangle(penBorda, x, y, larguraTotal, alturaBloco);
                g.DrawLine(penBorda, x + larguraRotulo, y, x + larguraRotulo, y + alturaBloco);

                using var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Far,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString($"TOTAL ({_itens.Count} {(_itens.Count == 1 ? "item" : "itens")})",
                    FontTotal, Brushes.Black,
                    new RectangleF(x + PaddingCelula, y, larguraRotulo - PaddingCelula * 2f, AlturaLinhaTotal), sf);
                g.DrawString(_somaItens.ToString("C2", PtBr), FontTotal, Brushes.Black,
                    new RectangleF(x + larguraRotulo + PaddingCelula, y, colW[^1] - PaddingCelula * 2f, AlturaLinhaTotal), sf);

                if (_temNegociado)
                {
                    // Abatimento interno de créditos etc. — só aparece quando ≠ 0.
                    float yNeg = y + AlturaLinhaTotal;
                    g.DrawLine(penBorda, x, yNeg, x + larguraTotal, yNeg);
                    g.DrawString("VALOR NEGOCIADO", FontTotal, Brushes.Black,
                        new RectangleF(x + PaddingCelula, yNeg, larguraRotulo - PaddingCelula * 2f, AlturaLinhaTotal), sf);
                    g.DrawString(_cab.VrNegociado.ToString("C2", PtBr), FontTotal, Brushes.Black,
                        new RectangleF(x + larguraRotulo + PaddingCelula, yNeg, colW[^1] - PaddingCelula * 2f, AlturaLinhaTotal), sf);
                }
            }
            return y + AlturaBlocoTotais;
        }

        private float DesenharRodapePedido(Graphics g, Pen penBorda, float x, float y, float larguraTotal, bool medir)
        {
            if (!medir)
            {
                g.DrawRectangle(penBorda, x, y, larguraTotal, AlturaRodape);
                using var sfLinha = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                const float pad = 6f;
                float meiaLargura = larguraTotal / 2f;
                static string V(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
                // Altura de célula pela métrica da fonte (17f > linha de 8,5pt) —
                // valores com "g"/"q"/"kg" não perdem os descendentes.
                (string texto, float cx, float cy)[] celulas =
                [
                    ($"FORMA DE PAGAMENTO: {V(_cab.Texto01)}", x + pad, y + 6f),
                    ($"NÚMERO DO PEDIDO: {V(_cab.Texto02)}", x + meiaLargura + pad, y + 6f),
                    ($"VOLUMES: {V(_cab.Texto03)}", x + pad, y + 26f),
                    ($"PESO: {V(_cab.Texto04)}", x + meiaLargura + pad, y + 26f)
                ];
                foreach (var (texto, cx, cy) in celulas)
                    g.DrawString(texto, FontCabecalho, Brushes.Black,
                        new RectangleF(cx, cy, meiaLargura - pad * 2f, 17f), sfLinha);
            }
            return y + AlturaRodape;
        }

        // ===== Máscaras =====

        private static string SoDigitos(string s) =>
            new([.. s.Where(char.IsDigit)]);

        /// <summary>
        /// CPF 000.000.000-00 / CNPJ 00.000.000/0000-00. O banco guarda só dígitos;
        /// a natureza repõe zeros à esquerda eventualmente perdidos. Comprimento
        /// desconhecido imprime o valor cru (nunca inventa máscara).
        /// </summary>
        private static string FormatarCpfCnpj(string bruto, string natureza)
        {
            var d = SoDigitos(bruto);
            if (d.Length == 0) return "";
            if (natureza == "F" && d.Length < 11) d = d.PadLeft(11, '0');
            else if (natureza == "J" && d.Length is > 11 and < 14) d = d.PadLeft(14, '0');
            return d.Length switch
            {
                11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
                14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
                _ => bruto.Trim()
            };
        }

        private static string FormatarFone(string ddd, string numero)
        {
            var n = SoDigitos(numero);
            if (n.Length == 0) return "";
            string corpo = n.Length switch
            {
                8 => $"{n[..4]}-{n[4..]}",
                9 => $"{n[..5]}-{n[5..]}",
                _ => n
            };
            var d = SoDigitos(ddd);
            return d.Length > 0 ? $"({d}) {corpo}" : corpo;
        }

        private static string FormatarCep(string bruto)
        {
            var d = SoDigitos(bruto);
            return d.Length == 8 ? $"{d[..5]}-{d[5..]}" : bruto.Trim();
        }

        private static string PrefixarSeNaoVazio(string prefixo, string valor) =>
            string.IsNullOrWhiteSpace(valor) ? "" : prefixo + valor.Trim();

        private static string JuntarNaoVazios(string separador, params string[] partes) =>
            string.Join(separador, partes.Where(p => !string.IsNullOrEmpty(p)));
    }
}
