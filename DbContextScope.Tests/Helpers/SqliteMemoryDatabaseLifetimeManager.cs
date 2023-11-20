using Microsoft.Data.Sqlite;
using System;
using System.Data.Common;

namespace Zejji.Tests.Helpers
{
    /// <summary>
    /// This class is responsible for keeping an in-memory SQLite database
    /// alive for the lifetime of an instance. This is required because SQLite
    /// in-memory databases cease to exist as soon as the last database
    /// connection is closed.
    ///
    /// See <a href="https://www.sqlite.org/inmemorydb.html">this link</a> for more information.
    ///
    /// An instance of this class instantiates a SQLite database upon creation
    /// and disposes of it when it is itself disposed.
    /// </summary>
    internal sealed class SqliteMemoryDatabaseLifetimeManager : IDisposable
    {
        public readonly string ConnectionString =
            $"DataSource={Guid.NewGuid()};mode=memory;cache=shared";

        private DbConnection? _keepAliveConnection;

        public SqliteMemoryDatabaseLifetimeManager()
        {
            _keepAliveConnection = new SqliteConnection(ConnectionString);
            _keepAliveConnection.Open();
        }

        public void Dispose() // see https://rules.sonarsource.com/csharp/RSPEC-3881
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_keepAliveConnection == null)
                return;

            _keepAliveConnection.Dispose();
            _keepAliveConnection = null;
        }
    }
}
