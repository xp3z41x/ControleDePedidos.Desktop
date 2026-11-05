# Arquitetura

Visão técnica do Controle de Pedidos. Complementa o [README](../README.md) (visão geral) e os documentos de [funcionalidades](FUNCIONALIDADES.md), [banco de dados](BANCO-DE-DADOS.md) e [impressão](IMPRESSAO.md).

## Princípio central

O app acompanha o ciclo de vida de pedidos **sem modificar o ERP Integra**: lê o banco do ERP em modo somente leitura e mantém todo o estado próprio em um segundo banco (`controlepedidos`), criado e migrado automaticamente. É um "sidecar" do ERP.

## Camadas

| Camada | Arquivos | Papel |
|---|---|---|
| Bootstrap | `Program.cs` | Handlers globais de exceção (logam, não suprimem), `ApplicationConfiguration.Initialize`, carga dos stores locais, `Application.Run(new Form1())` |
| Janela principal | `Form1.cs` (~2400 linhas) + `Form1.Designer.cs` (~1130) | Grids, pipeline de status, filtros, refresh, handlers de impressão. O Designer é escrito à mão (não há designer visual) e concentra o sistema de layout responsivo |
| Forms secundários | `RotasForm`, `ExclusaoMassaForm`, `IgnoradosForm`, `HistoricoExclusoesForm`, `RelatorioCargaForm`, `RelatorioNfeForm`, `ConfigDialog`, `CorrigirObservacaoDialog`, `SelecionarRotaDialog`, `ImprimirPorOrcamentoDialog` | Todos modais (`ShowDialog(this)`), construídos sem parâmetros (exceto os dialogs), tema aplicado à mão |
| Acesso a dados | `DatabaseService.cs` (singleton) | Npgsql puro, sem ORM. Conexão nova por operação (pooling default do Npgsql) |
| Impressão | `PrintingHelper.cs`, `Code128.cs`, 4 × `*PrintDoc.cs` | Ver [IMPRESSAO.md](IMPRESSAO.md) |
| Configuração | `AppConfig.cs` + `ConfigDialog.cs` | INI plano em `BaseDirectory\config.ini` (por máquina — ideal no RDS) |
| Infra de UI | `Theme.cs`, `GridLayout.cs`, `OptimizedDataGridView`, `ColumnWidthStore`, `FilterStore`, `SortableBindingList`, `DataGridViewSortHelper` | Tema dark, colunas responsivas, persistência local |
| Log | `Logger.cs` | Rotativo (1 MB × 5), multi-processo (FileShare + retry com jitter), inclui PID em cada linha |

## Os dois bancos

### `integrapgsql` (ERP — PRODUÇÃO, somente leitura)

Read-only garantido em **três camadas**:

1. **Connection string** — `Options=-c default_transaction_read_only=on` (`AppConfig.ConnectionStringIntegra`): o próprio PostgreSQL rejeita INSERT/UPDATE/DELETE na sessão.
2. **Tipo** — `IntegraReadOnlyConnection` (wrapper que só expõe `CreateCommand()`; construtor `internal`, só o `DatabaseService` cria).
3. **Convenção** — todo acesso passa por `ConsultarIntegraAsync<T>(Func<IntegraReadOnlyConnection, Task<T>>)`.

**Única exceção sancionada:** `AtualizarObservacaoEstmovAsync` (fluxo "Corrigir Observação"), que usa `ConnectionStringIntegraEscrita` (sem a flag read-only) para `UPDATE estmov SET observacao=...`. O comentário no código proíbe novos consumidores sem revisão — respeite.

### `controlepedidos` (próprio — leitura/escrita)

Criado automaticamente (`CREATE DATABASE`) se ausente; tabelas garantidas por `GarantirTabelasAsync` (DDL idempotente com `CREATE TABLE IF NOT EXISTS` + `ALTER ... ADD COLUMN IF NOT EXISTS` para migrações). Esquema completo em [BANCO-DE-DADOS.md](BANCO-DE-DADOS.md).

