# Bancos de dados

Dois bancos PostgreSQL no mesmo servidor. Regra de ouro: **`integrapgsql` é PRODUÇÃO do ERP e é somente leitura** (exceção única documentada abaixo); `controlepedidos` é do app e é auto-criado/migrado.

## `controlepedidos` (próprio)

DDL em `DatabaseService.GarantirTabelasAsync` — idempotente (`CREATE TABLE IF NOT EXISTS` + `ALTER ... ADD COLUMN IF NOT EXISTS`), executado a cada conexão do banco de controle.

### `pedidos_controlados`

| Coluna | Tipo | Observações |
|---|---|---|
| `orcamento` | `INTEGER PRIMARY KEY` | nº do orçamento no Integra |
| `cliente` | `VARCHAR(300) NOT NULL DEFAULT ''` | snapshot do momento do controle |
| `nome_fantasia` | `VARCHAR(300) NOT NULL DEFAULT ''` | idem |
| `rota` | `VARCHAR(200) NOT NULL DEFAULT ''` | **texto livre** — sem FK com `cadastro_rotas` |
| `aprovado`, `em_separacao`, `conferido`, `liberado`, `travado` | `BOOLEAN NOT NULL DEFAULT FALSE` | pipeline + cadeado |
| `em_faturamento` | `BOOLEAN NOT NULL DEFAULT FALSE` | via `ALTER` (migração); booleano anônimo, sem operador |
| `data_aprovacao` | `TIMESTAMP NOT NULL DEFAULT NOW()` | **coluna morta** — nunca lida/escrita explicitamente |

Não existe coluna `faturado`: o estado Faturado deriva de `estorc.pedido_venda > 0` no Integra. Nenhum índice além da PK.

Todo UPDATE usa **locking otimista** — o `WHERE` inclui o estado esperado completo (`travado=FALSE AND em_faturamento=FALSE AND aprovado=@ AND em_separacao=@ AND conferido=@ AND liberado=@`); rowcount 0 = conflito. A exclusão (`ExcluirPedidoComHistoricoAsync`) segue o mesmo padrão no `DELETE` (flags + `rota`).

### `orcamentos_ignorados`

| Coluna | Tipo | Observações |
|---|---|---|
| `orcamento` | `INTEGER PRIMARY KEY` | |
| `motivo` | `VARCHAR(300)` | **coluna morta** — nunca preenchida |
| `data_inclusao` | `TIMESTAMP NOT NULL DEFAULT NOW()` | só usada no ORDER BY da tela |

### `cadastro_rotas`

| Coluna | Tipo |
|---|---|
| `id` | `SERIAL PRIMARY KEY` |
| `nome` | `VARCHAR(200) NOT NULL UNIQUE` (violação 23505 tratada na UI) |

### `historico_exclusoes` (append-only, auditoria)

`id SERIAL PK` · `orcamento INTEGER` · `cliente`/`nome_fantasia VARCHAR(300)` · `rota`/`vendedor VARCHAR(200)` · `pedido_venda INTEGER` · `nota_fiscal VARCHAR(20)` · `status VARCHAR(40)` (texto consolidado do momento) · as 6 flags booleanas · `tipo_exclusao VARCHAR(10)` (`MANUAL`/`MASSA`) · `usuario_windows VARCHAR(100)` (`Environment.UserName`) · `data_exclusao TIMESTAMP DEFAULT NOW()`.

Índices: `ix_historico_exclusoes_data (data_exclusao DESC)`, `ix_historico_exclusoes_orcamento (orcamento)`.

Gravação **atômica** com o DELETE do pedido (transação; INSERT só se o DELETE afetou linha). Limpeza: manual, botão "> 3 meses" na tela de histórico.

### Canal `cp_mudou` (LISTEN/NOTIFY)

- Payload: `"<motivo>|<IdInstancia>"` — motivos: `pedidos` (automático via `ExecutarControleComLinhasAsync` quando rowcount > 0), `ignorados`, `exclusao`, `historico` (limpeza do histórico de exclusões).
- Instância emissora descarta o próprio eco (sufixo do payload).
- Escuta em conexão dedicada `Pooling=false` (obrigatório) + `KeepAlive=30`.

