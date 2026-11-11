# Funcionalidades — especificação

Especificação funcional de cada área do app, na versão 11.8.0. Números de comportamento (regras, guardas, mensagens) refletem o código; âncoras técnicas nos documentos de [arquitetura](ARQUITETURA.md), [banco](BANCO-DE-DADOS.md) e [impressão](IMPRESSAO.md).

## Janela principal

Três cards em um container centralizado e responsivo:

1. **Orçamentos Pendentes** (faixa superior, esquerda) — orçamentos do Integra ainda não controlados.
2. **Detalhes do Orçamento** (faixa superior, direita) — dados do orçamento selecionado em qualquer um dos grids.
3. **Controle de Pedidos** (largura total, embaixo) — os pedidos sob gestão, com o pipeline de status.

Status bar: pill de conexão (verde "Conectado - IP" / amarelo "Parcial - IP (sem <banco>)" quando só um dos bancos responde / vermelho "Desconectado") + botão **Configurações**. Os dois bancos são testados de forma independente: com o Integra fora e o controle no ar, as telas que só usam o controle (Histórico de Exclusões, rotas, Exclusão em Massa etc.) continuam funcionando.

Menu: **ROTAS** → Cadastro de Rotas · **PEDIDOS** → Exclusão em Massa, Histórico de Exclusões · **RELATÓRIOS** → Relatório de Carga, Relatório de NF-e, Imprimir por Orçamento... (Ctrl+P) · **OBSERVAÇÃO** → Corrigir Observação.

### Orçamentos Pendentes

- Fonte: `estorc` na filial configurada, com `pedido_venda = 0` (não faturado), `perdido = 'N'` e `orcamento > OrçamentoMínimo` (config). Nome do cliente via `cadcli` (fallback para o nome digitado no orçamento).
- Exclui em memória os já controlados e os ignorados.
- **Controlar Pedido**: escolhe rota (obrigatória, de `cadastro_rotas`) → INSERT em `pedidos_controlados` com `aprovado = FALSE` (o pedido nasce na etapa "(Não aprovado)"; é o botão Avançar que aprova). `ON CONFLICT DO NOTHING`; corrida com outro operador gera aviso e recarga.
- **Ignorar Orçamento**: confirmação → vai para a "lixeira reversível" (`orcamentos_ignorados`), some da lista.
- **Ignorados**: abre a tela de restauração.

### Detalhes do Orçamento

Selecionar linha em qualquer grid carrega do Integra: VALOR TOTAL (destaque, R$), CIDADE, UF, VENDEDOR, FORMA DE PAGAMENTO (`texto01`), NÚMERO DO PEDIDO (`texto02`), VOLUMES (`texto03` + sufixo "VOLUMES"), PESO (`texto04` + sufixo "KG"), OBSERVAÇÃO (com quebra). Campo vazio = "—". Proteção contra corrida: resposta de seleção antiga é descartada.

### Controle de Pedidos (grid principal)

Colunas: ORÇ., CLIENTE, NOME FANTASIA, ROTA, APROVADO, SEPARANDO, CONFERIDO, LIBERADO, FATURADO — as de etapa com pintura por valor:

| Texto | Significado | Cor |
|---|---|---|
| SIM / OK | etapa cumprida (OK = conferido pelo Integra) | verde |
| MANUAL | conferido manualmente no app | verde claro |
| NÃO | pendente | vermelho |
| TRAVADO | pedido travado (sobrepõe as 4 colunas de etapa) | âmbar |
| EM FATURAMENTO | assumido por um operador | azul-petróleo |
| `<número>` | nº do pedido de venda (faturado no ERP) | verde |

**Filtros persistentes** (dropdowns Vendedor / Rota / Etapa + botão Limpar): opções vêm do próprio dataset; macro "(Marcar todos)/(Limpar)" em cada menu; seleção ativa destaca o botão em azul com contagem; persistidos por perfil Windows e restaurados na abertura. Contagem "N de M registros" quando filtrando. Botão ⟳ Atualizar força recarga.

## Pipeline de status

```
(Não aprovado) → Aprovado → Em Separação → Conferido → Liberado → [Em Faturamento] → Faturado
```

- **Avançar ▶ / ◀ Retornar**: um passo por clique, com locking otimista (conflito = aviso + recarga). Bloqueados se travado ou em faturamento.
- **Conferência**: pode vir do **Integra** (`estorc.status_conferencia = 'S'` → "OK") ou ser **manual** (→ "MANUAL"). O avanço usa a conferência *efetiva* (Integra OU manual) — se o Integra já conferiu, Avançar pula direto para Liberado. **Retornar a conferência do Integra é bloqueado** ("a reversão deve ser feita no próprio Integra").
- **Faturado** não é gravado no app: deriva de `estorc.pedido_venda > 0` (a coluna FATURADO mostra o número do pedido de venda).
- **🔒 Travar / 🔓 Destravar**: congela o pedido contra mudanças de status/rota/exclusão. Não é uma etapa — é um cadeado sobre a etapa atual. Bloqueado se em faturamento.
- **Assumir / Largar**: marca `em_faturamento` (exige Em Separação + conferido efetivo + Liberado; recusa se já faturado). Sinaliza "um operador está faturando este pedido" — enquanto ativo, ninguém mexe no status. Sem registro de *quem* assumiu (booleano anônimo; a etiqueta é convenção da equipe).
- **Alterar Rota**: troca a rota (dialog de seleção), com locking otimista sobre a rota anterior.
- **Excluir Pedido**: confirmação destrutiva → remoção com **auditoria atômica** no Histórico de Exclusões (tipo MANUAL).
- **🖨 Imprimir** (Relatório de Separação) e **📄 Cópia de Pedido**: ver [IMPRESSAO.md](IMPRESSAO.md). A impressão FÍSICA da separação pelo botão do grid avança APROVADO → EM SEPARAÇÃO automaticamente (uma vez, com guardas); a Cópia nunca altera nada.