Escrita via:
- `ExecutarControleComLinhasAsync(Func<NpgsqlConnection, Task<int>>)` — devolve rowcount **e emite a notificação `cp_mudou` automaticamente quando `linhas > 0`**. É o caminho preferido para qualquer escrita.
- `ExecutarControleAsync` (com e sem retorno) — sem notificação automática; quem usa precisa chamar `NotificarMudanca(...)` manualmente se a mudança afeta outras instâncias (ex.: fluxo Ignorar).

## Concorrência multiusuário (10+ operadores no mesmo RDS)

- **Optimistic locking em todo UPDATE**: o `WHERE` inclui todas as flags do estado esperado (`aprovado=@aprov AND em_separacao=@em_sep AND conferido=@conf AND liberado=@lib`, mais `travado=FALSE AND em_faturamento=FALSE`). Rowcount 0 = outro operador mexeu → MessageBox "Conflito de Concorrência" + reload. Nunca há lock de banco.
- **Exclusão atômica e com locking otimista**: transação `DELETE pedidos_controlados ... RETURNING` + `INSERT historico_exclusoes`. O `DELETE` também leva o estado esperado no `WHERE` (as 6 flags + `rota`); rowcount 0 é classificado por um `SELECT` na mesma transação em `ResultadoExclusao.JaExcluido` (linha sumiu) ou `Alterado` (linha existe com outro estado), cada um com sua mensagem no Form1 e no resumo da Exclusão em Massa. O INSERT só ocorre se o DELETE afetou linha (evita auditoria falsa em conflito); sucesso notifica `exclusao`.
- **`em_faturamento` é um booleano anônimo** — não há coluna de operador; "o operador responsável deve largá-lo" é convenção social, não enforcement.

## Refresh em tempo quase real

Arquitetura em camadas (medições que a motivaram: refresh custa ~1,7 ms de CPU no Postgres, mas o rebind do DataGridView custa ~45 ms por instância; ~17 mudanças reais/hora):

1. **`LISTEN/NOTIFY` (`canal cp_mudou`)** — toda escrita no controle emite `pg_notify('cp_mudou', 'motivo|IdInstancia')`. Cada instância mantém uma conexão **dedicada** de escuta (`Pooling=false` obrigatório — o `DISCARD ALL` do pool mataria o LISTEN; `KeepAlive=30`; `ApplicationName=ControleDePedidos-Listener`; backoff até 30 s em falha). Payload terminado no próprio `IdInstancia` é descartado (a instância que gravou já se atualizou). Resultado: ação de um operador aparece nas outras telas em < 1 s.
2. **Coalescer de 500 ms** — rajadas de notificações (ex.: exclusão em massa) viram um único refresh.
3. **Timer de segurança** — `timerAutoRefresh` cobre o que o NOTIFY não vê (mudanças feitas no próprio Integra). Alvo dinâmico: 15 s com foco, 30 s sem foco, 120 s minimizado, **+jitter de até 25 %** (instâncias não sincronizam), **pausado** com sessão RDP desconectada/bloqueada (`SystemEvents.SessionSwitch`) e com Integra offline (religa e recarrega ao voltar).
4. **Dirty check** — os dois grids comparam uma assinatura (``-separada) dos dados novos com a anterior; iguais = zero rebind (~97 % dos ciclos). Rebind, quando ocorre, preserva referências dos objetos (`AtualizarMasterList`/`CopiarDe`), seleção, scroll e ordenação.
5. **Enriquecimento eficiente** — a query de enriquecimento no Integra usa `= ANY(@ids)` (texto estável ⇒ plano reaproveitado) em vez de `IN (@p0..@pN)` dinâmico. Alguns forms secundários ainda usam o padrão antigo (aceitável: são telas eventuais).
- **Monitor de conexão**: `System.Threading.Timer` de 15 s (`SELECT 1` nos dois bancos); pill de status na status bar (verde/vermelho); evento `StatusAlterado` dispara carga inicial, `GarantirTabelasAsync` e a escuta de mudanças.