## `integrapgsql` (ERP Integra — PRODUÇÃO, somente leitura)

Acesso via `ConsultarIntegraAsync` (sessão com `default_transaction_read_only=on`). Tabelas e colunas que o app usa:

### `estorc` — cabeçalho de orçamentos/pedidos

| Coluna | Uso no app |
|---|---|
| `filial`, `orcamento` | chave de tudo (sempre filtrado pela Filial comercial do config) |
| `pedido_venda`, `serie_venda` | `> 0` = faturado; nº exibido na coluna FATURADO |
| `perdido` | filtro dos pendentes (`= 'N'`) |
| `cliente`, `vendedor` | FKs lógicas para `cadcli`/`cadfun` |
| `nome`, `cidade`, `estado` | fallbacks quando o cadastro não preenche |
| `observacao` | separação/cópia e painel de detalhes |
| `texto01..texto04` | forma de pagamento / nº do pedido / volumes / peso (rótulos por convenção da empresa) |
| `vr_total` | valor total do pedido (painel de detalhes) |
| `vr_negociado` | abatimento interno (créditos etc.); ≠ 0 imprime a linha VALOR NEGOCIADO na Cópia |
| `vr_prazo` | **não usado** — soma de `quantidade × estior.vr_prazo`, mas inclui itens depois cancelados (diverge em 3 de 3.832 orçamentos, medido em 2026-11-09). A Cópia soma o total original pelos itens ativos. |
| `desconto_vista` | **não usado** — desconto à vista do cabeçalho; é o que separa `estior.vr_vista` de `estior.vr_prazo` |
| `status_conferencia` | `'S'` = conferido pelo Integra (coluna CONFERIDO "OK"; bloqueia retorno manual) |
| `data_inc`, `hora_inc` | data/hora da digitação (impressa nos relatórios; `hora_inc` é `varchar(5)` "HH:MM") |
| `dt_emissao` | data comercial de emissão (só data; **não** usada nos relatórios — pode divergir da digitação) |

Fatos medidos em produção (filial 5, ago/2026): `data_inc`/`hora_inc` 100 % preenchidos; soma dos `estior.vr_total` ativos = `estorc.vr_total` em 100 % de 500 orçamentos recentes.

### `estior` — itens do orçamento

`filial + orcamento + item` · `matricula` · `descricao` (snapshot do momento da venda) · `quantidade` · `vr_unitario` (já líquido de desconto de item) · `vr_total` (= `quantidade × vr_unitario`, 0 divergências medidas em 5.000 itens) · `vr_prazo` (unitário ORIGINAL, ver abaixo) · `cancelado` (`'S'` = cancelado; a Cópia SEMPRE oculta; a Separação oculta por config).

**Preços do item — qual coluna é qual** (medido em 2026-11-09, filial 5, orçamentos abr–nov/2026, 96.232 itens ativos):

| Coluna | Significado | Uso no app |
|---|---|---|
| `vr_prazo` | Unitário **original**, antes do desconto — é o "Valor a Prazo" que o impresso do próprio Integra mostra. Nunca nulo/zero; nunca menor que `vr_unitario` (0 acréscimos); sempre 2 casas efetivas. Igual a `vr_unitario` = item sem desconto. | Cópia de Pedido: `CopiaPedidoItem.VrOriginal` (última coluna do SELECT de itens). Defensivo no código: 0 ou abaixo do unitário vira `vr_unitario` (item tratado como sem desconto). |
| `vr_unitario` / `vr_total` | Unitário e total **já com desconto** (o que o cliente paga). | Cópia e Separação. |
| `vr_vista` | `vr_prazo` menos o `estorc.desconto_vista` do cabeçalho (difere de `vr_prazo` em 611 itens). | **Não usar.** |
| `vr_venda` | Preço de cadastro do produto no momento (difere de `vr_prazo` em ~6 % dos itens; 28 zeros; 727 casos abaixo de `vr_unitario`). | **Não usar.** |
| `perc_desconto` | % de desconto **digitada** pelo vendedor. Diverge da % efetiva (calculada dos preços) por arredondamento em 208 itens; 31 itens têm % sem diferença alguma de preço. | **Não usar** — a % impressa na Cópia é calculada de `vr_prazo`/`vr_unitario`. |

