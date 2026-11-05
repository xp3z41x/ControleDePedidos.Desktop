using System.ComponentModel;

namespace CONTROLE_DE_PEDIDOS
{
    public class RotasForm : Form
    {
        private readonly DatabaseService _db = DatabaseService.Instancia;
        private DataGridView dgvRotas = null!;
        private TextBox txtNovaRota = null!;
        private Button btnAdicionar = null!;
        private Button btnExcluir = null!;
        private Label lblContagem = null!;
        private SortableBindingList<RotaItem> _rotas = [];
        private bool _ocupado;

        public RotasForm()
        {
            InitializeUI();
            Load += (_, _) => Theme.LimitarAoWorkingArea(this);
            _ = CarregarRotasAsync();
        }

        private void InitializeUI()
        {
            SuspendLayout();

            Text = "Cadastro de Rotas";
            ClientSize = new Size(700, 550);
            MinimumSize = new Size(500, 400);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.BgDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontRegular;
            DoubleBuffered = true;

            // ---- Card principal ----
            var panelCard = new Panel
            {
                Location = new Point(16, 16),
                Size = new Size(ClientSize.Width - 32, ClientSize.Height - 32),
                BackColor = Theme.BgPanel,
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };
            panelCard.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panelCard.Width - 1, panelCard.Height - 1);
            };

