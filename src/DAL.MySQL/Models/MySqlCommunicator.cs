using MySqlConnector;
using Regira.DAL.Models;

namespace Regira.DAL.MySQL.Models;

public class MySqlCommunicator : DbCommunicator<MySqlConnection>
{
    public MySqlCommunicator(MySqlConnection dbConnection)
        : base(dbConnection)
    {
    }
    public MySqlCommunicator(string connectionString)
        : base(connectionString)
    {
    }
}