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

            btnGravar.Enabled = false;
            btnCancelar.Enabled = false;
            _gravando = true;
            try
            {
                // Prévia somente leitura (conexão read-only do Integra) com os MESMOS
                // filtros do UPDATE: o operador vê o que vai sobrescrever e quantas
                // linhas do estmov serão tocadas antes de confirmar.
                var previa = await ConsultarPreviaAsync(pedido);
                int totalPrevisto = previa.Sum(p => p.Linhas);
                if (totalPrevisto == 0)
                {
                    MessageBox.Show(this,
                        $"Nenhum registro do estmov encontrado com pedido {pedido} na filial fiscal {_db.Config.FilialFiscal}.\n\nNada foi gravado.",
                        "Nada a alterar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var atuais = string.Join("\n", previa.Take(MaxValoresPrevia).Select(p =>
                    $"   • {(p.Observacao.Length == 0 ? "(vazia)" : $"'{p.Observacao}'")} — {p.Linhas} linha(s)"));
                if (previa.Count > MaxValoresPrevia)
                    atuais += $"\n   • … e mais {previa.Count - MaxValoresPrevia} valor(es) diferente(s)";

                var confirmar = MessageBox.Show(this,
                    $"Pedido {pedido} — filial fiscal {_db.Config.FilialFiscal}\n\n" +
                    $"Observação atual:\n{atuais}\n\n" +
                    $"Nova observação: '{observacao}'\n" +
                    $"Linhas do estmov que serão alteradas: {totalPrevisto}\n\n" +
                    "Esta ação sobrescreve o valor atual. Confirmar a gravação?",
                    "Confirmar Gravação", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (confirmar != DialogResult.Yes) return;

                int linhas = await _db.AtualizarObservacaoEstmovAsync(pedido, observacao);
                if (linhas > 0 && linhas != totalPrevisto)
                    Logger.Warn($"Corrigir Observação: prévia contou {totalPrevisto} linha(s) do pedido {pedido}, UPDATE alterou {linhas}");
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

        private const int MaxValoresPrevia = 5;

        private sealed record ValorObservacao(string Observacao, int Linhas);

        /// <summary>
        /// Valores atuais de estmov.observacao (agrupados) com os mesmos filtros de
        /// <see cref="DatabaseService.AtualizarObservacaoEstmovAsync"/>. Roda na conexão
        /// somente leitura do Integra — sem efeito colateral.
        /// </summary>
        private async Task<List<ValorObservacao>> ConsultarPreviaAsync(int pedido)
        {
            int filial = _db.Config.FilialFiscal;
            var lista = await _db.ConsultarIntegraAsync(async conn =>
            {
                var result = new List<ValorObservacao>();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT COALESCE(TRIM(observacao::text), '') AS obs, COUNT(*)::int
                      FROM estmov
                     WHERE filial = @filial
                       AND pedido = @pedido
                     GROUP BY 1
                     ORDER BY 2 DESC, 1";
                cmd.Parameters.AddWithValue("filial", filial);
                cmd.Parameters.AddWithValue("pedido", pedido);
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                    result.Add(new ValorObservacao(reader.GetString(0), reader.GetInt32(1)));
                return result;
            });
            return lista ?? [];
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
