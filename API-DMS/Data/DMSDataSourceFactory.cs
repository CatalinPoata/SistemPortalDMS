using API_DMS.Entities;
using API_DMS.Entities.Base;
using Npgsql;

namespace API_DMS.Data
{
    public static class DmsDataSourceFactory
    {
        public static NpgsqlDataSource Create(string connectionString)
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);









            return dataSourceBuilder.Build();
        }
    }
}
