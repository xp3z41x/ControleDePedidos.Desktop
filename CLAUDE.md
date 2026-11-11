# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Aplicação WinForms (.NET 10, C# com nullable, Npgsql puro, sem ORM) que controla o ciclo de vida de pedidos do ERP Integra sem modificá-lo: lê `integrapgsql` em modo somente leitura e mantém estado próprio em `controlepedidos`. Roda em servidor RDS com 10+ operadores simultâneos. Código, comentários e strings de UI em **pt-BR** — mantenha o idioma.

Documentação completa em `docs/`: [ARQUITETURA](docs/ARQUITETURA.md) · [FUNCIONALIDADES](docs/FUNCIONALIDADES.md) · [BANCO-DE-DADOS](docs/BANCO-DE-DADOS.md) · [IMPRESSAO](docs/IMPRESSAO.md).

## Comandos

```powershell
dotnet build "CONTROLE DE PEDIDOS.csproj"                      # compile (0 warnings é o baseline)
dotnet run --project "CONTROLE DE PEDIDOS.csproj"              # rodar (config.ini fica ao lado do EXE)
dotnet publish "CONTROLE DE PEDIDOS.csproj" -c Release         # single-file self-contained (win-x64)
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe" installer\installer.iss   # instalador
```

**Não há suíte de testes.** Verificação é manual/instrumentada — ver "Como verificar" abaixo.

**Ritual de release** (o usuário pede "gere o setup"): bump em **4 lugares** — `<Version>`, `<AssemblyVersion>`, `<FileVersion>` no `.csproj` e `MyAppVersion` + `VersionInfoVersion` + comentário de cabeçalho no `installer/installer.iss` — depois `publish` + `iscc`. Confirme o `FileVersion` do EXE publicado antes de anunciar. Feature nova = bump minor; refinamento = patch. O instalador embarca só o EXE (recursos são `EmbeddedResource`).

Logs de execução: `%LocalAppData%\CONTROLE DE PEDIDOS\logs\app.log` (rotativo, com PID por linha — várias instâncias gravam no mesmo arquivo).

## Arquitetura em 60 segundos

- **`Form1.cs` (~2400 linhas) + `Form1.Designer.cs` (~1130, escrito à mão)** — janela principal: grids de pendentes/controlados, pipeline de status, filtros, refresh. O Designer concentra o layout responsivo manual (tudo passa por `Dpi()`/`LogicalToDeviceUnits`; mudanças de `Height` durante pass de dock são adiadas via `BeginInvoke`).
- **`DatabaseService` (singleton)** — todo acesso a dados. Integra: `ConsultarIntegraAsync` (wrapper read-only). Controle: `ExecutarControleComLinhasAsync` (devolve rowcount **e notifica as outras instâncias automaticamente** via `pg_notify('cp_mudou')` quando rowcount > 0 — prefira-o para escrita; `ExecutarControleAsync` não notifica).
- **Refresh quase ao vivo** — LISTEN/NOTIFY (conexão dedicada `Pooling=false`) + coalescer 500 ms + timer de segurança com jitter/backoff por foco/pausa em RDP desconectado + dirty-check por assinatura antes de qualquer rebind (rebind custa ~45 ms; 97 % dos ciclos são pulados). Rebinds preservam referências (`CopiarDe`), seleção e scroll.
- **Concorrência** — locking otimista em TODO update do controle: `WHERE` com o estado completo esperado; rowcount 0 = MessageBox de conflito + recarga. Não introduza UPDATE sem esse padrão.
- **Impressão** — subsistema próprio com contrato de geometria (preview vs impressora real têm origens diferentes). **Leia `docs/IMPRESSAO.md` antes de tocar em `*PrintDoc.cs` ou `PrintingHelper.cs`.**
- **Config** — INI plano em `BaseDirectory\config.ini` (por máquina). Chaves precisam ser **globalmente únicas** (o parser ignora seções); nova chave = propriedade + linha no `Salvar()` + `case` no `Carregar()` + campo no `ConfigDialog` + atribuição no `AplicarConfig()`.

## ⛔ PROIBIDO: imprimir em impressora física durante desenvolvimento/testes

**Incidente de 11/11/2026:** harnesses de teste mandaram centenas de jobs da Cópia de Pedido para impressoras físicas (`DISTRIBUIDORA`, `SDN-A4`, `ESCRITORIO`…), confiando em `PrinterSettings.PrintToFile = true` + `PrintFileName` para "desviar para .prn". **Dezenas de folhas foram impressas de verdade.** Nesta máquina (servidor RDS) TODAS as impressoras físicas são conexões remotas (`\\TS-*\...`, `\\SDN-IMP\...`, IP) e o desvio para arquivo **não é garantido** — o job pode chegar ao papel. Regras, sem exceção, para humanos, Claude e sub-sessões:

- **Nunca** chamar `doc.Print()`, `ImprimirDocumento`, `PrintDialog` → OK, nem botão "Imprimir" do app (preview ou grid) com destino a impressora física — nem com `PrintToFile`, nem "só um teste", nem em "uma página". A impressora padrão do Windows (hoje `DISTRIBUIDORA`) é física: **nunca deixe o destino no padrão**.
- **Permitido apenas:**
  1. `PreviewPrintController` (não passa pelo spooler) — é o modo de verificação principal; PNGs via `PreviewPageInfo.Image`.
  2. Modo REAL (`StandardPrintController`) **somente** com `PrinterSettings.PrinterName = "Microsoft Print to PDF"` (driver local, porta `PORTPROMPT:`) + `PrintToFile = true` + `PrintFileName` no scratchpad. O harness **deve ter guard que lança exceção antes do `Print()`** se `PrinterName` for qualquer outra coisa:
     `if (doc.PrinterSettings.PrinterName != "Microsoft Print to PDF") throw new InvalidOperationException("Impressora física proibida em teste");`
  3. Geometria de impressora física (margens físicas, DPI): **só consulta**, sem job — `PrinterSettings.DefaultPageSettings.HardMarginX/Y`, `PrintableArea`, `PrinterSettings.CreateMeasurementGraphics()`. Consultar não imprime; `Print()` imprime.
