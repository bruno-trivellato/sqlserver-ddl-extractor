using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;

namespace DDLExtractor
{
    public partial class MainForm : Form
    {
        // Dados de conexão – substitua pelos valores do seu ambiente
        private string serverName = "liladbserver2.database.windows.net";
        private string databaseName = "lila";
        private string userName = "lilaadm";
        private string password = "jm3ZS1Sc1tPlz5pw5Y8BWdAmfeMZgbyl";

        // Campos (nullable) para armazenar conexão e banco
        private Server? sqlServer;
        private Database? sqlDatabase;

        public MainForm()
        {
            InitializeComponent();
            // Inicia o carregamento dos dados de conexão e das tabelas em background
            LoadDataAsync();
        }

        /// <summary>
        /// Método assíncrono para conectar e carregar as tabelas sem travar a UI.
        /// </summary>
        private async void LoadDataAsync()
        {
            ShowLoading(true);
            await Task.Run(() => Conectar());
            await Task.Run(() => CarregarTabelas());
            ShowLoading(false);
        }

        /// <summary>
        /// Exibe ou oculta o indicador de loading.
        /// </summary>
        private void ShowLoading(bool show)
        {
            if (labelLoading.InvokeRequired)
            {
                labelLoading.Invoke(new Action(() => labelLoading.Visible = show));
            }
            else
            {
                labelLoading.Visible = show;
            }
        }

        /// <summary>
        /// Conecta ao SQL Server e seleciona o banco de dados.
        /// </summary>
        private void Conectar()
        {
            try
            {
                ServerConnection connection = new ServerConnection(serverName, userName, password);
                sqlServer = new Server(connection);
                sqlDatabase = sqlServer.Databases[databaseName];
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao conectar: " + ex.Message);
            }
        }

        /// <summary>
        /// Carrega as tabelas (exceto as de sistema) na CheckedListBox.
        /// </summary>
        private void CarregarTabelas()
        {
            if (sqlDatabase == null)
            {
                MessageBox.Show("Banco de dados não inicializado.");
                return;
            }

            // Atualiza a CheckedListBox na thread da UI
            if (checkedListBoxTabelas.InvokeRequired)
            {
                checkedListBoxTabelas.Invoke(new Action(() =>
                {
                    checkedListBoxTabelas.Items.Clear();
                    foreach (Table tabela in sqlDatabase.Tables)
                    {
                        if (!tabela.IsSystemObject && string.Equals(tabela.Schema, "dbo", StringComparison.OrdinalIgnoreCase))
                            checkedListBoxTabelas.Items.Add(tabela.Schema + "." + tabela.Name);
                    }
                }));
            }
            else
            {
                checkedListBoxTabelas.Items.Clear();
                foreach (Table tabela in sqlDatabase.Tables)
                {
                    if (!tabela.IsSystemObject && string.Equals(tabela.Schema, "dbo", StringComparison.OrdinalIgnoreCase))
                        checkedListBoxTabelas.Items.Add(tabela.Schema + "." + tabela.Name);
                }
            }
        }


        /// <summary>
        /// Extrai os DDLs das tabelas selecionadas de forma assíncrona.
        /// </summary>
        private async void buttonExtrair_Click(object sender, EventArgs e)
        {
            if (sqlServer == null || sqlDatabase == null)
            {
                MessageBox.Show("Conexão não estabelecida.");
                return;
            }

            ShowLoading(true);
            string ddlCompleto = "";
            await Task.Run(() =>
            {
                foreach (var item in checkedListBoxTabelas.CheckedItems)
                {
                    string[] partes = item.ToString().Split('.');
                    if (partes.Length == 2)
                    {
                        string schema = partes[0];
                        string tableName = partes[1];
                        Table tabela = sqlDatabase.Tables[tableName, schema];
                        if (tabela != null && tabela.Urn != null)
                        {
                            Scripter scripter = new Scripter(sqlServer)
                            {
                                Options =
                                {
                                    ScriptDrops = false,
                                    WithDependencies = false,
                                    Indexes = true,
                                    DriAll = true,
                                    Triggers = true,
                                    IncludeHeaders = false,
                                    AnsiPadding = false
                                }
                            };

                            var scripts = scripter.Script(new Urn[] { tabela.Urn });
                            ddlCompleto += string.Join(Environment.NewLine, scripts.Cast<string>()) + Environment.NewLine + Environment.NewLine;
                        }
                    }
                }
                // Processa o DDL para remover linhas com SET ANSI_NULLS e SET QUOTED_IDENTIFIER
                ddlCompleto = string.Join(Environment.NewLine,
                    ddlCompleto.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                               .Where(line => !line.Trim().StartsWith("SET ANSI_NULLS", StringComparison.OrdinalIgnoreCase)
                                           && !line.Trim().StartsWith("SET QUOTED_IDENTIFIER", StringComparison.OrdinalIgnoreCase)));
            });
            textBoxResultado.Text = ddlCompleto;
            ShowLoading(false);
        }
    }
}
