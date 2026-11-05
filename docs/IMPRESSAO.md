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
- Aviso quando a impressora não declara suporte a A4 (`ImpressoraSuportaA4`).
- **O `owner` importa**: o preview faz `ShowDialog(owner)`. Chamado de dentro de um diálogo modal (ex.: Imprimir por Orçamento), o owner deve ser o diálogo, nunca o Form1 — senão o preview nasce mal-parentado.

**Toda impressão real passa por `PrintingHelper.ImprimirDocumento(owner, doc, ctx)`** — nunca `doc.Print()` direto. Motivo (bug "Print to PDF perde o foco"): o controller padrão `PrintControllerWithStatusDialog` mostra o "Imprimindo página X" numa thread separada sem owner, que rouba a ativação; impressoras com porta `PORTPROMPT:` (Microsoft Print to PDF / XPS Writer) abrem o "Salvar como" dentro do `StartDoc` usando a janela em primeiro plano como owner — que acabava sendo outro app (ex.: navegador). Fechar esse app travava o preview. O helper: (1) para porta `PORTPROMPT:` (via `GetPrinter` nível 2) abre o **nosso** `SaveFileDialog` com o owner certo e passa o caminho por `PrintToFile`/`PrintFileName` (driver grava sem prompt; `PrintAction` segue `PrintToFile` = impressão real); (2) usa `StandardPrintController` (sem thread de status); (3) restaura `PrintToFile`/`PrintFileName`/controller e reativa o owner no `finally`.

`PrintingHelper.AplicarConfigA4(doc, landscape)` seleciona a primeira impressora A4-capaz (evitando térmicas/etiquetadoras pela heurística `PalavrasTermicas`), re-aplica A4 + margens 40. A Separação sobrescreve para 25 depois (densidade de itens é objetivo lá; na Cópia, visual de documento de cliente pede as 40).

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
- **Grid:** MATRÍCULA, DESCRIÇÃO, MARCA, REFERÊNCIA, QTD, VR. UNITÁRIO, VR. TOTAL — valores `"N2"` pt-BR explícito.
- **Fechamento:** linha TOTAL (contagem + soma dos `vr_total` impressos em `"C2"`); linha **VALOR NEGOCIADO** logo abaixo quando `estorc.vr_negociado ≠ 0`. O lookahead da paginação reserva o bloco inteiro junto do último item (nada de linha órfã).
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

1. **Preview** = modo PREVIEW real do pipeline.
2. **Ramo REAL sem papel:** no PrintDialog, marcar *Imprimir em arquivo* (ou usar "Microsoft Print to PDF") — exercita `StandardPrintController` + translate. **Não** usar Print to PDF para validar geometria de corte (hard margin 0 = correção vira no-op); para isso, imprimir em arquivo `.prn` numa impressora física instalada.
3. Conferir no log as duas linhas `LogarPagina` (PREVIEW e REAL): `area` idêntica e `DENTRO_DA_AREA_IMPRIMIVEL=True` nos dois.
4. Contagem de páginas deve bater entre preview e real (o Warn "simulação previu X, renderizou Y" indica bug).
5. Existe um harness de referência (console app que compila os fontes de impressão, renderiza cenários sintéticos nos dois modos, compara contagens e exporta PNGs das páginas) — o padrão está descrito em `CLAUDE.md`, seção Verificação.
