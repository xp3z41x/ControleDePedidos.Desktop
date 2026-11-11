# Subsistema de Impressão

Documentação técnica dos relatórios impressos do Controle de Pedidos. Este é o subsistema mais delicado do app: geometria de página, paginação e regras de negócio acopladas à impressão física. Leia antes de mexer em qualquer `*PrintDoc.cs` ou em `PrintingHelper.cs`.

## Visão geral

| Documento | Arquivo | Orientação | Margens | Efeito colateral |
|---|---|---|---|---|
| Relatório de Carga | `RelatorioCargaPrintDoc.cs` | retrato | 40 | nenhum |
| Relatório de NF-e | `RelatorioNfePrintDoc.cs` | retrato | 40 | nenhum |
| Separação de Mercadorias | `RelatorioSeparacaoPrintDoc.cs` | configurável | **25** | impressão FÍSICA avança APROVADO → EM SEPARAÇÃO (só pelo botão do grid) |
| Cópia de Pedido | `RelatorioCopiaPedidoPrintDoc.cs` | configurável | 40 | **nenhum, por decisão de projeto** |

Todos herdam de `System.Drawing.Printing.PrintDocument`, desenham via GDI+ em unidades **Display (1/100 de polegada)** e seguem o mesmo contrato de geometria (abaixo).

## O contrato de geometria (`PrintingHelper.GeometriaPagina`)

**Problema que resolve:** com `OriginAtMargins = false`, o preview (`PreviewPrintController`, metafile com origem no canto do PAPEL) e a impressão real (`StandardPrintController`, DC com origem no canto da ÁREA IMPRIMÍVEL) têm origens diferentes. Como `e.MarginBounds` é sempre medido a partir do canto do papel, na impressão real tudo deslocava ~4 mm para direita/baixo — cortando a última coluna em impressoras com margem física (o bug não aparecia em "Print to PDF", que tem margem física zero).

**Regras que todo print doc segue (não simplificar):**

1. `OriginAtMargins = false` sempre — trocar para `true` seria inócuo (o `ResetTransform` do helper anula a compensação do controller) e deslocaria o preview.
2. Em `OnBeginPrint`: capturar `_preview = e.PrintAction == PrintAction.PrintToPreview` e medir a geometria **uma vez por job** (`_geo = PrintingHelper.GeometriaPagina.Medir(DefaultPageSettings)`) — cada medição cria/destrói um DC de impressora. `OnBeginPrint` roda depois do PrintDialog, então reflete a impressora escolhida.
3. Em `OnPrintPage`, primeira coisa: `var area = _geo.PrepararPagina(e, _preview);` — faz `ResetTransform` → `PageUnit = Display` (antes do translate!) → AntiAlias → translate `(-Imprimivel.X, -Imprimivel.Y)` **só na impressão real** → devolve `Intersect(e.MarginBounds, Imprimivel)`.
4. O **clamp nunca depende do modo** (define largura de colunas e itens por página; movê-lo para dentro do "if real" faria preview e papel paginarem diferente). O **translate depende** (é ele que iguala papel e preview).
5. `Medir` usa `PrintableArea` (não `HardMarginX/Y`, que sofre divisão inteira e não informa margem inferior/direita), testa a variante trocada X↔Y (drivers fazem troca dupla em paisagem) e cai para folha inteira em qualquer incoerência — degradação segura.
6. Laço de itens sempre com guard `itensNestaPagina > 0` — garante ao menos 1 item por página; sem isso, área degenerada = job infinito cuspindo folhas em branco.
7. `PrintAction.PrintToFile` (inclui "Microsoft Print to PDF") conta como impressão **real** para regra de negócio e geometria.

**Diagnóstico:** `PrintingHelper.LogarEstado(doc, ctx)` antes do preview/print e `PrintingHelper.LogarPagina(e, ctx, area, geo, preview)` na página 1 de cada modo. A linha de log traz `DENTRO_DA_AREA_IMPRIMIVEL=True/False` — `False` é a assinatura exata do corte. `VisibleClipBounds` é a prova empírica da origem; `transform=(-16,7;-16,7)` na Brother é o translate aplicado.

