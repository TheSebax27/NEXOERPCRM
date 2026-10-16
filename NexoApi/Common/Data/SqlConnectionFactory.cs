using System.Data;
using Microsoft.Data.SqlClient;

namespace NexoApi.Common.Data;

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NexoDb")
            ?? throw new InvalidOperationException("No se encontro la cadena de conexion 'NexoDb' en appsettings.json");
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}