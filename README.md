# Controle de Pedidos

> Aplicação desktop para controle do ciclo de vida de pedidos e orçamentos, integrada ao ERP **Integra** da AtuaL Sistemas.

<p align="center">
  <img src="icon.png" width="96" alt="Controle de Pedidos">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-11.7.0-0969da?style=flat-square" alt="version">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet" alt=".NET 10">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11%2FServer-0078D6?style=flat-square&logo=windows" alt="Windows">
  <img src="https://img.shields.io/badge/database-PostgreSQL-336791?style=flat-square&logo=postgresql&logoColor=white" alt="PostgreSQL">
  <img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="MIT License">
</p>

---

## Sobre

O app acompanha o ciclo de vida de pedidos **sem modificar o ERP**. Ele lê orçamentos pendentes diretamente do banco do Integra em modo read-only e gerencia o fluxo de aprovação, separação, conferência, liberação e faturamento em um banco próprio — preservando a integridade do sistema-fonte.

### Ciclo de vida do pedido

```
 Pendente  →  Aprovado  →  Em Separação  →  Conferido  →  Liberado  →  Em Faturamento  →  Faturado
```

Flags como `travado` bloqueiam edições manuais sem criar lock de banco; `Em Faturamento` sinaliza que um operador assumiu o pedido; `Faturado` é derivado do próprio ERP (`pedido_venda > 0`), não gravado pelo app. Todo UPDATE usa **optimistic locking** (WHERE com flags atuais) para evitar conflitos em ambiente multi-usuário. A conferência pode vir do próprio Integra (`status_conferencia = 'S'`, exibida como "OK") ou ser feita manualmente no app ("MANUAL").

---

## Principais funcionalidades

- **Dois grids sincronizados** na tela principal: orçamentos pendentes (vindos do Integra) + pedidos controlados (estado interno).
- **Filtros persistentes** por vendedor, rota e etapa do pedido — gravados em `%AppData%`, retornam na próxima abertura.
- **Larguras de coluna personalizáveis** — cada grid memoriza as larguras ajustadas pelo usuário.
- **Aprovação em lote** com seleção de rota via dialog.
- **Exclusão em massa** com seleção múltipla e confirmação destrutiva.
- **Orçamentos ignorados** — tela de "lixeira reversível" para pedidos que não devem aparecer na listagem pendente.
- **Cadastro de rotas** de entrega gerenciado pela própria app.
- **Histórico de exclusões** (append-only) — todo pedido excluído é registrado com orçamento, cliente, NFe, status do momento, data/hora e usuário Windows. Consultável via tela dedicada com busca multi-campo e botão de limpeza dos registros com mais de 3 meses.
- **Travar/Destravar e Assumir/Largar** — cadeado por pedido e sinalização de "em faturamento" para coordenar a equipe sem locks de banco.
- **Correção de observação** — fluxo excepcional que permite gravar no Integra apenas o campo de observação do orçamento (única exceção ao read-only).
- **Relatórios imprimíveis** — Relatório de Carga, Relatório de NF-e, **Relatório de Separação de Mercadorias** (com código de barras Code 128 e avanço automático de status na impressão física) e **Cópia de Pedido** para o cliente (logomarca embutida, preços, valor negociado, "Página X de Y").
- **Imprimir por Orçamento (Ctrl+P)** — mini-janela que imprime separação/cópia de **qualquer** orçamento pelo número, inclusive antigos fora do grid, sem alterar status.
- **Tela quase ao vivo** — ações de outros operadores aparecem em menos de 1 segundo (canal PostgreSQL `LISTEN/NOTIFY` entre as instâncias); mudanças do Integra em até 15 segundos, com backoff por foco, pausa em sessão RDP desconectada e comparação em memória que evita repintar o grid sem necessidade.
- **Tema dark nativo** pintado em todos os controles WinForms, com layout responsivo até 1366×768.

---

## Requisitos

- **Sistema operacional:** Windows 10 versão 1809 ou superior, Windows 11, Windows Server 2019 / 2022 / 2025
- **Arquitetura:** x64
- **Banco de dados:** PostgreSQL acessível pela rede (servidor do Integra + banco de controle próprio)
- **Credenciais:** usuário PostgreSQL com leitura no banco `integrapgsql` e leitura/escrita no banco `controlepedidos`

O instalador é **self-contained** — não é necessário instalar o .NET runtime separadamente.

---

## Instalação