Conferência contra o impresso do Integra (2026-11-09, PDFs `EXEMPLO 1..3`): orçamentos 2522, 3807 e 3739 batem item a item em `vr_prazo`, `vr_unitario` e `vr_total`, e nos totais (Σ `quantidade × vr_prazo` / Σ `vr_total`): 2522 = 20.334,60 / 19.187,00 · 3807 = 1.024,45 / 987,22 · 3739 = 839,50 / 814,54. Orçamentos sem desconto (ex.: 3843–3845) têm `vr_prazo = vr_unitario` em todos os itens.

### `estsal` — saldos/estoque por filial

`filial + matricula` · `local` (endereço físico) · `referencia`, `marca` · colunas de saldo (`anteriorv/a`, `entradav/a`, `saidav/a`, `consignacaov/a`, `condicionalv/a`, `consertov/a`, `servico`, `estoque_reserva`, `entregafuturav/a`) — as fórmulas de SALDO ATUAL e ESTOQUE FÍSICO do Relatório de Separação escolhem o sufixo `v`/`a` por `cadsys.atividade` (SQL documentado em `Form1.cs`, fornecido pelo desenvolvedor do Integra).

### `cadcli` — clientes

`cliente` (código) · `nome`, `fantasia` · `natureza` (`'F'`/`'J'` — decide CPF/RG vs CNPJ/IE na Cópia) · `cpf` (só dígitos: 11 = CPF, 14 = CNPJ; máscara e reposição de zeros são do app) · `identidade` (RG p/ F, IE p/ J) · `endereco`, `numero`, `bairro`, `cidade`, `estado`, `cep` · `ddd`+`fone`, `ddd_celular`+`celular` · `e_mail`.

### `cadfun` — funcionários/vendedores

`funcionario` (código) ↔ `estorc.vendedor` · `nome`. (Não existe tabela `cadven`.)

### `cadpro` — produtos

`matricula` · `unidade` (UN, PR, PA, KT... — coluna UN da Separação).

### `cadsys` — configuração do sistema

`atividade` (`'V'` varejo / outro) — decide as colunas de saldo do `estsal`.

### `estmov` — movimentação fiscal

- **Correlação de NF-e**: `estmov.observacao` (texto) armazena, por convenção do processo fiscal da empresa, o número do orçamento; o app busca `nota_fiscal` com `WHERE filial = FilialFiscal AND observacao IN (...)` (best-effort, nunca lança). Um orçamento pode ter várias linhas (reemissões): vale só a mais recente — `DISTINCT ON (observacao) … ORDER BY dtemissao DESC, nota_fiscal DESC` — e notas `0` são ignoradas.
- **Única escrita sancionada no Integra**: fluxo "Corrigir Observação" → `UPDATE estmov SET observacao=@obs WHERE filial=@filialFiscal AND pedido=@pedido` (sem LIMIT; pode afetar N linhas — o app informa quantas). Não adicionar novos consumidores da connection string de escrita sem revisão.

## Conexões e filiais

| Config | Uso |
|---|---|
| `Filial` (comercial) | todas as queries de `estorc`/`estior`/`estsal` |
| `FilialFiscal` | só `estmov` (correlação NF-e + Corrigir Observação); `0` = recurso desligado |

Connection strings montadas em `AppConfig`: Integra read-only, Integra escrita (excepcional), Controle, Servidor (banco `postgres`, para o `CREATE DATABASE`). `Timeout=5`, `SSL Mode=Prefer` em todas.

## Explorando os bancos com segurança (para desenvolvimento)

Sempre em sessão somente leitura:

```powershell
$env:PGPASSWORD='<senha>'; $env:PGOPTIONS='-c default_transaction_read_only=on'
& "C:\Program Files\PostgreSQL\13\bin\psql.exe" -h localhost -U postgres -d integrapgsql -c "SELECT ..."
```

JAMAIS alterar dados do `integrapgsql` — é produção do ERP.
