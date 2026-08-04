using System.Data;

namespace Regira.DAL.Abstractions;

/// <summary>
/// Defines an abstraction for a database communicator that manages database connections 
/// and provides methods for opening and closing connections.
/// </summary>
/// <remarks>
/// This interface is intended to be implemented by classes that encapsulate the logic 
/// for interacting with a database, ensuring proper connection management and resource disposal.
/// </remarks>
public interface IDbCommunicator : IDisposable
{
    IDbConnection DbConnection { get; }
    IDbConnection OpenDbConnection { get; }

    IDbConnection Open();
    IDbConnection Close();
}