            // Header
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.BgCard
            };

            var lblTitulo = new Label
            {
                Text = "Rotas de Entrega",
                Font = Theme.FontTitle,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 14)
            };

            lblContagem = new Label
            {
                Text = "",
                Font = Theme.FontSmall,
                ForeColor = Theme.TextMuted,
                AutoSize = true
            };

            panelHeader.Controls.Add(lblTitulo);
            panelHeader.Controls.Add(lblContagem);
            panelHeader.Resize += (_, _) =>
            {
                lblTitulo.Location = new Point(16, (panelHeader.Height - lblTitulo.Height) / 2);
                lblContagem.Location = new Point(
                    panelHeader.Width - lblContagem.Width - 16,
                    (panelHeader.Height - lblContagem.Height) / 2);
            };
            lblContagem.TextChanged += (_, _) =>
            {
                BeginInvoke(() =>
                {
                    lblContagem.Location = new Point(
                        panelHeader.Width - lblContagem.Width - 16,
                        (panelHeader.Height - lblContagem.Height) / 2);
                });
            };

            // Separador
            var separador = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Theme.Border
            };

            // ---- Barra de adicionar (rodape do card) ----
            var panelAdd = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Theme.BgCard,
                Padding = new Padding(12, 10, 12, 10)
            };
            panelAdd.Paint += (_, e) =>
            {
                using var pen = new Pen(Theme.Border);
                e.Graphics.DrawLine(pen, 0, 0, panelAdd.Width, 0);
            };

            txtNovaRota = new TextBox
            {
                Location = new Point(16, 12),
                Size = new Size(260, 28),
                PlaceholderText = "Nome da nova rota..."
            };
            Theme.EstilizarTextBox(txtNovaRota);
            txtNovaRota.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AdicionarRota();
                }
            };

            btnAdicionar = new Button
            {
                Text = "Adicionar",
                Size = new Size(90, 28)
            };
            Theme.EstilizarBotao(btnAdicionar, primario: true);
            btnAdicionar.Click += (_, _) => AdicionarRota();

            btnExcluir = new Button
            {
                Text = "Excluir",
                Size = new Size(70, 28)
            };
            Theme.EstilizarBotao(btnExcluir);
            btnExcluir.ForeColor = Theme.Error;
            btnExcluir.Click += (_, _) => ExcluirRota();

            panelAdd.Controls.Add(txtNovaRota);
            panelAdd.Controls.Add(btnAdicionar);
            panelAdd.Controls.Add(btnExcluir);

            panelAdd.Resize += (_, _) =>
            {
                btnExcluir.Location = new Point(panelAdd.Width - btnExcluir.Width - 16, (panelAdd.Height - btnExcluir.Height) / 2);
                btnAdicionar.Location = new Point(btnExcluir.Left - btnAdicionar.Width - 8, (panelAdd.Height - btnAdicionar.Height) / 2);
                txtNovaRota.Location = new Point(16, (panelAdd.Height - txtNovaRota.Height) / 2);
                txtNovaRota.Width = btnAdicionar.Left - 8 - 16;
            };

            // ---- Grid ----
            dgvRotas = new OptimizedDataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                ScrollBars = ScrollBars.Vertical
            };
            Theme.EstilizarGrid(dgvRotas, "Rotas_Rotas");

            dgvRotas.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNome",
                HeaderText = "NOME DA ROTA",
                DataPropertyName = "Nome",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Montar card (ordem inversa por Dock)
            panelCard.Controls.Add(dgvRotas);
            panelCard.Controls.Add(panelAdd);
            panelCard.Controls.Add(separador);
            panelCard.Controls.Add(panelHeader);

            Controls.Add(panelCard);

            ResumeLayout(false);
            PerformLayout();
        }

        private async Task CarregarRotasAsync()
        {
            try
            {
                var lista = await _db.ExecutarControleAsync(async conn =>
                {
                    var result = new List<RotaItem>();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT id, nome FROM cadastro_rotas ORDER BY nome";

                    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        result.Add(new RotaItem
                        {
                            Id = reader.GetInt32(0),
                            Nome = reader.GetString(1)
                        });
                    }
                    return result;
                });
                if (IsDisposed) return; // janela fechada durante a carga

                _rotas = new SortableBindingList<RotaItem>(lista ?? []);
                dgvRotas.DataSource = _rotas;
                AtualizarContagem();
            }
            catch (Exception ex)
            {
                Logger.Error("Erro ao carregar rotas", ex);
            }
        }

        private async void AdicionarRota()
        {
            // Enter (com auto-repeat) e clique disparam este async void; sem a trava o
            // segundo disparo durante o await gerava INSERT duplicado -> falso "Já existe".
            if (_ocupado) return;
            var nome = txtNovaRota.Text.Trim();
            if (string.IsNullOrEmpty(nome)) return;

            DefinirOcupado(true);
            try
            {
                // ComLinhas: notifica as outras instâncias (invariante "escreveu, notifique").
                await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO cadastro_rotas (nome) VALUES (@nome)";
                    cmd.Parameters.AddWithValue("nome", nome);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                txtNovaRota.Clear();
                txtNovaRota.Focus();
                await CarregarRotasAsync();
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                MessageBox.Show(this, "Já existe uma rota com esse nome.", "Duplicado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao adicionar rota: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                DefinirOcupado(false);
            }
        }

        private async void ExcluirRota()
        {
            if (_ocupado) return;
            if (dgvRotas.CurrentRow?.DataBoundItem is not RotaItem rota) return;
            var confirm = MessageBox.Show(this,
                $"Excluir a rota \"{rota.Nome}\"?", "Confirmar Exclusão",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            DefinirOcupado(true);
            try
            {
                var linhas = await _db.ExecutarControleComLinhasAsync(async conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM cadastro_rotas WHERE id = @id";
                    cmd.Parameters.AddWithValue("id", rota.Id);
                    return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                });

                if (linhas == 0)
                {
                    MessageBox.Show(this, "Esta rota já foi excluída por outro usuário.\nA tela será atualizada.",
                        "Conflito de Concorrência", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                await CarregarRotasAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao excluir rota: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                DefinirOcupado(false);
            }
        }

        private void DefinirOcupado(bool ocupado)
        {
            _ocupado = ocupado;
            btnAdicionar.Enabled = !ocupado;
            btnExcluir.Enabled = !ocupado;
        }

        // Fechar (X/Esc/Alt+F4) no meio de um INSERT/DELETE descartava o form e o
        // MessageBox(this) seguinte lançava ObjectDisposedException em async void.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_ocupado && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            base.OnFormClosing(e);
        }

        private void AtualizarContagem()
        {
            lblContagem.Text = $"{_rotas.Count} rota{(_rotas.Count != 1 ? "s" : "")}";
        }
    }

    public class RotaItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
    }
}
