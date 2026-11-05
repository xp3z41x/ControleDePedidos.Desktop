namespace CONTROLE_DE_PEDIDOS
{
    /// <summary>
    /// Mini-janela do menu RELATÓRIOS (Ctrl+P): imprime Relatório de Separação ou
    /// Cópia de Pedido digitando o número do orçamento — inclusive de pedidos
    /// antigos que não estão no grid de controle. Impressão por aqui NUNCA altera
    /// status (decisão de projeto: é ferramenta de reimpressão/consulta).
    /// Fluxo: digitar → Buscar (Enter) → conferir cliente/data → imprimir.
    /// </summary>
    public class ImprimirPorOrcamentoDialog : FormEscalavel
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private readonly Func<int, PedidoControlado?> _localizarControlado;
        private readonly Func<int, Form, Task> _imprimirSeparacao;
        private readonly Func<int, Form, Task> _imprimirCopia;

        private TextBox txtOrcamento = null!;
        private Button btnBuscar = null!;
        private Label lblCliente = null!;
        private Label lblDataHora = null!;
        private Label lblChip = null!;
        private Button btnSeparacao = null!;
        private Button btnCopia = null!;
        private Button btnFechar = null!;

        /// <summary>Orçamento validado pela última busca; impressão usa SEMPRE este valor
        /// (nunca re-parse do campo). Zero = nada encontrado/invalidação.</summary>
        private int _orcamentoEncontrado;

        /// <summary>Busca/impressão em andamento: o X/Alt+F4 fica bloqueado (ver OnFormClosing).</summary>
        private bool _ocupado;

        public ImprimirPorOrcamentoDialog(
            Func<int, PedidoControlado?> localizarControlado,
            Func<int, Form, Task> imprimirSeparacao,
            Func<int, Form, Task> imprimirCopia)
        {
            _localizarControlado = localizarControlado;
            _imprimirSeparacao = imprimirSeparacao;
            _imprimirCopia = imprimirCopia;
            InitializeUI();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Imprimir por Orçamento";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            ClientSize = Dpi(400, 252);

            var lblOrcamento = new Label
            {
                Text = "Orçamento:",
                Location = DpiPt(16, 16),
                AutoSize = true,
                ForeColor = Theme.TextPrimary
            };

            txtOrcamento = new TextBox
            {
                Location = DpiPt(16, 40),
                Size = Dpi(252, 26),
                Font = Theme.FontRegular,
                MaxLength = 9
            };
            Theme.EstilizarTextBox(txtOrcamento);
            txtOrcamento.KeyPress += SomenteDigitos;
            // Qualquer edição invalida o resultado: impressão só com busca fresca.
            txtOrcamento.TextChanged += (_, _) => InvalidarResultado();

            btnBuscar = new Button
            {
                Text = "Buscar",
                Size = Dpi(104, 30),
                Location = DpiPt(280, 38)
            };
            Theme.EstilizarBotao(btnBuscar, primario: true);
            btnBuscar.Click += BtnBuscar_Click;

            lblCliente = new Label
            {
                Location = DpiPt(16, 80),
                Size = Dpi(368, 20),
                AutoSize = false,
                AutoEllipsis = true,   // nome de cliente longo trunca com "…"
                Font = Theme.FontRegularBold,
                ForeColor = Theme.TextPrimary
            };

            lblDataHora = new Label
            {
                Location = DpiPt(16, 102),
                AutoSize = true,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary
            };

            lblChip = new Label
            {
                Location = DpiPt(16, 124),
                AutoSize = true,
                Font = Theme.FontStatus,
                Padding = Dpi(6, 2, 6, 2)
            };

            btnSeparacao = new Button
            {
                Text = "Relatório de Separação",
                Size = Dpi(184, 36),
                Location = DpiPt(16, 156)
            };
            Theme.EstilizarBotao(btnSeparacao, primario: true);
            btnSeparacao.Click += async (_, _) => await ExecutarImpressaoAsync(_imprimirSeparacao);

            btnCopia = new Button
            {
                Text = "Cópia de Pedido",
                Size = Dpi(176, 36),
                Location = DpiPt(208, 156)
            };
            Theme.EstilizarBotao(btnCopia);
            btnCopia.Click += async (_, _) => await ExecutarImpressaoAsync(_imprimirCopia);

            btnFechar = new Button
            {
                Text = "Fechar",
                Size = Dpi(90, 32),
                Location = DpiPt(294, 204),
                DialogResult = DialogResult.Cancel
            };
            Theme.EstilizarBotao(btnFechar);

            // Enter = Buscar SEMPRE (imprimir exige clique explícito — evita um Enter
            // duplo abrir preview sem querer); Esc = Fechar.
            AcceptButton = btnBuscar;
            CancelButton = btnFechar;

            Controls.AddRange([lblOrcamento, txtOrcamento, btnBuscar,
                lblCliente, lblDataHora, lblChip, btnSeparacao, btnCopia, btnFechar]);

            ResumeLayout(false);
            PerformLayout();

            InvalidarResultado();
            txtOrcamento.Select();
        }

        private static void SomenteDigitos(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        // ===== Estados =====

        /// <summary>Estado Inicial (e pós-edição do campo): resultado some, impressão desabilita.</summary>
        private void InvalidarResultado()
        {
            _orcamentoEncontrado = 0;
            lblCliente.Text = "";
            lblCliente.ForeColor = Theme.TextPrimary;
            lblDataHora.Text = "";
            lblChip.Text = "";
            lblChip.BackColor = Color.Transparent;
            btnSeparacao.Enabled = false;
            btnCopia.Enabled = false;
        }

        private void MostrarErroInline(string mensagem)
        {
            InvalidarResultado();
            lblCliente.Text = mensagem;
            lblCliente.ForeColor = Theme.Error;
        }

        private void HabilitarControles(bool habilitar)
        {
            _ocupado = !habilitar;
            // ReadOnly (não Enabled) para o campo não perder o foco durante o async.
            txtOrcamento.ReadOnly = !habilitar;
            btnBuscar.Enabled = habilitar;
            btnFechar.Enabled = habilitar;
        }

        // ===== Busca =====

        private sealed class InfoOrcamento
        {
            public int Orcamento;
            public string Cliente = "";
            public string DataHora = "";
        }

        private async void BtnBuscar_Click(object? sender, EventArgs e)
        {
            if (!int.TryParse(txtOrcamento.Text.Trim(), out int n) || n <= 0)
            {
                MostrarErroInline("Informe um número de orçamento válido.");
                txtOrcamento.Focus();
                return;
            }

            HabilitarControles(false);
            btnSeparacao.Enabled = false;
            btnCopia.Enabled = false;
            lblCliente.ForeColor = Theme.TextMuted;
            lblCliente.Text = "Buscando...";
            try
            {
                var info = await _db.ConsultarIntegraAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    // Sem filtro de status: alcança qualquer orçamento antigo da filial.
                    cmd.CommandText = @"
                        SELECT e.orcamento,
                               COALESCE(c.nome, '')     AS cliente,
                               e.data_inc,
                               COALESCE(e.hora_inc, '') AS hora_inc
                        FROM estorc e
                        LEFT JOIN cadcli c ON c.cliente = e.cliente
                        WHERE e.filial = @filial AND e.orcamento = @orc";
                    cmd.Parameters.AddWithValue("filial", _db.Config.Filial);
                    cmd.Parameters.AddWithValue("orc", n);
                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    if (!await reader.ReadAsync().ConfigureAwait(false)) return null;
                    string dataHora = reader.IsDBNull(2)
                        ? "—"
                        : $"{reader.GetDateTime(2):dd/MM/yyyy} {reader.GetString(3).Trim()}".TrimEnd();
                    return new InfoOrcamento
                    {
                        Orcamento = reader.GetInt32(0),
                        Cliente = reader.GetString(1),
                        DataHora = dataHora
                    };
                });

                if (info == null)
                {
                    MostrarErroInline($"Orçamento {n} não encontrado no Integra (filial {_db.Config.Filial}).");
                    txtOrcamento.Focus();
                    txtOrcamento.SelectAll();
                    return;
                }

                MostrarEncontrado(info);
            }
            catch (Exception ex)
            {
                Logger.Error("Erro na busca do Imprimir por Orçamento", ex);
                MostrarErroInline($"Erro na busca: {ex.Message}");
            }
            finally
            {
                HabilitarControles(true);
            }
        }

        private void MostrarEncontrado(InfoOrcamento info)
        {
            _orcamentoEncontrado = info.Orcamento;
            lblCliente.ForeColor = Theme.TextPrimary;
            lblCliente.Text = string.IsNullOrWhiteSpace(info.Cliente)
                ? $"Orçamento {info.Orcamento}"
                : $"Orçamento {info.Orcamento} — {info.Cliente}";
            lblDataHora.Text = $"Pedido em {info.DataHora}";

            var controlado = _localizarControlado(info.Orcamento);
            if (controlado != null)
            {
                lblChip.Text = $"Controlado — {controlado.EtapaAtual.LabelAmigavel()}";
                lblChip.BackColor = Theme.StatusSimBg;
                lblChip.ForeColor = Theme.StatusSimFg;
            }
            else
            {
                lblChip.Text = "Não controlado";
                lblChip.BackColor = Theme.BgInput;
                lblChip.ForeColor = Theme.TextSecondary;
            }

            btnSeparacao.Enabled = true;
            btnCopia.Enabled = true;
        }

        // ===== Impressão =====

        private async Task ExecutarImpressaoAsync(Func<int, Form, Task> imprimir)
        {
            if (_orcamentoEncontrado <= 0) return;
            int orcamento = _orcamentoEncontrado;

            HabilitarControles(false);
            btnSeparacao.Enabled = false;
            btnCopia.Enabled = false;
            try
            {
                // this = owner: preview e MessageBoxes parentados NESTA janela modal.
                // Erros já são tratados e exibidos dentro do método do Form1.
                await imprimir(orcamento, this);
            }
            catch (Exception ex)
            {
                // Rede de segurança: o handler é async void — exceção escapando daqui
                // derrubaria o app inteiro.
                Logger.Error($"Erro ao imprimir orçamento {orcamento} pela mini-janela", ex);
                MessageBox.Show(this, $"Erro ao imprimir: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                HabilitarControles(true);
                bool resultadoValido = _orcamentoEncontrado == orcamento && orcamento > 0;
                btnSeparacao.Enabled = resultadoValido;
                btnCopia.Enabled = resultadoValido;
                // Janela fica aberta, pronta para o próximo número (decisão do usuário).
                txtOrcamento.Focus();
                txtOrcamento.SelectAll();
            }
        }

        // btnFechar fica desabilitado durante busca/impressão, mas o X/Alt+F4 não:
        // fechar ali descartava esta janela, que é o owner do preview/MessageBox que o
        // Form1 abre em seguida (ObjectDisposedException / diálogo mal-parentado).
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_ocupado && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
