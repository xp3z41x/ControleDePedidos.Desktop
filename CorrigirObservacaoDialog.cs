namespace CONTROLE_DE_PEDIDOS
{
    public class CorrigirObservacaoDialog : Form
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;

        private TextBox txtPedido = null!;
        private TextBox txtObservacao = null!;
        private Button btnGravar = null!;
        private Button btnCancelar = null!;
        private bool _gravando;

        public CorrigirObservacaoDialog()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Corrigir Observação (estmov)";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            ClientSize = new Size(380, 240);

            var lblPedido = new Label
            {
                Text = "Pedido (estmov.pedido):",
                Location = new Point(16, 16),
                AutoSize = true,
                ForeColor = Theme.TextPrimary
            };

            txtPedido = new TextBox
            {
                Location = new Point(16, 40),
                Size = new Size(348, 26),
                Font = Theme.FontRegular,
                MaxLength = 9
            };
            Theme.EstilizarTextBox(txtPedido);
            txtPedido.KeyPress += SomenteDigitos;

            var lblObservacao = new Label
            {
                Text = "Nova Observação (estmov.observacao):",
                Location = new Point(16, 78),
                AutoSize = true,
                ForeColor = Theme.TextPrimary
            };

            txtObservacao = new TextBox
            {
                Location = new Point(16, 102),
                Size = new Size(348, 26),
                Font = Theme.FontRegular,
                MaxLength = 9
            };
            Theme.EstilizarTextBox(txtObservacao);
            txtObservacao.KeyPress += SomenteDigitos;

            var lblFilial = new Label
            {
                Text = $"Filial Fiscal alvo: {_db.Config.FilialFiscal}",
                Location = new Point(16, 140),
                AutoSize = true,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };

            var lblAviso = new Label
            {
                Text = "Sobrescreve qualquer valor existente.",
                Location = new Point(16, 160),
                AutoSize = true,
                ForeColor = Theme.TextMuted,
                Font = Theme.FontSmall
            };

            btnCancelar = new Button
            {
                Text = "Cancelar",
                Size = new Size(90, 32),
                Location = new Point(274, 192),
                DialogResult = DialogResult.Cancel
            };
            Theme.EstilizarBotao(btnCancelar);

            btnGravar = new Button
            {
                Text = "Gravar",
                Size = new Size(90, 32),
                Location = new Point(176, 192)
            };
            Theme.EstilizarBotao(btnGravar, primario: true);
            btnGravar.Click += BtnGravar_Click;

            AcceptButton = btnGravar;
            CancelButton = btnCancelar;

            Controls.AddRange([lblPedido, txtPedido, lblObservacao, txtObservacao, lblFilial, lblAviso, btnGravar, btnCancelar]);

            ResumeLayout(false);
            PerformLayout();

            txtPedido.Select();
        }

        private static void SomenteDigitos(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private async void BtnGravar_Click(object? sender, EventArgs e)
        {
            if (!_db.IntegraConectado)
            {
                MessageBox.Show(this, $"Sem conexão com o banco {_db.Config.BancoIntegra}.",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_db.Config.FilialFiscal <= 0)
            {
                MessageBox.Show(this,
                    "Filial Fiscal não configurada. Acesse a Configuração do app para definir.",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!int.TryParse(txtPedido.Text.Trim(), out int pedido) || pedido <= 0)
            {
                MessageBox.Show(this, "Informe um número de pedido válido.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPedido.Focus();
                return;
            }

            // A observação é o nº do orçamento (convenção fiscal usada na correlação de
            // NF-e: estmov.observacao IN ('123', ...)). Grava SEMPRE a forma canônica do
            // número: "000123" passaria no filtro de dígitos mas nunca casaria no IN, e
            // "0" não é orçamento — ambos deixavam a NF-e do pedido invisível.
            var textoObs = txtObservacao.Text.Trim();
            if (!int.TryParse(textoObs, out int orcamentoObs) || orcamentoObs <= 0)
            {
                MessageBox.Show(this, string.IsNullOrEmpty(textoObs)
                        ? "Informe a nova observação."
                        : "Observação inválida: informe um número de orçamento maior que zero.",
                    "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtObservacao.Focus();
                return;
            }
            var observacao = orcamentoObs.ToString();

            var confirmar = MessageBox.Show(this,
                $"Gravar observação '{observacao}' no pedido {pedido} da filial fiscal {_db.Config.FilialFiscal}?\n\nEsta ação sobrescreve qualquer valor atual de observação.",
                "Confirmar Gravação", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmar != DialogResult.Yes) return;

            btnGravar.Enabled = false;
            btnCancelar.Enabled = false;
            _gravando = true;
            try
            {
                int linhas = await _db.AtualizarObservacaoEstmovAsync(pedido, observacao);
                if (linhas > 0)
                {
                    MessageBox.Show(this, $"{linhas} registro(s) atualizado(s).",
                        "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _gravando = false; // libera o OnFormClosing para o Close() abaixo
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(this,
                        $"Nenhum registro encontrado com pedido {pedido} na filial fiscal {_db.Config.FilialFiscal}.",
                        "Nada atualizado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao gravar: {ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _gravando = false;
                btnGravar.Enabled = true;
                btnCancelar.Enabled = true;
            }
        }

        // Durante o UPDATE no Integra o "X"/Alt+F4 ficaria ativo (só os botões são
        // desabilitados): fechar ali descartava o dialog e o MessageBox(this) do
        // resultado estourava ObjectDisposedException num async void — e o operador
        // ficava sem saber se a gravação aconteceu.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_gravando && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