## Persistência local (por perfil Windows)

`%AppData%\CONTROLE DE PEDIDOS\`:
- `filters.json` — filtros de vendedor/rota/etapa do grid principal (etapas como `enum.ToString()` — renomear membro de `EtapaPedido` invalida silenciosamente o filtro salvo).
- `column-widths.json` — larguras de coluna por `gridKey` (8 chaves; só o **arraste manual** persiste — larguras computadas pelo `GridLayout` não).

Ambos com debounce de 500 ms e falha silenciosa. Logs em `%LocalAppData%\CONTROLE DE PEDIDOS\logs\app.log` (rotativo, multi-processo, com PID).

`config.ini` fica **junto do EXE** (`Program Files` na instalação) — compartilhado por todos os usuários da máquina; só admin edita (by design).

## Infra de UI

- **`Theme.cs`** — paleta dark completa (GitHub-dark-like), `EstilizarBotao/TextBox/Grid`, renderers de menu/toolstrip. Destaques não óbvios:
  - Headers de grid centralizados via `CellPainting` manual — o `MiddleCenter` nativo desvia à esquerda porque o DataGridView reserva espaço do glifo de ordenação; o tema desenha texto centrado na célula inteira e a seta por cima.
  - `LimitarAoWorkingArea(form)` — clamp de forms modais à área útil do monitor (chamado no `Load` de todos), com recentragem no Owner.
  - `EstilizarGrid(dgv, gridKey)` liga o grid ao `GridLayout` + persistência de larguras.
- **`GridLayout.cs`** — elimina scroll horizontal: se as larguras preferidas cabem, aplica exatas; senão, rateio proporcional com clamp iterativo nos mínimos (última coluna absorve arredondamento). Estado por grid em `ConditionalWeakTable`; eventos coalescidos via `BeginInvoke`.
- **`OptimizedDataGridView`** — double-buffer (anti-flicker) + flag `SkipAutoAdjust` para refreshes controlados não dispararem auto-ajuste de colunas.
- **Layout do Form1** (Designer à mão) — container centralizado com `Clamp(1000..1870)`; faixa superior (pendentes 28 % clamp 380–460 + detalhes) com **altura ancorada em número inteiro de linhas de grid** (evita meia-linha cortada); card de detalhes em 3 linhas com célula elástica por linha; barra de ações com quebra automática em 2 linhas quando estreita (mudança de Height adiada via `BeginInvoke` — mudar dentro do pass de dock deixaria o grid cobrindo a barra). Toda métrica passa por `Dpi()` = `LogicalToDeviceUnits` (o `AutoScaleMode.Font` não escala layout manual); `DpiChanged` re-executa o layout.

## Dívidas e peculiaridades conhecidas

Registradas para ninguém "descobrir" de novo:

1. `StatusConexao.Verificando` nunca é atribuído — o ramo amarelo do pill de status é código morto.
2. `orcamentos_ignorados.motivo` e `pedidos_controlados.data_aprovacao` são colunas declaradas mas nunca lidas/escritas.
3. `LimparHistoricoExclusoesAntigoAsync` emite `cp_mudou` com motivo `"pedidos"` (efeito colateral do wrapper) — dispara refresh de grids por uma operação que não os afeta. Inócuo.
4. Exclusão em massa grava `vendedor` vazio no histórico (o POCO da tela não carrega o campo) — auditoria menos completa que a exclusão individual.
5. `cadastro_rotas` não tem FK com `pedidos_controlados.rota` (texto livre): excluir rota em uso deixa pedidos com rota "órfã", que aparece como fantasma nos dropdowns de filtro.
6. Comentário obsoleto em `Form1.Designer.cs` ("hospedado na janela modal 'Pendentes'") — o card de pendentes voltou à janela principal na v11.2; a janela modal não existe mais.
7. O grid de pendentes carrega `NomeFantasia` (entra no dirty check) mas não o exibe.
