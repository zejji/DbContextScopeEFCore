using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Zejji.Entity
{
    /// <summary>
    /// As its name suggests, <see cref="DbContextCollection"/> maintains a collection of <see cref="DbContext"/> instances.
    ///
    /// What it does in a nutshell:
    /// - Lazily instantiates <see cref="DbContext"/> instances when its <see cref="DbContextCollection.Get{TDbContext}()"/> method is called
    /// (and optionally starts an explicit database transaction).
    /// - Keeps track of the <see cref="DbContext"/> instances it created so that it can return the existing
    /// instance when asked for a <see cref="DbContext"/> of a specific type.
    /// - Takes care of committing / rolling back changes and transactions on all the <see cref="DbContext"/>
    /// instances it created when its <see cref="DbContextCollection.Commit()"/> or <see cref="DbContextCollection.Rollback()"/> method is called.
    ///
    /// </summary>
    public class DbContextCollection(
        bool readOnly = false,
        IsolationLevel? isolationLevel = null,
        IDbContextFactory? dbContextFactory = null
    ) : IDbContextCollection
    {
        private readonly Dictionary<Type, DbContext> _initializedDbContexts = new();
        private readonly Dictionary<DbContext, IDbContextTransaction> _transactions = new();
        private bool _disposed = false;
        private bool _completed = false;

        internal Dictionary<Type, DbContext> InitializedDbContexts => _initializedDbContexts;

        public TDbContext Get<TDbContext>()
            where TDbContext : DbContext
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextCollection));

            var requestedType = typeof(TDbContext);

            if (!_initializedDbContexts.ContainsKey(requestedType))
            {
                // First time we've been asked for this particular DbContext type.
                // Create one, cache it and start its database transaction if needed.
                TDbContext dbContext =
                    dbContextFactory != null
                        ? dbContextFactory.CreateDbContext<TDbContext>()
                        : Activator.CreateInstance<TDbContext>();

                _initializedDbContexts.Add(requestedType, dbContext);

                if (readOnly)
                {
                    dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                }

                if (isolationLevel.HasValue)
                {
                    var tran = dbContext.Database.BeginTransaction(isolationLevel.Value);
                    _transactions.Add(dbContext, tran);
                }
            }

            return (TDbContext)_initializedDbContexts[requestedType];
        }

        public int Commit()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextCollection));
            if (_completed)
                throw new InvalidOperationException(
                    $"You can't call {nameof(Commit)}() or {nameof(Rollback)}() more than once on a {nameof(DbContextCollection)}. All the changes in the {nameof(DbContext)} instances managed by this collection have already been saved or rolled back and all database transactions have been completed and closed. If you wish to make more data changes, create a new {nameof(DbContextCollection)} and make your changes there."
                );

            // Best effort. You'll note that we're not actually implementing an atomic commit
            // here. It entirely possible that one DbContext instance will be committed successfully
            // and another will fail. Implementing an atomic commit would require us to wrap
            // all of this in a TransactionScope. The problem with TransactionScope is that
            // the database transaction it creates may be automatically promoted to a
            // distributed transaction if our DbContext instances happen to be using different
            // databases. And that would require the DTC service (Distributed Transaction Coordinator)
            // to be enabled on all of our live and dev servers as well as on all of our dev workstations.
            // Otherwise the whole thing would blow up at runtime.

            // In practice, if our services are implemented following a reasonably DDD approach,
            // a business transaction (i.e. a service method) should only modify entities in a single
            // DbContext. So we should never find ourselves in a situation where two DbContext instances
            // contain uncommitted changes here. We should therefore never be in a situation where the below
            // would result in a partial commit.

            ExceptionDispatchInfo? lastError = null;

            var c = 0;

            foreach (var dbContext in _initializedDbContexts.Values)
            {
                try
                {
                    if (!readOnly)
                    {
                        c += dbContext.SaveChanges();
                    }

                    // If we've started an explicit database transaction, time to commit it now.
                    var tran = GetValueOrDefault(_transactions, dbContext);
                    if (tran != null)
                    {
                        tran.Commit();
                        tran.Dispose();
                        _transactions.Remove(dbContext);
                    }
                }
                catch (Exception e)
                {
                    lastError = ExceptionDispatchInfo.Capture(e);
                }
            }

            if (lastError != null)
                lastError.Throw(); // Re-throw while maintaining the exception's original stack track
            else
                _completed = true;

            return c;
        }

        public Task<int> CommitAsync()
        {
            return CommitAsync(CancellationToken.None);
        }

        public async Task<int> CommitAsync(CancellationToken cancelToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextCollection));
            if (_completed)
                throw new InvalidOperationException(
                    $"You can't call {nameof(Commit)}() or {nameof(Rollback)}() more than once on a {nameof(DbContextCollection)}. All the changes in the {nameof(DbContext)} instances managed by this collection have already been saved or rolled back and all database transactions have been completed and closed. If you wish to make more data changes, create a new {nameof(DbContextCollection)} and make your changes there."
                );

            // See comments in the sync version of this method for more details.
            ExceptionDispatchInfo? lastError = null;

            var c = 0;

            foreach (var dbContext in _initializedDbContexts.Values)
            {
                try
                {
                    if (!readOnly)
                    {
                        c += await dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                    }

                    // If we've started an explicit database transaction, time to commit it now.
                    var tran = GetValueOrDefault(_transactions, dbContext);
                    if (tran != null)
                    {
                        tran.Commit();
                        tran.Dispose();
                        _transactions.Remove(dbContext);
                    }
                }
                catch (Exception e)
                {
                    lastError = ExceptionDispatchInfo.Capture(e);
                }
            }

            if (lastError != null)
                lastError.Throw(); // Re-throw while maintaining the exception's original stack track
            else
                _completed = true;

            return c;
        }

        public void Rollback()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextCollection));
            if (_completed)
                throw new InvalidOperationException(
                    $"You can't call {nameof(Commit)}() or {nameof(Rollback)}() more than once on a {nameof(DbContextCollection)}. All the changes in the {nameof(DbContext)} instances managed by this collection have already been saved or rolled back and all database transactions have been completed and closed. If you wish to make more data changes, create a new {nameof(DbContextCollection)} and make your changes there."
                );

            ExceptionDispatchInfo? lastError = null;

            foreach (var dbContext in _initializedDbContexts.Values)
            {
                // There's no need to explicitly rollback changes in a DbContext as
                // DbContext doesn't save any changes until its SaveChanges() method is called.
                // So "rolling back" for a DbContext simply means not calling its SaveChanges()
                // method.

                // But if we've started an explicit database transaction, then we must roll it back.
                var tran = GetValueOrDefault(_transactions, dbContext);
                if (tran != null)
                {
                    try
                    {
                        tran.Rollback();
                        tran.Dispose();
                    }
                    catch (Exception e)
                    {
                        lastError = ExceptionDispatchInfo.Capture(e);
                    }
                }
            }

            _transactions.Clear();
            _completed = true;

            if (lastError != null)
                lastError.Throw(); // Re-throw while maintaining the exception's original stack track
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Do our best here to dispose as much as we can even if we get errors along the way.
            // Now is not the time to throw. Correctly implemented applications will have called
            // either Commit() or Rollback() first and would have got the error there.

            if (!_completed)
            {
                try
                {
                    if (readOnly)
                        Commit();
                    else
                        Rollback();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }

            foreach (var dbContext in _initializedDbContexts.Values)
            {
                try
                {
                    dbContext.Dispose();
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }

            _initializedDbContexts.Clear();
            _disposed = true;
        }

        /// <summary>
        /// Returns the value associated with the specified key or the default
        /// value for the <typeparamref name="TValue"/> type.
        /// </summary>
        /// <typeparam name="TKey">The type of the lookup key.</typeparam>
        /// <typeparam name="TValue">The type of the value stored in the dictionary.</typeparam>
        private static TValue? GetValueOrDefault<TKey, TValue>(
            Dictionary<TKey, TValue> dictionary,
            TKey key
        )
            where TKey : notnull
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }
    }
}