## Preview compartilhado

`PrintingHelper.ExibirPreviewComImpressao(Form owner, PrintDocument doc, string contextoLog, Action reaplicarPageSettings)`:

- Preview maximizado com o **botão nativo de impressão substituído** por "Imprimir..." que abre `PrintDialog` (o nativo imprimia direto na impressora pré-selecionada, sem feedback — se a fila estivesse travada, "nada acontecia").
- Após o OK do PrintDialog, `reaplicarPageSettings()` **deve** re-forçar `PaperSize` A4 + `Landscape` + `Margins` — o PrintDialog reescreve `DefaultPageSettings`.
- Após o reaplicar, o preview é **regerado** (`PrintPreviewControl.InvalidatePreview()`) para mostrar a geometria da impressora escolhida — é um job novo de preview, com `GeometriaPagina.Medir` de novo no `OnBeginPrint`. A chamada fica no `finally`, **depois** do job real, de propósito: invalidar antes deixaria um job de preview pendente (via `BeginInvoke`) que qualquer bombeamento de mensagens durante a impressão real (diálogo de driver no `StartDoc`) rodaria no MESMO `PrintDocument` no meio do job real, zerando o estado de paginação.
- Aviso quando a impressora não declara suporte a A4 (`ImpressoraSuportaA4`).
- **O `owner` importa**: o preview faz `ShowDialog(owner)`. Chamado de dentro de um diálogo modal (ex.: Imprimir por Orçamento), o owner deve ser o diálogo, nunca o Form1 — senão o preview nasce mal-parentado.

**Toda impressão real passa por `PrintingHelper.ImprimirDocumento(owner, doc, ctx)`** — nunca `doc.Print()` direto. Motivo (bug "Print to PDF perde o foco"): o controller padrão `PrintControllerWithStatusDialog` mostra o "Imprimindo página X" numa thread separada sem owner, que rouba a ativação; impressoras com porta `PORTPROMPT:` (Microsoft Print to PDF / XPS Writer) abrem o "Salvar como" dentro do `StartDoc` usando a janela em primeiro plano como owner — que acabava sendo outro app (ex.: navegador). Fechar esse app travava o preview. O helper: (1) para porta `PORTPROMPT:` (via `GetPrinter` nível 2) abre o **nosso** `SaveFileDialog` com o owner certo e passa o caminho por `PrintToFile`/`PrintFileName` (driver grava sem prompt; `PrintAction` segue `PrintToFile` = impressão real); (2) usa `StandardPrintController` (sem thread de status); (3) restaura `PrintToFile`/`PrintFileName`/controller e reativa o owner no `finally`.

### Seleção de impressora (`AplicarConfigA4` / `EncontrarImpressoraA4Capaz`)

`PrintingHelper.AplicarConfigA4(doc, landscape)` seleciona a impressora e re-aplica A4 + margens 40. A Separação sobrescreve para 25 depois (densidade de itens é objetivo lá; na Cópia, visual de documento de cliente pede as 40).

Ordem de escolha (núcleo puro `EscolherImpressoraA4`, testável sem impressoras reais):

1. **A impressora padrão do Windows do usuário**, se declara A4 e não parece térmica. No RDS todas as 16 impressoras de rede aparecem para todo operador; a padrão é a da mesa dele.
2. Senão, a primeira instalada que declara A4 e não parece térmica (comportamento antigo — era a COL-A4 de outra loja para todo mundo).
3. Senão, a primeira A4 com cara de térmica (último recurso); senão `null` (fica a padrão do Windows, com `Warn`).

"Parece térmica" = heurística de nome `PalavrasTermicas` (inclui a convenção do RDS: `FISCAL`, `ETQ`, `ETIQUETADORA`; drivers genéricos de bobina às vezes declaram A4). A escolha e o motivo vão para o log: `Impressora A4: 'X' — <motivo>`.

