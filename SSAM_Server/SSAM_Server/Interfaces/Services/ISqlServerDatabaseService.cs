namespace SSAM_Server.Interfaces.Services
{
    public interface ISqlServerDatabaseService
    {
        Task<string> GetDatabaseSchemaAsync();
        Task<string> ExecuteReadOnlyQueryAsync(string sqlQuery);
    }
}