- Validação em papel é feita **pelo usuário**, a pedido dele. Ao reportar, diga "não testado em papel" — nunca "nada foi impresso" sem ter verificado.
- Ao delegar a sub-sessões qualquer trabalho de impressão, **copie estas regras no brief**.

## Invariantes críticos (quebrar = incidente)

1. **`integrapgsql` é PRODUÇÃO e somente leitura.** Três camadas garantem (connection string `default_transaction_read_only=on`, wrapper `IntegraReadOnlyConnection`, convenção). Única exceção: `AtualizarObservacaoEstmovAsync` — não adicione consumidores de `ConnectionStringIntegraEscrita`. Para explorar o banco em desenvolvimento: `$env:PGOPTIONS='-c default_transaction_read_only=on'` no psql, sempre.
2. **Leituras de reader são posicionais** (`reader.GetString(7)`...). Ao ampliar um SELECT, acrescente colunas **só no fim** — inserir no meio desloca ordinais silenciosamente.
3. **Print docs**: `OriginAtMargins = false` sempre; `GeometriaPagina.Medir` uma vez por job em `OnBeginPrint`; `PrepararPagina(e, _preview)` como primeira coisa do `OnPrintPage`; guard `itensNestaPagina > 0` no laço (anti-job-infinito); `PrintToFile` conta como impressão real. Na Cópia de Pedido, simulação e desenho usam o MESMO `LayoutPagina(medir:)` — todo `g.Draw*` sob `if (!medir)`, aritmética de `y` incondicional.
4. **Alturas de texto** nos relatórios: sempre pela métrica da fonte (`Font.GetHeight`/`MedirTexto`), nunca constante menor que a linha tipográfica (corta descendentes de "g"/"q").
5. **Owner de diálogos**: `ExibirPreviewComImpressao` e MessageBox chamados a partir de um diálogo modal devem ter o diálogo como owner, não o Form1 (senão nascem mal-parentados). Os fluxos `ImprimirSeparacaoAsync`/`ImprimirCopiaPedidoAsync` recebem `Form owner` por isso.
6. **Efeitos colaterais de impressão**: a separação avança APROVADO→EM SEPARAÇÃO **só** pelo botão do grid (parâmetro `pedidoParaAvanco`; a mini-janela passa `null`). A Cópia de Pedido nunca altera status — não adicione wiring de `ImpressaoRealConcluida` nela.
7. **`LOGO DISTRIBUIDORA.png` (raiz, 8000×5000, 153 MB) jamais entra no git** (está no `.gitignore`). O app usa `Resources/logo-distribuidora.png` (recortado, 1200×505) como `EmbeddedResource` com `LogicalName` fixo.
8. **PublishSingleFile**: qualquer asset novo precisa ser `EmbeddedResource` — arquivo solto não é instalado (o installer embarca só o EXE).
9. `PedidoControlado`/`OrcamentoResumo` são bindados: mutações devem preservar referências (`AtualizarMasterList`/`CopiarDe`) ou o binding/seleção quebra.
10. **Escreveu no controle? Notifique.** Via `ExecutarControleComLinhasAsync` (automático) ou `NotificarMudanca(...)` manual — senão as outras instâncias só veem no timer.

## Como verificar (sem papel, sem tocar produção)

- **Impressão** (leia antes a seção **⛔ PROIBIDO** acima): preview exercita o modo PREVIEW; o modo REAL só com "Microsoft Print to PDF" + `PrintToFile` para o scratchpad e guard de impressora no harness — **jamais** "Imprimir em arquivo" numa impressora física. Conferir no log as linhas `LogarPagina` com `DENTRO_DA_AREA_IMPRIMIVEL=True` nos dois modos e contagens de página idênticas (o Warn "simulação previu X, renderizou Y" indica bug de paginação). Padrão de harness: console app `net10.0-windows` no scratchpad que inclui via `<Compile Include>` os fontes de impressão (`Code128.cs`, `Logger.cs`, `AppConfig.cs`, `PrintingHelper.cs`, `Relatorio*PrintDoc.cs`) + o `EmbeddedResource` do logo, renderiza cenários sintéticos com `PreviewPrintController` e `StandardPrintController+PrintToFile` **exclusivamente na "Microsoft Print to PDF"** (com o guard que lança se `PrinterName` for outra), compara contagens e exporta PNGs das páginas (via `PreviewPageInfo.Image`) para inspeção visual.
- **GUI**: rodar o EXE de Debug (o `config.ini` do bin aponta para os bancos locais) e capturar via `PrintWindow`/`CopyFromScreen`. Cliques via `mouse_event` funcionam; **SendKeys de teclas modificadas (Ctrl+X, Esc) NÃO atravessa a camada RDP** — valide atalhos apenas visualmente (rótulo no menu) e peça teste físico ao usuário.
- **Banco**: consultas exploratórias sempre com `PGOPTIONS='-c default_transaction_read_only=on'`. Abrir preview de relatório é livre de efeitos (o avanço de status só dispara em impressão física).
- Selecionar linhas/abrir telas no app rodando é seguro; **não** clicar Avançar/Retornar/Travar/Controlar/Ignorar/Excluir em dados reais durante testes.
