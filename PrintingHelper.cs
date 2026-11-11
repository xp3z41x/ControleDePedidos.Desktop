using System.Drawing.Printing;

namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Helper para garantir que documentos A4 sejam impressos corretamente
    /// independentemente da impressora padrão do Windows do usuário
    /// (que pode ser térmica, de etiqueta, ou outra que não suporta A4).
    ///
    /// Por padrão, <see cref="PrintDocument"/> herda <see cref="PrinterSettings"/>
    /// da impressora default do Windows. Se essa for uma térmica 10x15, o preview
    /// e a impressão calculam layout para 4x6 polegadas — quebrando relatórios A4.
    /// Esta classe pré-seleciona uma impressora A4-capable no construtor da PrintDoc:
    /// a PADRÃO do usuário quando ela serve (A4 e não térmica), senão a primeira
    /// A4 não-térmica instalada (ver <see cref="EscolherImpressoraA4"/>).
    /// </summary>
    public static class PrintingHelper
    {
        // Heurística para excluir impressoras claramente não-A4 (térmicas, etiqueta, POS).
        // "fiscal"/"etq"/"etiquet": convenção de nomes do RDS (COL-FISCAL, ETQ-ECOMMERCE,
        // ETIQUETADORA) — drivers genéricos dessas costumam declarar A4 mesmo sendo bobina.
        private static readonly string[] PalavrasTermicas =
            ["thermal", "térmica", "termica", "pos ", "label", "zebra",
             "bematech", "epson tm", "daruma", "elgin", "10x15", "4x6",
             "fiscal", "cupom", "bobina", "etiquet", "etq", "80mm", "58mm"];

        internal static bool PareceTermica(string nome) =>
            PalavrasTermicas.Any(palavra => nome.Contains(palavra, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// PaperSize padrão A4: 210x297mm = 827x1169 centésimos de polegada.
        /// </summary>
        public static PaperSize CriarPaperSizeA4() =>
            new("A4", 827, 1169) { RawKind = (int)PaperKind.A4 };

        /// <summary>
        /// Geometria FÍSICA da página em centésimos de polegada, no sistema de
        /// coordenadas do PAPEL (origem = canto físico da folha) — o MESMO referencial
        /// de <see cref="PrintPageEventArgs.MarginBounds"/>.
        ///
        /// POR QUE ISTO EXISTE
        /// Com OriginAtMargins=false o WinForms não aplica transform nenhum, e os dois
        /// controllers entregam origens DIFERENTES:
        ///   - impressão real (StandardPrintController): o Graphics vem de um DC de
        ///     impressora, cuja origem é o canto da ÁREA IMPRIMÍVEL (o GDI já desconta
        ///     a faixa física que a impressora não alcança);
        ///   - preview (PreviewPrintController): o Graphics vem de um metafile do
        ///     tamanho de PageBounds, ou seja origem = canto do PAPEL.
        /// Como e.MarginBounds é SEMPRE calculado a partir do canto do papel, na
        /// impressão real todo o desenho saía deslocado ~4mm para a direita e para
        /// baixo, jogando a última coluna para fora da área imprimível — o corte que
        /// não aparecia no preview.
        /// </summary>
        public sealed class GeometriaPagina
        {
            /// <summary>Retângulo fisicamente imprimível, em coordenadas do PAPEL (1/100").</summary>
            public RectangleF Imprimivel { get; private init; }

            /// <summary>false = o driver não informou nada coerente e assumimos a folha
            /// inteira (mesmo comportamento de antes da correção — degradação segura).</summary>
            public bool Confiavel { get; private init; }

            private string _bruto = "";

            /// <summary>
            /// Mede a geometria da impressora atual. Nunca lança: qualquer falha
            /// (impressora offline/removida) cai para "folha inteira".
            /// Chamar UMA VEZ POR JOB (em OnBeginPrint): cada leitura de
            /// PrintableArea/HardMargin cria e destrói um DC de impressora, o que
            /// custa dezenas de ms em impressora de rede.
            /// </summary>
            public static GeometriaPagina Medir(PageSettings ps)
            {
                Rectangle papel;
                try { papel = ps.Bounds; }   // Bounds JÁ respeita Landscape (troca W/H)
                catch { papel = new Rectangle(0, 0, 827, 1169); }

                GeometriaPagina FolhaInteira(string motivo) => new()
                {
                    Imprimivel = new RectangleF(0, 0, papel.Width, papel.Height),
                    Confiavel = false,
                    _bruto = $"papel={papel.Width}x{papel.Height} land={ps.Landscape} [{motivo}]"
                };

                try
                {
                    // HardMarginX/Y são orientação-aware, mas o getter do .NET divide em
                    // aritmética INTEIRA (reporta 16 onde o físico é 16,67) e não informa
                    // a margem inferior/direita. Servem só para DESEMPATAR a orientação.
                    float hx = ps.HardMarginX;
                    float hy = ps.HardMarginY;

                    // PrintableArea traz os offsets em float E a extensão imprimível real
                    // — é de lá que sai a margem física inferior/direita, que pode ser
                    // MAIOR que a superior/esquerda (típico de jato de tinta). Por isso
                    // não se pode simplesmente encolher PageBounds de forma simétrica.
                    //
                    // ARMADILHA: em paisagem alguns drivers trocam X<->Y e W<->H sobre um
                    // DC que o GDI já rotacionou (troca dupla). Testamos as duas variantes
                    // e usamos a coerente — assim o código continua correto se a troca
                    // deixar de acontecer numa versão futura do .NET.
                    var pa = ps.PrintableArea;
                    var trocado = new RectangleF(pa.Y, pa.X, pa.Height, pa.Width);
                    string bruto = $"papel={papel.Width}x{papel.Height} land={ps.Landscape} " +
                                   $"hard=({hx};{hy}) printableArea={pa}";

                    RectangleF imp;
                    if (Coerente(pa, hx, hy, papel)) imp = pa;
                    else if (Coerente(trocado, hx, hy, papel)) imp = trocado;
                    else
                    {
                        Logger.Warn($"Geometria da página incoerente; assumindo folha inteira. {bruto}");
                        return FolhaInteira(bruto);
                    }

                    return new GeometriaPagina { Imprimivel = imp, Confiavel = true, _bruto = bruto };
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Falha ao medir geometria da página " +
                                $"({ex.GetType().Name}: {ex.Message}); assumindo folha inteira.");
                    return FolhaInteira(ex.GetType().Name);
                }

                // Aceita só valores fisicamente plausíveis. Tolerância de 1,5 centésimos
                // porque HardMargin vem truncado (erro sempre < 1).
                static bool Coerente(RectangleF r, float hx, float hy, Rectangle papel) =>
                    float.IsFinite(r.X) && float.IsFinite(r.Y) &&
                    float.IsFinite(r.Width) && float.IsFinite(r.Height) &&
                    r.X >= 0f && r.Y >= 0f &&
                    r.Width > papel.Width * 0.5f && r.Height > papel.Height * 0.5f &&
                    r.Right <= papel.Width + 1f && r.Bottom <= papel.Height + 1f &&
                    Math.Abs(r.X - hx) <= 1.5f && Math.Abs(r.Y - hy) <= 1.5f;
            }

            /// <summary>
            /// Prepara o Graphics da página e devolve a área de desenho em coordenadas
            /// do PAPEL (1/100"), IDÊNTICA no preview e na impressão real. Chamar como
            /// primeira coisa do OnPrintPage.
            ///
            /// ATENÇÃO — invariante frágil:
            ///   - o TRANSLATE depende do modo (só na impressão real): é ele que faz o
            ///     papel coincidir com o preview;
            ///   - o CLAMP nunca depende do modo — ele define a largura das colunas e
            ///     quantos itens cabem por página. Movê-lo para dentro do "if (real)"
            ///     faria preview e papel paginarem diferente.
            /// </summary>
            public RectangleF PrepararPagina(PrintPageEventArgs e, bool preview)
            {
                var g = e.Graphics!;

                // ResetTransform antes do translate: garante identidade como base e evita
                // compensação dupla caso algum controller passe a aplicar transform.
                // (É este ResetTransform que torna OriginAtMargins inócuo — trocá-lo para
                // true não resolveria nada e ainda deslocaria o preview.)
                g.ResetTransform();

                // 1/100 de polegada. DEFINIR ANTES do translate: o offset é interpretado
                // na unidade de página vigente.
                g.PageUnit = GraphicsUnit.Display;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                if (!preview)
                    g.TranslateTransform(-Imprimivel.X, -Imprimivel.Y);

                var area = RectangleF.Intersect(e.MarginBounds, Imprimivel);

                // Nunca devolver área degenerada: sem espaço para uma linha, o laço de
                // itens sairia sem consumir nada e HasMorePages ficaria true para sempre.
                if (area.Width < 50f || area.Height < 50f)
                {
                    Logger.Warn($"Área útil degenerada ({area}); usando MarginBounds. {this}");
                    area = e.MarginBounds;
                }
                return area;
            }

            public override string ToString() =>
                $"imprimivel={Imprimivel} confiavel={Confiavel} [{_bruto}]";
        }

        /// <summary>
        /// Aplica configurações A4 (paper size, landscape, margins) ao documento
        /// E seleciona a impressora (padrão do usuário se A4-capaz e não térmica;
        /// senão a primeira A4 não-térmica instalada — ver <see cref="EscolherImpressoraA4"/>).
        /// Idempotente — pode ser chamado várias vezes (ex: no construtor + após PrintDialog).
        /// </summary>
        public static void AplicarConfigA4(PrintDocument doc, bool landscape)
        {
            var nomeImpressoraA4 = EncontrarImpressoraA4Capaz();
            if (nomeImpressoraA4 != null)
            {
                doc.PrinterSettings.PrinterName = nomeImpressoraA4;
            }
            else
            {
                Logger.Warn("Nenhuma impressora A4-capable encontrada. Usando impressora padrão do Windows.");
            }

            // Trocar PrinterName recarrega DefaultPageSettings — sempre re-aplicar nossos defaults
            doc.DefaultPageSettings.PaperSize = CriarPaperSizeA4();
            doc.DefaultPageSettings.Landscape = landscape;
            doc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
        }

        /// <summary>
        /// Retorna a impressora A4 a usar: a PADRÃO do Windows do usuário quando ela
        /// suporta A4 e não parece térmica; senão a primeira instalada que suporta A4,
        /// preferindo as não-térmicas. Null se nenhuma suporta. Loga a escolha e o motivo.
        ///
        /// Por que a padrão primeiro: no RDS as 16 impressoras de rede de todas as lojas
        /// aparecem para todo operador; "a primeira A4 da enumeração" era a COL-A4 de
        /// outra loja para todo mundo. A padrão do usuário é a da mesa dele.
        /// </summary>
        public static string? EncontrarImpressoraA4Capaz()
        {
            var instaladas = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            string? padrao = ObterNomeImpressoraPadrao(instaladas);

            lock (_lockSelecao)
            {
                if (_selecaoCache is { } c)
                {
                    bool padraoIgual = string.Equals(c.Padrao, padrao, StringComparison.OrdinalIgnoreCase);
                    bool escolhidaExiste = c.Nome == null
                        || instaladas.Contains(c.Nome, StringComparer.OrdinalIgnoreCase);
                    if (padraoIgual && escolhidaExiste) return c.Nome;

                    // Padrão mudou (ou a escolhida sumiu da sessão RDS): recomeça do zero —
                    // tamanhos de papel também podem ter mudado (driver trocado, etc.).
                    Logger.Info($"Cache de impressoras invalidado: padrão '{c.Padrao}' → '{padrao}'" +
                        (escolhidaExiste ? "" : $", '{c.Nome}' não está mais instalada"));
                    _selecaoCache = null;
                    _cacheSuportaA4.Clear();
                }
            }

            var (nome, motivo, completa) = EscolherImpressoraA4(padrao, instaladas, SuportaA4ComCache);
            Logger.Info($"Impressora A4: '{nome ?? "(nenhuma)"}' — {motivo}" +
                (completa ? "" : " [consulta falhou; não cacheado]"));

            // Só cacheia escolha feita sem falhas de consulta — senão a próxima tenta de novo.
            if (completa)
                lock (_lockSelecao) _selecaoCache = (padrao, nome);
            return nome;
        }

        // ── Cache por sessão do processo ──────────────────────────────────────────
        // Consultar PaperSizes de 16 impressoras de REDE custava segundos a cada
        // relatório. Cacheia (a) se cada impressora declara A4 e (b) a escolha final,
        // chaveada pela impressora padrão. Invalidação: padrão mudou, escolhida sumiu
        // da lista instalada, falha de consulta (nunca cacheada) ou InvalidarCacheImpressoras().
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _cacheSuportaA4 =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lockSelecao = new();
        private static (string? Padrao, string? Nome)? _selecaoCache;

        /// <summary>Descarta todo o cache de impressoras (próxima seleção reconsulta tudo).</summary>
        public static void InvalidarCacheImpressoras()
        {
            lock (_lockSelecao)
            {
                _selecaoCache = null;
                _cacheSuportaA4.Clear();
            }
        }

        private static bool? SuportaA4ComCache(string nome)
        {
            if (_cacheSuportaA4.TryGetValue(nome, out var v)) return v;
            var r = ConsultarSuporteA4(nome);
            if (r is bool ok) _cacheSuportaA4[nome] = ok;
            else _cacheSuportaA4.TryRemove(nome, out _);
            return r;
        }

        /// <summary>
        /// Núcleo PURO da seleção (testável sem impressoras reais).
        /// <paramref name="suportaA4"/> devolve null quando a consulta falhou.
        /// <c>Completa</c> = false se alguma consulta relevante falhou (resultado
        /// não deve ser cacheado).
        /// </summary>
        internal static (string? Nome, string Motivo, bool Completa) EscolherImpressoraA4(
            string? padrao, IReadOnlyList<string> instaladas, Func<string, bool?> suportaA4)
        {
            bool completa = true;
            string motivoPadrao;

            if (string.IsNullOrEmpty(padrao))
                motivoPadrao = "sem impressora padrão no Windows";
            else if (PareceTermica(padrao))
                motivoPadrao = $"padrão '{padrao}' parece térmica/etiqueta";
            else
            {
                bool? a4 = suportaA4(padrao);
                if (a4 == true)
                    return (padrao, "impressora padrão do Windows (suporta A4)", true);
                if (a4 == null) completa = false;
                motivoPadrao = a4 == null
                    ? $"falha ao consultar a padrão '{padrao}'"
                    : $"padrão '{padrao}' não declara A4";
            }

            string? primeiraA4 = null;
            foreach (var nome in instaladas)
            {
                if (string.Equals(nome, padrao, StringComparison.OrdinalIgnoreCase)
                    && !PareceTermica(nome)) continue; // já avaliada acima
                bool termica = PareceTermica(nome);
                // Térmica só interessa como último recurso — não consultar se já há uma A4.
                if (termica && primeiraA4 != null) continue;
                bool? a4 = suportaA4(nome);
                if (a4 == null) { completa = false; continue; }
                if (a4 != true) continue;
                if (!termica)
                    return (nome, $"fallback: {motivoPadrao}; primeira A4 não-térmica instalada", completa);
                primeiraA4 ??= nome;
            }
            return primeiraA4 != null
                ? (primeiraA4, $"fallback: {motivoPadrao}; só há A4 com cara de térmica", completa)
                : (null, $"{motivoPadrao}; nenhuma impressora instalada declara A4", completa);
        }

        /// <summary>
        /// Nome da impressora padrão do Windows para o usuário atual, ou null se não
        /// houver (o getter de <see cref="PrinterSettings.PrinterName"/> devolve um
        /// texto tipo "&lt;no default printer&gt;" nesse caso — não é nome instalado).
        /// </summary>
        internal static string? ObterNomeImpressoraPadrao(IReadOnlyList<string> instaladas)
        {
            try
            {
                var nome = new PrinterSettings().PrinterName;
                return instaladas.Contains(nome, StringComparer.OrdinalIgnoreCase) ? nome : null;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao obter impressora padrão: {ex.Message}");
                return null;
            }
        }

        /// <summary>true/false = declara (ou não) A4; null = consulta falhou.</summary>
        private static bool? ConsultarSuporteA4(string nome)
        {
            try
            {
                var ps = new PrinterSettings { PrinterName = nome };
                if (!ps.IsValid) return null;
                return ps.PaperSizes.Cast<PaperSize>().Any(p => p.Kind == PaperKind.A4);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao consultar impressora '{nome}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retorna true se a impressora atualmente atribuída ao documento
        /// declara suporte a A4 nas suas PaperSizes.
        /// </summary>
        public static bool ImpressoraSuportaA4(PrintDocument doc)
        {
            try
            {
                return doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
                    .Any(p => p.Kind == PaperKind.A4);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Preview maximizado com o botão nativo de impressão substituído por
        /// "Imprimir..." que abre o PrintDialog. O botão nativo imprime DIRETO na
        /// impressora pré-selecionada, sem escolha nem feedback — se a fila dela
        /// estiver travada, "nada acontece" (histórico: bug da v11.1).
        ///
        /// <paramref name="reaplicarPageSettings"/> roda após o OK do PrintDialog,
        /// que reescreve DefaultPageSettings — o callback DEVE re-forçar PaperSize A4,
        /// Landscape e Margins do relatório em questão. Em seguida o preview é
        /// invalidado e regerado com a impressora escolhida.
        /// </summary>
        public static void ExibirPreviewComImpressao(Form owner, PrintDocument doc,
            string contextoLog, Action reaplicarPageSettings)
        {
            using var preview = new PrintPreviewDialog
            {
                Document = doc,
                WindowState = FormWindowState.Maximized
            };

            if (preview.Controls.OfType<ToolStrip>().FirstOrDefault() is { } barraPreview
                && barraPreview.Items.Count > 0)
            {
                var btnNativo = barraPreview.Items[0];
                btnNativo.Visible = false;

                var btnImprimirPreview = new ToolStripButton("Imprimir...")
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    Image = btnNativo.Image
                };
                btnImprimirPreview.Click += (_, _) =>
                {
                    using var dlgImpressao = new PrintDialog
                    {
                        Document = doc,
                        UseEXDialog = true
                    };
                    if (dlgImpressao.ShowDialog(preview) != DialogResult.OK) return;

                    reaplicarPageSettings();
                    try
                    {
                        if (!ImpressoraSuportaA4(doc))
                        {
                            var continuar = MessageBox.Show(preview,
                                $"A impressora '{doc.PrinterSettings.PrinterName}' não declara suporte a papel A4.\n" +
                                "O documento pode ser impresso de forma incorreta ou rejeitado pelo driver.\n\n" +
                                "Imprimir mesmo assim?",
                                "Aviso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (continuar != DialogResult.Yes) return;
                        }

                        ImprimirDocumento(preview, doc, $"{contextoLog} (confirmado)");
                    }
                    finally
                    {
                        // A impressora/orientação podem ter mudado: a área imprimível (e com
                        // ela largura de colunas e paginação) é outra — o preview ficaria
                        // mostrando a impressora antiga. InvalidatePreview agenda um job NOVO
                        // de PreviewPrintController (OnBeginPrint → GeometriaPagina.Medir de
                        // novo, contrato por job preservado) via BeginInvoke.
                        // Fica no finally, DEPOIS do job real, de propósito: se fosse antes,
                        // qualquer bombeamento de mensagens durante o job real (diálogo de
                        // driver no StartDoc) poderia rodar o job de preview no MESMO
                        // PrintDocument no meio do real — OnBeginPrint zeraria o estado de
                        // paginação e corromperia a impressão.
                        preview.PrintPreviewControl.InvalidatePreview();
                    }
                };
                barraPreview.Items.Insert(0, btnImprimirPreview);
            }

            preview.ShowDialog(owner);
        }

        /// <summary>
        /// Executa a impressão REAL de <paramref name="doc"/> (já configurado pelo
        /// PrintDialog) com <paramref name="owner"/> como dono de toda UI envolvida.
        ///
        /// Por que não chamar <c>doc.Print()</c> direto (bug "Print to PDF perde o foco"):
        ///   1. O controller padrão (PrintControllerWithStatusDialog) abre a janelinha
        ///      "Imprimindo página X" numa THREAD SEPARADA, sem owner. Ela rouba a ativação
        ///      do preview; ao sumir, o Windows ativa a próxima janela do Z-order — que pode
        ///      ser o navegador do usuário.
        ///   2. Impressoras com porta "PORTPROMPT:" (Microsoft Print to PDF, XPS Writer)
        ///      abrem o "Salvar como" DENTRO do StartDoc, escolhendo o owner pela janela em
        ///      primeiro plano naquele instante — que, por (1), já não é a nossa. O diálogo
        ///      fica pendurado em outro app; fechar esse app deixa o StartDoc (e o preview,
        ///      cujo laço modal está preso dentro do Click) num estado em que não fecha.
        ///
        /// Correção: para portas PORTPROMPT perguntamos NÓS o arquivo (SaveFileDialog com
        /// owner correto) e passamos via PrintToFile/PrintFileName — o driver grava direto,
        /// sem prompt. E o job roda com StandardPrintController (sem thread de status).
        /// PrintToFile continua sendo PrintAction.PrintToFile = impressão real (regra de
        /// negócio da separação inalterada).
        /// </summary>
        /// <returns>true se o job foi enviado; false se cancelado ou com erro.</returns>
        public static bool ImprimirDocumento(Form owner, PrintDocument doc, string contextoLog)
        {
            var ps = doc.PrinterSettings;
            var controllerOriginal = doc.PrintController;
            var cursorOriginal = owner.Cursor;
            // DEVNAMES (driver/impressora/porta) de antes do redirecionamento. É o único
            // jeito público de devolver PrintFileName ao valor original VAZIO: o setter
            // de PrintFileName lança ArgumentNullException para "" — restaurar por ele
            // estourava no finally de TODA impressão em PORTPROMPT (o PDF era gerado,
            // mas o usuário via erro e o controller/ativação não eram restaurados).
            IntPtr devnamesOriginal = IntPtr.Zero;
            try
            {
                if (!ps.PrintToFile && PortaPedeArquivo(ps.PrinterName))
                {
                    bool xps = ps.PrinterName.Contains("XPS", StringComparison.OrdinalIgnoreCase);
                    using var salvar = new SaveFileDialog
                    {
                        Title = $"Salvar impressão — {ps.PrinterName}",
                        Filter = xps
                            ? "Documento XPS (*.oxps;*.xps)|*.oxps;*.xps|Todos os arquivos (*.*)|*.*"
                            : "Documento PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*",
                        DefaultExt = xps ? "oxps" : "pdf",
                        AddExtension = true,
                        OverwritePrompt = true,
                        FileName = NomeArquivoSeguro(doc.DocumentName, contextoLog)
                    };
                    if (salvar.ShowDialog(owner) != DialogResult.OK)
                    {
                        Logger.Info($"{contextoLog}: salvamento em arquivo cancelado pelo usuário");
                        return false;
                    }
                    devnamesOriginal = ps.GetHdevnames();
                    ps.PrintToFile = true;
                    ps.PrintFileName = salvar.FileName;
                    Logger.Info($"{contextoLog}: porta PORTPROMPT — saída direta em '{salvar.FileName}'");
                }

                doc.PrintController = new StandardPrintController();
                LogarEstado(doc, contextoLog);
                owner.Cursor = Cursors.WaitCursor;
                doc.Print();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Erro ao imprimir ({contextoLog})", ex);
                MessageBox.Show(owner, $"Erro ao imprimir: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                owner.Cursor = cursorOriginal;
                // Restaura (só se redirecionamos): senão um próximo "Imprimir em arquivo"
                // reaproveitaria o caminho do PDF. Nunca pode lançar daqui.
                if (devnamesOriginal != IntPtr.Zero)
                {
                    try
                    {
                        ps.SetHdevnames(devnamesOriginal);
                        ps.PrintToFile = false;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"{contextoLog}: falha ao restaurar saída da impressora: {ex.Message}");
                    }
                    finally
                    {
                        NativeMethods.GlobalFree(devnamesOriginal);
                    }
                }
                doc.PrintController = controllerOriginal;
                // Garantia extra: devolve o primeiro plano ao nosso diálogo.
                if (!owner.IsDisposed) owner.Activate();
            }
        }

        private static string NomeArquivoSeguro(string? documentName, string contextoLog)
        {
            var nome = string.IsNullOrWhiteSpace(documentName) || documentName == "document"
                ? contextoLog.Replace(" (confirmado)", "")
                : documentName;
            foreach (var c in Path.GetInvalidFileNameChars())
                nome = nome.Replace(c, '_');
            return nome.Trim();
        }

        /// <summary>
        /// True se a impressora usa a porta "PORTPROMPT:" (o driver pede o nome do
        /// arquivo no StartDoc). Consulta via GetPrinter nível 2; se falhar, cai na
        /// heurística pelos nomes das impressoras virtuais nativas do Windows.
        /// </summary>
        internal static bool PortaPedeArquivo(string nomeImpressora)
        {
            try
            {
                var porta = ObterPortaImpressora(nomeImpressora);
                if (porta != null)
                    return porta.Equals("PORTPROMPT:", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao consultar porta da impressora '{nomeImpressora}': {ex.Message}");
            }
            return nomeImpressora.Equals("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase)
                || nomeImpressora.Equals("Microsoft XPS Document Writer", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ObterPortaImpressora(string nomeImpressora)
        {
            if (!NativeMethods.OpenPrinter(nomeImpressora, out var hPrinter, IntPtr.Zero))
                return null;
            try
            {
                NativeMethods.GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out int necessario);
                if (necessario <= 0) return null;
                var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(necessario);
                try
                {
                    if (!NativeMethods.GetPrinter(hPrinter, 2, buffer, necessario, out _))
                        return null;
                    // PRINTER_INFO_2: pServerName, pPrinterName, pShareName, pPortName, ...
                    var pPortName = System.Runtime.InteropServices.Marshal.ReadIntPtr(buffer, 3 * IntPtr.Size);
                    return System.Runtime.InteropServices.Marshal.PtrToStringUni(pPortName);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                NativeMethods.ClosePrinter(hPrinter);
            }
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
                EntryPoint = "OpenPrinterW", SetLastError = true)]
            public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

            [System.Runtime.InteropServices.DllImport("winspool.drv", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
                EntryPoint = "GetPrinterW", SetLastError = true)]
            public static extern bool GetPrinter(IntPtr hPrinter, int level, IntPtr pPrinter, int cbBuf, out int pcbNeeded);

            [System.Runtime.InteropServices.DllImport("winspool.drv", SetLastError = true)]
            public static extern bool ClosePrinter(IntPtr hPrinter);

            // PrinterSettings.GetHdevnames aloca com GlobalAlloc: liberar com GlobalFree.
            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            public static extern IntPtr GlobalFree(IntPtr hMem);
        }

        /// <summary>
        /// Loga estado de impressão para diagnóstico (printer name, paper size,
        /// margens, orientação). Chamar antes de Print() ou Preview.
        /// </summary>
        public static void LogarEstado(PrintDocument doc, string contexto)
        {
            try
            {
                var pg = doc.DefaultPageSettings;
                var ps = pg.PaperSize;
                var m = pg.Margins;
                // ATENÇÃO: PaperSize aqui é o objeto que NÓS atribuímos — log parcialmente
                // circular. A prova do papel realmente negociado com o driver é
                // e.PageBounds dentro do OnPrintPage (ver LogarPagina).
                var geo = GeometriaPagina.Medir(pg);
                Logger.Info(
                    $"{contexto} | Printer='{doc.PrinterSettings.PrinterName}' | " +
                    $"PaperKind={ps.Kind} RawKind={ps.RawKind} ({ps.Width}x{ps.Height}) | " +
                    $"Landscape={pg.Landscape} | " +
                    $"Margins=L{m.Left},R{m.Right},T{m.Top},B{m.Bottom} | " +
                    $"Controller={doc.PrintController?.GetType().Name} " +
                    $"(IsPreview={doc.PrintController?.IsPreview}) | {geo}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao logar estado de impressão: {ex.Message}");
            }
        }

        /// <summary>
        /// Diagnóstico DENTRO da página (chamar só na página 1, nos dois modos).
        /// Diferente de LogarEstado, aqui e.PageBounds vem do papel realmente negociado
        /// com o driver, e VisibleClipBounds é a prova empírica da origem: no preview
        /// vale a folha inteira, na impressão real vale a área imprimível.
        /// DENTRO_DA_AREA_IMPRIMIVEL=False é a assinatura exata do corte.
        /// </summary>
        public static void LogarPagina(PrintPageEventArgs e, string contexto,
                                       RectangleF area, GeometriaPagina geo, bool preview)
        {
            try
            {
                var g = e.Graphics!;
                var vcb = g.VisibleClipBounds;
                using var t = g.Transform;
                // No modo real a referência é a superfície do device (área imprimível);
                // no preview o metafile reporta o clip da impressora mas grava além dele,
                // então a referência correta é a folha inteira.
                var limite = preview ? (RectangleF)e.PageBounds : vcb;
                bool dentro = area.Left >= limite.Left - 0.5f && area.Top >= limite.Top - 0.5f
                           && area.Right <= limite.Right + 0.5f && area.Bottom <= limite.Bottom + 0.5f;
                Logger.Info(
                    $"{contexto} | modo={(preview ? "PREVIEW" : "REAL")} | " +
                    $"Printer='{e.PageSettings.PrinterSettings.PrinterName}' | " +
                    $"PageBounds={e.PageBounds.Width}x{e.PageBounds.Height} | " +
                    $"MarginBounds={e.MarginBounds} | area={area} | " +
                    $"{geo} | VisibleClip={vcb} | transform=({t.OffsetX};{t.OffsetY}) | " +
                    $"dpi={g.DpiX}x{g.DpiY} | DENTRO_DA_AREA_IMPRIMIVEL={dentro}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Falha ao logar página: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Mede LARGURAS de coluna num Graphics de REFERÊNCIA fixo (bitmap 600 dpi em
    /// 1/100", a unidade dos jobs), nunca no Graphics do job: preview (metafile) e
    /// impressora medem com diferença de fração de unidade, e com auto-ajuste a coluna
    /// que fica com a sobra (DESCRIÇÃO) oscilaria — um item quebraria só no papel e
    /// preview e impressão paginariam diferente. Com a referência, mesma área + mesmos
    /// dados = mesmas larguras nos dois modos. Só as ALTURAS seguem medidas no job.
    /// Mesmo método da Cópia de Pedido (ver docs/IMPRESSAO.md).
    /// </summary>
    internal sealed class MedidorLargura : IDisposable
    {
        /// <summary>Folga sobre a medida: cobre a diferença entre a referência e o DC
        /// real de cada impressora/preview (bem abaixo de 5% nas medições).</summary>
        internal const float Folga = 1.05f;

        private readonly Bitmap _bmp;
        private readonly Graphics _g;

        internal MedidorLargura()
        {
            _bmp = new Bitmap(1, 1);
            _bmp.SetResolution(600f, 600f);
            _g = Graphics.FromImage(_bmp);
            // 1/100" explícito: num bitmap, GraphicsUnit.Display NÃO é 1/100" (só em
            // impressora) — mediria tudo ~45% maior.
            _g.PageUnit = GraphicsUnit.Inch;
            _g.PageScale = 0.01f;
        }

        /// <summary>Largura de coluna em que o texto cabe numa linha: medida × folga,
        /// + 2×padding, + 1 (absorve o truncamento int da largura útil em
        /// MedirAlturaLinha).</summary>
        internal float SemQuebra(string? texto, Font fonte, float padding)
        {
            if (string.IsNullOrEmpty(texto)) return padding * 2f;
            return (float)Math.Ceiling(_g.MeasureString(texto, fonte).Width * Folga) + padding * 2f + 1f;
        }

        public void Dispose()
        {
            _g.Dispose();
            _bmp.Dispose();
        }
    }

    /// <summary>
    /// Primitivas de tabela compartilhadas pelos relatórios impressos
    /// (Separação de Mercadorias e Cópia de Pedido): medição de altura de linha
    /// com quebra por coluna e desenho de uma linha de células.
    /// Extraídas de RelatorioSeparacaoPrintDoc para os dois relatórios quebrarem
    /// texto de forma idêntica.
    /// </summary>
    internal static class RelatorioTabela
    {
        /// <summary>Altura necessária para exibir todos os valores por completo (com wrap).</summary>
        internal static float MedirAlturaLinha(Graphics g, float[] colW, string[] valores,
            Font font, float padding, float alturaMinima)
        {
            float altura = alturaMinima;
            for (int i = 0; i < valores.Length && i < colW.Length; i++)
            {
                if (string.IsNullOrEmpty(valores[i])) continue;
                float larguraUtil = colW[i] - padding * 2;
                float h = g.MeasureString(valores[i], font, (int)larguraUtil).Height + 5f;
                if (h > altura) altura = h;
            }
            return altura;
        }

        internal static void DrawRow(Graphics g, float[] colW, string[] valores, Font font,
            float x, float y, float altura, float padding, StringAlignment[] aligns, bool quebrarLinha)
        {
            float cx = x;
            using var brush = new SolidBrush(Color.Black);
            using var sf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming = quebrarLinha ? StringTrimming.None : StringTrimming.EllipsisCharacter,
                FormatFlags = quebrarLinha ? 0 : StringFormatFlags.NoWrap
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
