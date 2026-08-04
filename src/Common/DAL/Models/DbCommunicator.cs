using System.Data;
using Regira.DAL.Abstractions;

namespace Regira.DAL.Models;

/// <summary>
/// Represents a database communicator that provides functionality for managing database connections 
/// and executing operations using a specific type of database connection.
/// </summary>
/// <typeparam name="TDbConnection">
/// The type of database connection used by this communicator. This type must implement 
/// <see cref="System.Data.IDbConnection"/> and have a parameterless constructor.
/// </typeparam>
public class DbCommunicator<TDbConnection> : IDbCommunicator
    where TDbConnection : class, IDbConnection, new()
{
    private readonly bool _shouldDispose;
    public IDbConnection DbConnection { get; }

    public IDbConnection OpenDbConnection
    {
        get
        {
            if (DbConnection.State != ConnectionState.Open)
            {
                Open();
            }

            return DbConnection;
        }
    }
    public DbCommunicator(TDbConnection dbConnection)
    {
        DbConnection = dbConnection;
    }
    public DbCommunicator(string connectionString)
    {
        _shouldDispose = true;
        DbConnection = new TDbConnection
        {
            ConnectionString = connectionString
        };
    }


    public IDbConnection Open()
    {
        DbConnection.Open();
        return DbConnection;
    }
    public IDbConnection Close()
    {
        DbConnection.Close();
        return DbConnection;
    }


    public void Dispose()
    {
        if (_shouldDispose)
        {
            DbConnection.Dispose();
        }
    }
}