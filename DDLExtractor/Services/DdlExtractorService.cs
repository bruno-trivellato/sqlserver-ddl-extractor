using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo;

namespace DDLExtractor.Services
{
    public interface IDdlExtractorService
    {
        /// <summary>
        /// Retorna os nomes de tabelas no formato "SchemaName.TableName".
        /// Se schemaFilter não for nulo/vazio, filtra por esse schema.
        /// </summary>
        Task<IEnumerable<string>> GetTableNamesAsync(string? schemaFilter = null);

        /// <summary>
        /// Extrai o DDL de cada tabela na lista "Schema.TableName" (batch).
        /// </summary>
        Task<string> GetDdlAsync(IEnumerable<string> tableFullNames);
    }

    public class DdlExtractorService : IDdlExtractorService
    {
        private readonly string _connectionString;

        // Expressões para remover partes desnecessárias do script final
        private static readonly Regex[] CleanupRegexes =
        {
            new Regex(@"(?i)WITH\s*\(.*?\)\s*ON\s*\[.*?\]", RegexOptions.Singleline),
            new Regex(@"(?i)TEXTIMAGE_ON\s*\[.*?\]", RegexOptions.Singleline),
            new Regex(@"(?i)COLLATE\s+\w+", RegexOptions.Singleline),
            new Regex(@"(?i)ON\s+\[PRIMARY\]", RegexOptions.Singleline),
            new Regex(@"(?i)ALTER\s+TABLE.*?CHECK\s+CONSTRAINT.*?;", RegexOptions.Singleline),
            new Regex(@"(?i)ALTER\s+TABLE.*?ADD\s+DEFAULT\s+\(.*?\)\s+FOR\s+\[.*?\];", RegexOptions.Singleline),
            new Regex(@"\r?\n\s*\r?\n", RegexOptions.Singleline)
        };

        public DdlExtractorService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Carrega a lista de tabelas por T-SQL (INFORMATION_SCHEMA.TABLES).
        /// Aplica um simples filtro de schema no WHERE, se schemaFilter não for vazio.
        /// </summary>
        public async Task<IEnumerable<string>> GetTableNamesAsync(string? schemaFilter = null)
        {
            var tables = new List<string>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            var sql = new StringBuilder(@"
                SELECT TABLE_SCHEMA, TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                  AND TABLE_SCHEMA NOT IN ('sys','INFORMATION_SCHEMA')
            ");

            if (!string.IsNullOrEmpty(schemaFilter))
            {
                sql.Append(" AND TABLE_SCHEMA = @SchemaFilter");
            }

            sql.Append(" ORDER BY TABLE_SCHEMA, TABLE_NAME;");

            using var cmd = new SqlCommand(sql.ToString(), conn);
            if (!string.IsNullOrEmpty(schemaFilter))
            {
                cmd.Parameters.AddWithValue("@SchemaFilter", schemaFilter);
            }

            using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var schema = reader.GetString(0);
                var table  = reader.GetString(1);
                tables.Add($"{schema}.{table}");
            }

            return tables;
        }

        /// <summary>
        /// Extrai DDL das tabelas, fazendo uma só chamada Script() para todos os URNs coletados.
        /// </summary>
        public async Task<string> GetDdlAsync(IEnumerable<string> tableFullNames)
        {
            return await Task.Run(() =>
            {
                // 1) Criar a conexão SMO
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var serverConn = new ServerConnection(builder.DataSource, builder.UserID, builder.Password);
                var server = new Server(serverConn);
                var database = server.Databases[builder.InitialCatalog]
                    ?? throw new InvalidOperationException($"Banco '{builder.InitialCatalog}' não encontrado.");

                // 2) Colecionar URNs das tabelas
                var urns = new List<Urn>();
                foreach (var fullName in tableFullNames)
                {
                    var parts = fullName.Split('.', 2);
                    if (parts.Length == 2)
                    {
                        var schema = parts[0];
                        var tableName = parts[1];

                        var tableObj = database.Tables[tableName, schema];
                        if (tableObj?.Urn != null)
                        {
                            urns.Add(tableObj.Urn);
                        }
                    }
                }

                if (urns.Count == 0) return string.Empty;

                // 3) Configurar Scripter
                var scripter = new Scripter(server)
                {
                    Options =
                    {
                        ScriptDrops = false,
                        WithDependencies = false,
                        Indexes = false,
                        DriAll = true,
                        Triggers = false,
                        IncludeHeaders = false,
                        AnsiPadding = false,
                        NoCollation = true,
                        SchemaQualify = false,
                        SchemaQualifyForeignKeysReferences = false,
                        ScriptBatchTerminator = false
                    }
                };

                // 4) Executar Script() em lote
                var allScriptLines = scripter.Script(urns.ToArray()).Cast<string>();

                // 5) Construir texto final e remover linhas com SET ANSI_NULLS e SET QUOTED_IDENTIFIER
                var sb = new StringBuilder();
                foreach (var line in allScriptLines)
                {
                    var trimmed = line.TrimStart();
                    if (!trimmed.StartsWith("SET ANSI_NULLS", StringComparison.OrdinalIgnoreCase)
                     && !trimmed.StartsWith("SET QUOTED_IDENTIFIER", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine(line);
                    }
                }
                var result = sb.ToString();

                // 6) Limpar com as Regex
                foreach (var regex in CleanupRegexes)
                {
                    result = regex.Replace(result, string.Empty);
                }

                return result.Trim();
            });
        }
    }
}
