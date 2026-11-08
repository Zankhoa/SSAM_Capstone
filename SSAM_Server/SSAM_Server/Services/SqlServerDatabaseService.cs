using System.Data;
using Microsoft.Data.SqlClient;
using SSAM_Server.Interfaces.Services;

namespace SSAM_Server.Services
{
    public class SqlServerDatabaseService : ISqlServerDatabaseService
    {
        private readonly string _connectionString;

        public SqlServerDatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<string> GetDatabaseSchemaAsync()
        {
            // Provide the schema so Gemini knows what tables/columns exist
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var schemaSql = @"
                SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE 
                FROM INFORMATION_SCHEMA.COLUMNS 
                ORDER BY TABLE_NAME, ORDINAL_POSITION";

            using var command = new SqlCommand(schemaSql, connection);
            using var reader = await command.ExecuteReaderAsync();

            var schemaText = new System.Text.StringBuilder();
            while (await reader.ReadAsync())
            {
                schemaText.AppendLine($"Table: {reader["TABLE_NAME"]}, Column: {reader["COLUMN_NAME"]}, Type: {reader["DATA_TYPE"]}");
            }

            return schemaText.ToString();
        }

        public async Task<string> ExecuteReadOnlyQueryAsync(string sqlQuery)
        {
            // Basic safety check: Ensure query is strictly read-only
            var cleaned = sqlQuery.Trim().ToLowerInvariant();
            if (!cleaned.StartsWith("select") || cleaned.Contains("insert") || cleaned.Contains("update") || cleaned.Contains("delete") || cleaned.Contains("drop"))
            {
                return "Error: Only SELECT queries are permitted.";
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand(sqlQuery, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            var dt = new DataTable();
            dt.Load(reader);

            // Convert data table to JSON string for Gemini to digest easily
            return System.Text.Json.JsonSerializer.Serialize(
                dt.Rows.Cast<DataRow>().Select(row => dt.Columns.Cast<DataColumn>().ToDictionary(col => col.ColumnName, col => row[col]))
            );
        }
    }
}