**Cache por processo:** suporte a A4 por nome de impressora (`ConcurrentDictionary`) + a escolha final chaveada pela padrão (sob lock). Primeira consulta ~80 ms, seguintes ~0 (antes: PaperSizes das 16 impressoras de rede a cada relatório, ~0,9 s). Invalidação: padrão do Windows mudou, escolhida sumiu da lista instalada (sessão RDS reconectada), ou `InvalidarCacheImpressoras()`. Consultas que **falham** nunca entram no cache e impedem cachear a escolha daquela rodada (a próxima tenta de novo).

## Primitivas compartilhadas (`PrintingHelper.RelatorioTabela`)

- `MedirAlturaLinha(g, colW, valores, font, padding, alturaMinima)` — altura da linha com quebra por coluna (usa `MeasureString` por célula, fica com o máximo).
- `DrawRow(g, colW, valores, font, x, y, altura, padding, aligns, quebrarLinha)` — desenha uma linha de células; `quebrarLinha: false` = NoWrap + reticências (headers), `true` = quebra completa (dados; célula nunca é cortada, a linha engorda).

Separação e Cópia usam as duas — é o que garante quebra de texto idêntica nos dois relatórios.

## Code 128 (`Code128.cs`)

Código de barras 128-C desenhado vetorialmente via `FillRectangle` (zero dependência raster). `CodificarC` (tabela de 107 padrões + checksum mod-103), `Desenhar(g, digitos, x, y, larguraModulo, altura)`, `MedirLargura`. Parâmetros usados nos relatórios: módulo 1,5, altura 45, quiet zone 15 (unidades 1/100").

## Relatório de Separação de Mercadorias

- **Disparo:** botão "🖨 Imprimir" do grid de controle (`Form1.ImprimirSeparacaoAsync`) ou mini-janela Imprimir por Orçamento.
- **Dados:** cabeçalho de `estorc` + `cadfun` + `cadcli`; itens de `estior` + `estsal` (local, referência, marca, saldos) + `cadpro` (unidade) + `cadsys.atividade` (decide colunas V/A das fórmulas de estoque). Fórmulas de SALDO ATUAL e ESTOQUE FÍSICO documentadas no SQL em `Form1.cs`.
- **Colunas:** checkbox p/ marcação a caneta, ITEM, MATRÍCULA, DESCRIÇÃO, REFERÊNCIA, MARCA, LOCAL, QTD, UN, FÍSICO, SALDO.
- **Config (`[Separacao]`):** paisagem, ocultar cancelados, ordenar por LOCAL (sub-ordem descrição), 2 casas decimais.
- **Cabeçalho:** título + nº do pedido + barcode; página ≥2 tem cabeçalho resumido. Assinaturas (SEPARADOR / CONFERENTE / BALCÃO) entre o cabeçalho e a tabela, só na página 1.
- **Regra de negócio:** evento `ImpressaoRealConcluida` dispara em `OnEndPrint` quando `_impressaoReal && !e.Cancel`. O assinante (só no fluxo do botão do grid) avança APROVADO → EM SEPARAÇÃO com guards (não travado, não em faturamento, idempotente por sessão de preview) via UPDATE com locking otimista. `_impressaoReal` (regra de negócio) e `_preview` (renderização) são campos separados **de propósito** — layout não pode depender de regra de negócio.

## Cópia de Pedido

- **Disparo:** botão "📄 Cópia de Pedido" do grid ou mini-janela. **Nunca altera status** — não existe wiring de `ImpressaoRealConcluida`; `OnEndPrint` é só sentinela de deriva da paginação.
- **Cabeçalho (página 1):** tarja legal preta ("NÃO É DOCUMENTO FISCAL..."), logomarca + dados da empresa (config `[Empresa]`, campos vazios omitidos), bloco identificação (CÓPIA DE PEDIDO / PEDIDO Nº / Data / barcode), caixa do cliente em 2 colunas pareadas (nome+fantasia, CPF/RG ou CNPJ/IE conforme `natureza`, telefones, endereço+CEP, cidade/UF, e-mail), VENDEDOR + Nº NOTA FISCAL, OBSERVAÇÃO.
- **Grid — duas formas, decididas no construtor (`_temDesconto`):**
  - **Sem desconto** (nenhum item com `VrOriginal > VrUnitario`): MAT., DESCRIÇÃO, MARCA, REFERÊNCIA, QTD, UNITÁRIO, TOTAL.
  - **Com desconto** (algum item com `VrOriginal > VrUnitario`, comparando em centavos): coluna extra depois do unitário → UNITÁRIO (original, `VrOriginal` = `estior.vr_prazo`), C/ DESC. (`VrUnitario`), TOTAL (`VrTotal`, já com desconto). Não existe coluna de total sem desconto.
  - **% de desconto** só na coluna C/ DESC., no formato `R$ 109,52 (2,99%)`: **calculada** dos valores impressos, `(1 − VrUnitario/VrOriginal) × 100`, duas casas (`Math.Round` AwayFromZero + `"N2"`), pt-BR — nunca `estior.perc_desconto`. Item sem desconto (num pedido com desconto) não leva % (nada de "(0,00%)"); desconto ínfimo que arredonda a zero sai `(<0,01%)`. Vale para o item e para o COM DESCONTO do total. **Configurável** (`CopiaExibirPercentualDesconto`, padrão ligado; caixa "Exibir porcentagem de desconto e aviso de arredondamento" em Configurações → CÓPIA DE PEDIDO): desligada, o grid COM desconto continua igual (colunas UNITÁRIO/C/ DESC., campos SEM/COM DESCONTO), mas só com os valores em reais — sem "(x,xx%)" em lugar nenhum e **sem o aviso de arredondamento** (`ExibeAviso` = `_temDesconto && _exibirPercentual`, usado também no lookahead).
  - `VrOriginal ≤ 0` (dado ausente) vale como "sem desconto" (`OriginalEfetivo` usa o `VrUnitario`) — nunca imprime original R$ 0,00 nem desconto de 100%.
  - Valores em dinheiro em `"C2"` pt-BR (com "R$") via `Dinheiro()`, que troca o espaço por **NBSP** para a quebra nunca separar o "R$" do número; QTD em `"N2"` (sem R$). Entre o valor e a "(%)" o espaço é comum: se a coluna apertar, a % desce para a linha de baixo (medida por `MedirAlturaLinha`), nunca é truncada.
  - Rótulos **curtos** (MAT., UNITÁRIO, C/ DESC., TOTAL) para o header caber numa linha (altura 22) e não ditar a largura das colunas estreitas. Continuam desenhados com quebra (`quebrarLinha: true`) e com altura medida (`_alturaHeader` = `MedirAlturaLinha` dos rótulos, mínimo 22) como rede de segurança.
- **Larguras — auto-ajuste por job (`CalcularColunas`):** calculadas **uma vez por job**, em `SimularPaginacao`, **antes** de medir qualquer altura; o array `_colW` vale para a simulação e para o desenho (`LayoutPagina` não recalcula) e é zerado em `OnBeginPrint`.
  - **Larguras medidas num `Graphics` de REFERÊNCIA** (`BitmapReferencia`: bitmap 600 dpi, `PageUnit = Inch` + `PageScale = 0,01` → 1/100"; `GraphicsUnit.Display` num bitmap **não** é 1/100" e mede ~45% a mais), **não no do job**. Medindo no job, preview (metafile) e impressora divergiam em fração de unidade, o arredondamento de uma coluna de valor cruzava um inteiro (UNITÁRIO 65 × 66), a DESCRIÇÃO — que fica com a sobra — mudava 1 unidade e um item "no limite" quebrava só no papel: varredura de 26 itens em retrato dava preview 1 página, impressão 2. Com a referência, mesma área + mesmos dados = mesmas larguras nos dois modos (como nas proporções fixas); só as **alturas** seguem medidas no `Graphics` do job. Os números saem iguais aos medidos no DC da Brother.
  - Regra (W = largura útil):
    1. **Colunas estreitas** (MAT., QTD e as de valor): `max(rótulo em FontHeader, maior valor do pedido em FontDados)` medido sem quebra (`MeasureString` com o formato padrão, o mesmo que `MedirAlturaLinha` usa para decidir a quebra) × **`FolgaMedida` 1,05** + 2×padding + 1 (absorve o `(int)` da largura útil). A folga cobre a diferença entre a referência e o DC real (preview ou impressora), para nenhuma coluna estreita quebrar nem o valor `NoWrap` da linha TOTAL ganhar reticências.
    2. **MARCA e REFERÊNCIA:** o mesmo, com **teto** (`TetoMarca` 11% e `TetoReferencia` 13% de W, ~82 e ~97 em retrato) e nunca menores que o próprio rótulo. Acima do teto quebram (altura medida, nunca truncadas): em retrato "MAGNET MARELLI"/"FOCO DO BRASIL" vão para 2 linhas e referências longas sem espaço são partidas pelo GDI+.
    3. **Linha TOTAL antes da DESCRIÇÃO:** os campos precisam comportar os textos medidos na `FontTotal` — sem desconto, TOTAL ≥ soma dos itens e VALOR NEGOCIADO (o valor é desenhado `NoWrap`); com desconto, QTD+UNITÁRIO ≥ "SEM DESCONTO"/Σ original e C/ DESC.+TOTAL ≥ "COM DESCONTO"/total com "(x,xx%)" quando `_exibirPercentual`/VALOR NEGOCIADO. Déficit é repartido na proporção das larguras da faixa (`GarantirFaixa`).
    4. **DESCRIÇÃO = resto**, com piso `PisoDescricao` 25% de W (e o bastante para "TOTAL (N itens)"/"VALOR NEGOCIADO" caberem nas colunas à esquerda do campo). Piso não fecha → MARCA/REFERÊNCIA recuam proporcionalmente até os rótulos; ainda assim não fecha (área degenerada) → todas as outras encolhem na mesma proporção, com `Warn` no log.
  - Com a % desligada a C/ DESC. encolhe sozinha; em paisagem toda a sobra vai para a DESCRIÇÃO.
  - Pior caso de produção (matrícula 5 dígitos, marca 14, referência 30, qtd 600,00, total do item R$ 11.264,88, total R$ 1.234.567,89, negociado −R$ 1.234.567,89), retrato: DESCRIÇÃO 216 de 747 (29%) com desconto e %, 262 (35%) com % desligada, 305 (41%) sem desconto — antes 168 (22,5%) e 239 (32%) fixos. Preview e impressão real (`PrintToFile`) com as mesmas páginas na Brother, na HP (SDN-A4) e na Epson (ESCRITORIO), e nenhuma célula estreita quebrando no DC real. ⛔ **Essa medição foi feita com `PrintToFile` em impressoras físicas e imprimiu folhas de verdade — método PROIBIDO; não repita** (ver "Como verificar" abaixo e o `CLAUDE.md`).
  - Amostra realista de 40 itens (descrição mediana ~43 caracteres), retrato: com desconto 100 → 79 linhas de grid e 3 → 2 páginas; sem desconto 74 → 61 linhas; paisagem com desconto 69 → 43 linhas.
  - Mudou fonte, padding ou rótulo? Nada a re-medir à mão — mas rode o harness (preview × real) para confirmar contagens iguais.
- **Fechamento:**
  - Sem desconto: linha TOTAL como sempre — "TOTAL (N itens)" + soma dos `vr_total` na última coluna (TOTAL, alargada pelo auto-ajuste se a soma ou o negociado não couberem na fonte de 8,5pt).
  - Com desconto: **uma** linha com três campos — "TOTAL (N itens)" (MAT..REFERÊNCIA), **SEM DESCONTO** = Σ round2(Qtd × original) (sob QTD+UNITÁRIO) e **COM DESCONTO** = Σ `VrTotal` + % calculada do total (sob C/ DESC.+TOTAL). Cada campo tem rótulo pequeno em cima e valor embaixo; a altura da linha (`_alturaLinhaTotal`) é medida pela métrica da fonte em `MedirLinhaTotal` (rótulo + valor com quebra até 2 linhas).
  - Linha **VALOR NEGOCIADO** logo abaixo quando `estorc.vr_negociado ≠ 0`, com o valor no mesmo campo do total pago (última coluna sem desconto; C/ DESC.+TOTAL com desconto).
  - O lookahead da paginação reserva o fechamento inteiro junto do último item (nada de linha órfã): `AlturaFechamento` = `AlturaBlocoTotais` (`_alturaLinhaTotal` + eventual 22 do negociado) + aviso de arredondamento (gap 3 + `_alturaAviso`, só com desconto).
  - `_alturaHeader` e `_alturaLinhaTotal` voltam ao padrão (22) em `OnBeginPrint`, e `_alturaAviso` volta a 0; os três são medidos em `SimularPaginacao` **antes** de qualquer `LayoutPagina` — simulação e desenho usam os mesmos valores.
- **Aviso de arredondamento (só pedido COM desconto e com a % ligada):** a frase fixa do dono `AvisoArredondamento` ("A porcentagem de desconto pode ficar um pouco diferente da combinada em alguns casos, devido ao arredondamento dos centavos." — não editar) sai em duas linhas: (1) logo **acima do cabeçalho da tabela**, só no primeiro trecho do grid (`st.Item == 0`, página 1 — continuações não repetem); (2) logo **abaixo do bloco de totais** (TOTAL e eventual VALOR NEGOCIADO), dentro do lookahead (`AlturaFechamento`) — sempre na página do último item e do TOTAL.
  - Segoe UI **7pt itálico**, cinza escuro (64,64,64), à esquerda na borda do grid, `NoWrap` sem reticências, altura `FontAviso.GetHeight + 1`. Medida no DC da impressora: **575** de largura contra **747** úteis em retrato (~23% de folga; paisagem 1089). `SimularPaginacao` loga Warn se algum dia a frase medir mais que a largura útil.
  - Pedido sem desconto: `_alturaAviso = 0` e nada é desenhado — layout idêntico ao anterior.
  - Prova do lookahead: varredura de 1..105 itens (retrato/paisagem, com/sem negociado) comparando o código com um mutante sem o aviso no lookahead — 8 configurações em que só o aviso decide a quebra (ex.: 21 itens em retrato, 35 com negociado em paisagem); nelas o último item desce junto com TOTAL + aviso para a página seguinte, nos dois modos (preview e real).
- **Rodapé (última página):** FORMA DE PAGAMENTO / NÚMERO DO PEDIDO / VOLUMES / PESO (`estorc.texto01..04`; vazio = "—"). Se não couber, abre página só com cabeçalho resumido + rodapé.
- **"Página X de Y"** em toda página. O total é **pré-calculado por simulação** (abaixo).
- **Data:** `estorc.data_inc + hora_inc` (data/hora da digitação no Integra), pré-formatada em `Form1.FormatarDataPedido` — **não** é o relógio da impressão. `hora_inc` é texto "HH:MM" impresso verbatim.
- **Máscaras:** CPF/CNPJ (com reposição de zeros à esquerda pela `natureza`), CEP, telefones — helpers estáticos no próprio arquivo.
- **Config (`[CopiaPedido]`):** paisagem, ordenação alfabética (padrão: ordem do pedido). Itens cancelados **nunca** aparecem (sem toggle).

### Paginação sem deriva (o truque central)

Um único método `LayoutPagina(g, area, EstadoPaginacao, pagina, bool medir)` **simula e desenha**: todo `g.Draw*/Fill*` fica sob `if (!medir)`; toda aritmética de `y` roda nos dois modos. No primeiro `OnPrintPage`, `SimularPaginacao` mede a altura de cada item uma vez (cache `_alturasLinhas`) e roda `LayoutPagina(medir: true)` até o rodapé sair (trava de 999 páginas) → `_totalPaginas` conhecido **antes de qualquer desenho**. Deriva entre simulação e render é impossível por construção (mesmo código, mesmo `Graphics`); ainda assim `OnEndPrint` loga um Warn se divergirem. Cada passe (preview / impressão real) re-simula do zero via `OnBeginPrint`.

### Tipografia do cabeçalho

- Alturas de linha **sempre** pela métrica da fonte (`Font.GetHeight(g)` via helper `MedirTexto`) — retângulo menor que a linha tipográfica decapita descendentes de "g"/"q"/"ç" (bug corrigido na v11.6.1; não reintroduzir constantes apertadas).
- Campos longos quebram em **até 2 linhas** (`MedirTexto(..., maxLinhas)`), reticências só como último recurso. As colunas pareadas do bloco do cliente andam juntas: altura do par = lado mais alto.

### Logomarca

- Recurso embutido `Resources/logo-distribuidora.png` (`LogicalName=logo-distribuidora.png` no csproj) — **arte recortada** (o arquivo original `LOGO DISTRIBUIDORA.png`, 8000×5000/153 MB com bordas vazias enormes, está no `.gitignore` e NUNCA deve entrar no git; o conteúdo real é 5877×2447, aspecto ~2,38).
- Carregada uma vez via `Lazy<Image?>` + `GetManifestResourceStream`; o `MemoryStream` fica vivo de propósito (`Image.FromStream` referencia o stream — não "simplificar" com `using`). Recurso ausente → relatório sai sem logo, com `Logger.Warn`.
- Slot fixo 238×100 (aspecto da arte), aspect-fit, `HighQualityBicubic`.
- Para regenerar o recurso a partir de nova arte: recortar no bounding box do conteúdo, redimensionar para ~1200 px de largura, salvar PNG (o pipeline usado está descrito no histórico; qualquer script System.Drawing com `HighQualityBicubic` + `ImageAttributes.SetWrapMode(TileFlipXY)` serve).

## Como verificar impressão SEM gastar papel

> ⛔ **NUNCA enviar job a impressora física em teste** — nem com "Imprimir em arquivo"/`PrintToFile`. Em 11/11/2026 harnesses com `PrintToFile` em impressoras físicas (todas remotas neste servidor RDS: `\\TS-*`, `\\SDN-IMP`, IP) imprimiram dezenas de folhas de verdade. Regras completas na seção **⛔ PROIBIDO** do `CLAUDE.md`.

1. **Preview** (`PreviewPrintController`, fora do spooler) = modo PREVIEW real do pipeline e a verificação principal.
2. **Ramo REAL sem papel:** **somente** "Microsoft Print to PDF" com `PrintToFile` + `PrintFileName` no scratchpad, e guard no harness que lança exceção antes do `Print()` se `PrinterName` for outra — exercita `StandardPrintController` + translate. Print to PDF tem hard margin 0, então não valida o deslocamento de corte; para a geometria de uma impressora física use **só consultas sem job** (`DefaultPageSettings.HardMarginX/Y`, `PrintableArea`, `PrinterSettings.CreateMeasurementGraphics()`) e simule a margem na Print to PDF. Teste em papel é do usuário, a pedido dele.
3. Conferir no log as duas linhas `LogarPagina` (PREVIEW e REAL): `area` idêntica e `DENTRO_DA_AREA_IMPRIMIVEL=True` nos dois.
4. Contagem de páginas deve bater entre preview e real (o Warn "simulação previu X, renderizou Y" indica bug).
5. Seleção de impressora: chamar `EscolherImpressoraA4(padrao, instaladas, suportaA4)` com padrões/consultas simuladas (padrão térmica, padrão sem A4, consulta falhando) — **nunca** trocar a impressora padrão do sistema para testar.
6. Existe um harness de referência (console app que compila os fontes de impressão, renderiza cenários sintéticos nos dois modos, compara contagens e exporta PNGs das páginas) — o padrão está descrito em `CLAUDE.md`, seção Verificação.
