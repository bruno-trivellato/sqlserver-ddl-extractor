using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DDLExtractor.Properties;    // Para Settings
using DDLExtractor.Services;
using Timer = System.Windows.Forms.Timer;     // Para IDdlExtractorService

namespace DDLExtractor
{
    public partial class MainForm : Form
    {
        private IDdlExtractorService? _ddlService;

        // Timer que mostra “Salvo” por 2s após editar config
        private readonly Timer _saveTimer;

        // Stopwatch para medir tempo da extração
        private readonly Stopwatch _ddlStopwatch;

        // Timer para atualizar labelElapsed em tempo real
        private readonly Timer _ddlTimer;

        public MainForm()
        {
            InitializeComponent();

            // 1) Timer para exibir “Salvo” por 2 segundos
            _saveTimer = new Timer { Interval = 2000 };
            _saveTimer.Tick += (s, e) =>
            {
                labelSaved.Visible = false;
                _saveTimer.Stop();
            };

            // 2) Stopwatch que mede tempo de extração
            _ddlStopwatch = new Stopwatch();

            // 3) Timer que atualiza a labelElapsed a cada 100 ms
            _ddlTimer = new Timer { Interval = 100 };
            _ddlTimer.Tick += (s, e) =>
            {
                labelElapsed.Text = FormatTime(_ddlStopwatch.Elapsed);
            };

            // 4) Eventos “Leave” para salvar config
            txtConnectionString.Leave += (s, e) => SaveSettings();
            txtSchema.Leave += (s, e) => SaveSettings();
        }

        // Este método é chamado ao abrir o Form
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Carregar config
            txtConnectionString.Text = Settings.Default.DefaultConnection;
            txtSchema.Text           = Settings.Default.TableSchema;
        }

        /// <summary>
        /// Salva as configurações e exibe “Salvo” por 2s.
        /// </summary>
        private void SaveSettings()
        {
            Settings.Default.DefaultConnection = txtConnectionString.Text;
            Settings.Default.TableSchema       = txtSchema.Text;
            Settings.Default.Save();

            labelSaved.Visible = true;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>
        /// Handler do botão “Load Tables”. Carrega a lista (filtra se houver schema).
        /// </summary>
        private async void buttonLoadTables_Click(object sender, EventArgs e)
        {
            ToggleUi(false);
            try
            {
                // Instancia o serviço com a connection string atual
                _ddlService = new DdlExtractorService(txtConnectionString.Text);

                // Busca as tabelas do schema (se txtSchema vazio => todas)
                var tables = await _ddlService.GetTableNamesAsync(txtSchema.Text);
                var list = tables.ToList();

                // Exibe no CheckedListBox
                checkedListBoxTabelas.DataSource = new BindingList<string>(list);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar tabelas:\n{ex.Message}",
                                "Erro",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
            finally
            {
                ToggleUi(true);
            }
        }

        /// <summary>
        /// Handler do botão “Extrair DDL”. Faz a extração em lote, cronometra, exibe resultado no textBox.
        /// </summary>
        private async void buttonExtrair_Click(object sender, EventArgs e)
        {
            if (_ddlService == null)
            {
                MessageBox.Show("Por favor carregue as tabelas primeiro.",
                                "Aviso",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return;
            }

            var selected = checkedListBoxTabelas.CheckedItems.Cast<string>().ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Selecione ao menos uma tabela.",
                                "Aviso",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return;
            }

            ToggleUi(false);
            try
            {
                // Inicia cronômetro e Timer
                _ddlStopwatch.Restart();
                labelElapsed.Text = FormatTime(_ddlStopwatch.Elapsed);
                labelElapsed.Visible = true;
                _ddlTimer.Start();

                // Extrai DDL
                var ddl = await _ddlService.GetDdlAsync(selected);
                textBoxResultado.Text = ddl;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao extrair DDL:\n{ex.Message}",
                                "Erro",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
            finally
            {
                // Para contagem e define label final
                _ddlTimer.Stop();
                _ddlStopwatch.Stop();

                // Exibe o tempo final
                labelElapsed.Text = FormatTime(_ddlStopwatch.Elapsed) + " (final)";

                ToggleUi(true);
            }
        }

        /// <summary>
        /// Função que formata o TimeSpan para mm:ss.mmm
        /// </summary>
        private string FormatTime(TimeSpan ts)
        {
            // exibe mm:ss.mmm
            // se passar de 60 min, exibirá por ex. 63:05.128
            int totalMin = (int)ts.TotalMinutes;
            int secs = ts.Seconds;
            int ms   = ts.Milliseconds;
            return $"{totalMin}:{secs:D2}.{ms:D3} seg";
        }

        /// <summary>
        /// Marca / desmarca todos os itens quando o usuário marca / desmarca “Selecionar Tudo”.
        /// </summary>
        private void checkBoxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            bool checkAll = checkBoxSelectAll.Checked;
            for (int i = 0; i < checkedListBoxTabelas.Items.Count; i++)
            {
                checkedListBoxTabelas.SetItemChecked(i, checkAll);
            }
        }

        /// <summary>
        /// Se quiser desmarcar o “Selecionar Tudo” quando o usuário manualmente desmarca algo:
        /// Basta descomentar o evento em Designer e implementar algo assim:
        /// </summary>
        /*
        private void checkedListBoxTabelas_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (e.NewValue == CheckState.Unchecked)
            {
                // Desanexa para evitar loop
                checkBoxSelectAll.CheckedChanged -= checkBoxSelectAll_CheckedChanged;
                checkBoxSelectAll.Checked = false;
                checkBoxSelectAll.CheckedChanged += checkBoxSelectAll_CheckedChanged;
            }
        }
        */

        /// <summary>
        /// Ativa / desativa todos os controles. Exibe labelLoading quando desativado.
        /// </summary>
        private void ToggleUi(bool enabled)
        {
            txtConnectionString.Enabled  = enabled;
            txtSchema.Enabled            = enabled;
            buttonLoadTables.Enabled     = enabled;
            checkBoxSelectAll.Enabled    = enabled;
            checkedListBoxTabelas.Enabled = enabled;
            buttonExtrair.Enabled        = enabled;
            labelLoading.Visible         = !enabled;
        }
    }
}
