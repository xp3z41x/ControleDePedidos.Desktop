# Controle de Pedidos

> Aplicação desktop para controle do ciclo de vida de pedidos e orçamentos, integrada ao ERP **Integra** da AtuaL Sistemas.

<p align="center">
  <img src="icon.png" width="96" alt="Controle de Pedidos">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-11.0.1-0969da?style=flat-square" alt="version">
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

Cada transição registra timestamp e usuário Windows. Flags como `travado` bloqueiam edições manuais sem criar lock de banco. Todo UPDATE usa **optimistic locking** (WHERE com flags atuais) para evitar conflitos em ambiente multi-usuário.

---

## Principais funcionalidades

- **Dois grids sincronizados** na tela principal: orçamentos pendentes (vindos do Integra) + pedidos controlados (estado interno).
- **Filtros persistentes** por vendedor, rota e etapa do pedido — gravados em `%AppData%`, retornam na próxima abertura.
- **Larguras de coluna personalizáveis** — cada grid memoriza as larguras ajustadas pelo usuário.
- **Aprovação em lote** com seleção de rota via dialog.
- **Exclusão em massa** com seleção múltipla e confirmação destrutiva.
- **Orçamentos ignorados** — tela de "lixeira reversível" para pedidos que não devem aparecer na listagem pendente.
- **Cadastro de rotas** de entrega gerenciado pela própria app.
- **Histórico de exclusões** (append-only) — todo pedido excluído é registrado com orçamento, cliente, NFe, status do momento, data/hora e usuário Windows. Consultável via tela dedicada com busca multi-campo e limpeza automática de registros com mais de 3 meses.
- **Correção de observação** — fluxo excepcional que permite gravar no Integra apenas o campo de observação do orçamento (única exceção ao read-only).
- **Relatórios imprimíveis** — Relatório de Carga e Relatório de NF-e com paginação e preview.
- **Auto-refresh** a cada 3 minutos; monitor de conexão a cada 15 segundos com indicador visual de status.
- **Tema dark nativo** pintado em todos os controles WinForms.

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
2. Execute `Setup-ControleDePedidos-11.0.1.exe`.
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
| **UI (WinForms)** | `Form1` (tela principal), `ConfigDialog`, `RotasForm`, `ExclusaoMassaForm`, `IgnoradosForm`, `HistoricoExclusoesForm`, `RelatorioCargaForm`, `RelatorioNfeForm` |
| **Data access** | `DatabaseService` (singleton) — Npgsql puro, sem ORM. Métodos `ConsultarIntegraAsync` (read-only forçado via `default_transaction_read_only=on` na connection string) e `ExecutarControleAsync` (read/write). |
| **Domínio** | Records e POCOs internos aos forms. `ExclusaoRegistro` (snapshot imutável) é o único tipo cross-form. |
| **Concorrência** | Optimistic locking em todos os UPDATEs — rowcount = 0 dispara reload + aviso de conflito. Transações `BeginTransactionAsync` para DELETE + INSERT atômico em `historico_exclusoes`. |
| **Persistência local** | `%AppData%\CONTROLE DE PEDIDOS\` — `filters.json` (filtros do grid), `column-widths.json` (larguras de coluna). Logs rotativos em `%LocalAppData%\CONTROLE DE PEDIDOS\logs\`. |
| **Tema** | `Theme.cs` — paleta dark aplicada em todos os controles, estilização por célula baseada em valor (ex: status faturado). |

### Bancos de dados

```
integrapgsql (ERP — READ-ONLY)
├── estorc          cabeçalho de orçamentos/pedidos
├── cadcli          clientes
├── cadfun          funcionários/vendedores
└── estmov          movimentação fiscal / NF-e

controlepedidos (próprio — auto-criado)
├── pedidos_controlados      estado do ciclo de vida
├── orcamentos_ignorados     "lixeira reversível"
├── cadastro_rotas           rotas de entrega
└── historico_exclusoes      audit trail append-only
```

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

Saída: `installer\output\Setup-ControleDePedidos-11.0.1.exe`.

Para subir uma nova versão:

1. Bump `<Version>`, `<AssemblyVersion>`, `<FileVersion>` no `.csproj`.
2. Bump `MyAppVersion` em `installer\installer.iss`.
3. Repetir publish + iscc acima.

O `AppId` fixo no Inno Setup (`C7F3B8A2-4D5E-4F6A-9B1C-8E2D3F4A5B6C`) garante upgrades in-place sem reinstalação manual.

---

## Stack técnico

- **.NET 10.0** Windows + WinForms com nullable reference types habilitados
- **C# 13** com `ImplicitUsings` e `FileScopedNamespaces`
- **Npgsql 10.0.2** — driver ADO.NET puro para PostgreSQL, sem ORM
- **Inno Setup 6.7** para empacotamento
- **Single-file self-contained** — todo runtime e dependências bundled no EXE

---

## Licença

Distribuído sob a **[Licença MIT](LICENSE)**. Qualquer pessoa pode baixar, usar, modificar, distribuir e sublicenciar livremente — uso pessoal ou comercial. Sem garantias.