1. Baixe a versão mais recente em **[Releases](https://github.com/xp3z41x/ControleDePedidos.Desktop/releases/latest)**.
2. Execute `Setup-ControleDePedidos-<versão>.exe`.
3. Aceite o prompt UAC (administrador necessário para instalar em `Program Files`).
4. Siga o wizard. Instalação default em `C:\Program Files\Controle de Pedidos\`.
5. No final, deixe marcada a opção **"Executar Controle de Pedidos"** para que a primeira configuração herde a elevação do instalador.
6. Preencha IP, porta, usuário e senha do PostgreSQL na janela de configuração. O banco de controle será criado automaticamente.

> O SmartScreen do Windows pode alertar no primeiro download (instalador não é assinado digitalmente). Clique em **Mais informações → Executar mesmo assim**.

### Permissões e configuração

`config.ini` é gravado na pasta de instalação (`C:\Program Files\Controle de Pedidos\`). Apenas administradores conseguem alterar as configurações de conexão — **por design**. Usuários comuns abrem o app normalmente e trabalham com a configuração existente.

---

## Arquitetura

| Camada | Responsabilidade |
|---|---|
| **UI (WinForms)** | `Form1` (tela principal), `ConfigDialog`, `RotasForm`, `ExclusaoMassaForm`, `IgnoradosForm`, `HistoricoExclusoesForm`, `RelatorioCargaForm`, `RelatorioNfeForm`, `ImprimirPorOrcamentoDialog`, `CorrigirObservacaoDialog`, `SelecionarRotaDialog` |
| **Data access** | `DatabaseService` (singleton) — Npgsql puro, sem ORM. `ConsultarIntegraAsync` (read-only forçado via `default_transaction_read_only=on`), `ExecutarControleAsync`/`ExecutarControleComLinhasAsync` (read/write, com notificação automática entre instâncias). |
| **Impressão** | `PrintingHelper` (geometria de página preview×impressora, preview compartilhado), `Code128` (barcode vetorial), 4 documentos `*PrintDoc` — ver [docs/IMPRESSAO.md](docs/IMPRESSAO.md). |
| **Tempo real** | Canal PostgreSQL `LISTEN/NOTIFY` (`cp_mudou`) com conexão dedicada + coalescer + timer de segurança com jitter/backoff — ver [docs/ARQUITETURA.md](docs/ARQUITETURA.md). |
| **Concorrência** | Optimistic locking em todos os UPDATEs — rowcount = 0 dispara reload + aviso de conflito. Transações `BeginTransactionAsync` para DELETE + INSERT atômico em `historico_exclusoes`. |
| **Persistência local** | `%AppData%\CONTROLE DE PEDIDOS\` — `filters.json` (filtros do grid), `column-widths.json` (larguras de coluna). Logs rotativos em `%LocalAppData%\CONTROLE DE PEDIDOS\logs\`. |
| **Tema/Layout** | `Theme.cs` (paleta dark, estilização por célula baseada em valor), `GridLayout.cs` (colunas proporcionais sem scroll horizontal), layout responsivo com DPI por monitor. |

### Bancos de dados

```
integrapgsql (ERP — READ-ONLY)
├── estorc          cabeçalho de orçamentos/pedidos (status, textos, valores, datas)
├── estior          itens do orçamento (quantidades e preços)
├── estsal          saldos de estoque, local, marca e referência por filial
├── cadcli          clientes
├── cadfun          funcionários/vendedores
├── cadpro          produtos (unidade de medida)
├── cadsys          configuração do sistema (atividade)
└── estmov          movimentação fiscal / NF-e (única tabela com escrita excepcional)

controlepedidos (próprio — auto-criado)
├── pedidos_controlados      estado do ciclo de vida
├── orcamentos_ignorados     "lixeira reversível"
├── cadastro_rotas           rotas de entrega
└── historico_exclusoes      audit trail append-only
```

Detalhes de colunas e semântica em [docs/BANCO-DE-DADOS.md](docs/BANCO-DE-DADOS.md).

---

## Build from source

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (apenas para gerar o instalador)
- Visual Studio 2026 ou VS Code com C# Dev Kit (opcional)

### Compilar e rodar

```powershell
git clone https://github.com/xp3z41x/ControleDePedidos.Desktop.git
cd ControleDePedidos.Desktop
dotnet build
dotnet run --project "CONTROLE DE PEDIDOS.csproj"
```

### Publicar self-contained single-file

```powershell
dotnet publish "CONTROLE DE PEDIDOS.csproj" -c Release
```

Saída: `bin\Release\net10.0-windows\win-x64\publish\CONTROLE DE PEDIDOS.exe` (~52 MB, zero dependências externas).

### Gerar instalador

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\installer.iss
```

Saída: `installer\output\Setup-ControleDePedidos-<versão>.exe`.

Para subir uma nova versão:

1. Bump `<Version>`, `<AssemblyVersion>`, `<FileVersion>` no `.csproj`.
2. Bump `MyAppVersion` e `VersionInfoVersion` em `installer\installer.iss`.
3. Repetir publish + iscc acima.

O `AppId` fixo no Inno Setup (`C7F3B8A2-4D5E-4F6A-9B1C-8E2D3F4A5B6C`) garante upgrades in-place sem reinstalação manual.

---

## Stack técnico

- **.NET 10.0** Windows + WinForms com nullable reference types habilitados
- **C# moderno** com `ImplicitUsings` e collection expressions
- **Npgsql 10.0.2** — driver ADO.NET puro para PostgreSQL, sem ORM
- **Inno Setup 6.7** para empacotamento
- **Single-file self-contained** — todo runtime, dependências e recursos (logomarca embutida) num único EXE

---

## Documentação

Documentação técnica completa em [`docs/`](docs/):

- **[ARQUITETURA.md](docs/ARQUITETURA.md)** — camadas, os dois bancos, concorrência, refresh em tempo quase real, persistência local, infra de UI e peculiaridades conhecidas.
- **[FUNCIONALIDADES.md](docs/FUNCIONALIDADES.md)** — especificação funcional de cada tela, do pipeline de status e das regras de negócio.
- **[BANCO-DE-DADOS.md](docs/BANCO-DE-DADOS.md)** — esquema do `controlepedidos` e todas as tabelas/colunas lidas do `integrapgsql`, com semântica.
- **[IMPRESSAO.md](docs/IMPRESSAO.md)** — o subsistema de impressão: contrato de geometria preview×impressora, paginação simulada, código de barras, os 4 relatórios.

`CLAUDE.md` na raiz orienta agentes de IA (Claude Code) que trabalham neste repositório.

---

## Licença

Distribuído sob a **[Licença MIT](LICENSE)**. Qualquer pessoa pode baixar, usar, modificar, distribuir e sublicenciar livremente — uso pessoal ou comercial. Sem garantias.