## Imprimir por Orçamento (menu RELATÓRIOS, Ctrl+P)

Mini-janela para imprimir **qualquer orçamento da filial** — inclusive antigos, fora do grid:

1. Digite o número (só dígitos) → **Buscar** (Enter).
2. Conferência: cliente, "Pedido em <data/hora da digitação no Integra>" e chip de situação ("Controlado — <etapa>" verde / "Não controlado" cinza).
3. Botões **Relatório de Separação** e **Cópia de Pedido** habilitam só após busca bem-sucedida; editar o número desabilita até nova busca.
4. Impressão por aqui **nunca altera status** (ferramenta de reimpressão/consulta).
5. A janela permanece aberta para imprimir vários em sequência; Esc/Fechar encerra. Orçamento inexistente = aviso inline em vermelho (sem popup).

## Formulários secundários

- **Cadastro de Rotas**: CRUD simples (adicionar com Enter, excluir com confirmação). Nome único (violação → "Já existe uma rota com esse nome"). Excluir rota não afeta pedidos que a usam (texto livre).
- **Ignorados**: lista a lixeira (com cliente/cidade/UF do Integra); **Restaurar Selecionado** devolve o orçamento à lista de pendentes. Sem exclusão definitiva.
- **Exclusão em Massa**: grid com checkboxes + atalhos Marcar Todos / Desmarcar / **Marcar Faturados** / **Marcar Rotas** (multi-seleção com "(Sem rota)"). Confirmação destrutiva única; execução item a item com auditoria (tipo MASSA) e relatório final (excluídos / já excluídos por outro usuário / alterados por outro usuário — não excluídos / erros).
- **Histórico de Exclusões**: consulta append-only — DATA, TIPO (MANUAL/MASSA), ORÇAMENTO, STATUS no momento, CLIENTE, FANTASIA, ROTA, VENDEDOR, PED. VENDA, NFE, USUÁRIO (Windows). Busca instantânea multi-campo (10 campos, em memória). Botão **"Excluir histórico com mais de 3 meses"** (manual, com confirmação de irreversibilidade).
- **Relatório de Carga**: romaneio de entrega — seleção por checkbox/rota, saída ordenada por Cidade→Cliente, colunas com Nº pedido/volumes e coluna ASSINATURA em branco para o recebedor; totais de pedidos e volumes no rodapé. Preview + impressão A4 paisagem.
- **Relatório de NF-e**: correlaciona pedidos controlados com notas fiscais (via `estmov` na Filial Fiscal); saída ordenada por Cliente com coluna CONFERIDO em branco para marcação manual. Preview + impressão.
- **Corrigir Observação**: fluxo excepcional que grava `estmov.observacao` no Integra (a ÚNICA escrita no ERP) — pedido + nova observação numéricos, confirmação citando filial fiscal, resposta com nº de registros afetados.

## Configurações (botão na status bar; exige admin para salvar em Program Files)

| Seção | Campos |
|---|---|
| SERVIDOR | IP, Porta, Usuário, Senha |
| BANCOS DE DADOS | Banco Integra, Banco Controle |
| FILTROS DE BUSCA | Filial (comercial), Orçamento Mínimo |
| FISCAL | Filial Fiscal (0 = não configurada; usada pela correlação de NF-e e Corrigir Observação) |
| RELATÓRIO DE SEPARAÇÃO | paisagem, ocultar cancelados, ordenar por LOCAL, 2 casas decimais |
| CÓPIA DE PEDIDO | paisagem, ordenar alfabeticamente, exibir porcentagem de desconto e aviso de arredondamento (padrão: ligado; desligado = só valores em R$) |
| DADOS DA EMPRESA (CÓPIA DE PEDIDO) | Razão Social, CNPJ, IE, Endereço, Cidade/UF/CEP, Telefone, E-mail, WhatsApp — impressos no cabeçalho da Cópia; vazios são omitidos |

Botão **Testar** valida a conexão com os **dois** bancos antes de salvar e reporta cada um (Integra / controle; banco de controle inexistente com o servidor acessível aparece como "será criado"). A senha é gravada e lida sem `Trim` (espaços nas pontas são preservados); os demais campos são aparados. O `config.ini` é por máquina: no servidor RDS, preencher uma vez vale para todos os operadores.

## Comportamentos transversais

- **Refresh quase ao vivo**: ações de outros operadores aparecem em < 1 s (canal de notificação); mudanças do próprio Integra em até 15 s (com foco). Detalhes em [ARQUITETURA.md](ARQUITETURA.md).
- **Conflito de concorrência**: qualquer operação sobre estado desatualizado avisa e recarrega — última escrita não vence silenciosamente.
- **Larguras de coluna**: arraste manual é lembrado por grid/por perfil; sem espaço, as colunas encolhem proporcionalmente (sem scroll horizontal).
- **Telas pequenas**: layout responsivo até 1366×768 (barra de ações quebra em 2 linhas; forms modais são clampados à área útil do monitor).
- **Log**: `%LocalAppData%\CONTROLE DE PEDIDOS\logs\app.log` — inclui diagnóstico completo de impressão (impressora, geometria, `DENTRO_DA_AREA_IMPRIMIVEL`).
