using System.Data;

namespace NexoApi.Common.Data
{
    public interface IDbConnectionFactory
    {

        IDbConnection CreateConnection();

    }
}
