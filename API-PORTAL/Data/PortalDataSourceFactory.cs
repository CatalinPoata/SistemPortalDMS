using API_PORTAL.Entities;
using API_PORTAL.Entities.Base;
using Npgsql;

namespace API_PORTAL.Data
{
    public class PortalDataSourceFactory
    {
        public static NpgsqlDataSource Create(string connectionString)
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);














            return dataSourceBuilder.Build();
        }
    }
}
