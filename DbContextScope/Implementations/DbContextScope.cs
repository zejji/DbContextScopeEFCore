using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections;
using System.Data;
using System.Runtime.CompilerServices;

namespace Zejji.Entity
{
    public sealed class DbContextScope : IDbContextScope
    {
        private bool _disposed;
        private bool _readOnly;
        private bool _completed;
        private bool _nested;
        private DbContextScope? _parentScope;
        private DbContextCollection _dbContexts;

        public IDbContextCollection DbContexts { get { return _dbContexts; } }

        public DbContextScope(IDbContextFactory? dbContextFactory = null) :
            this(joiningOption: DbContextScopeOption.JoinExisting, readOnly: false, isolationLevel: null, dbContextFactory: dbContextFactory)
        { }

        public DbContextScope(bool readOnly, IDbContextFactory? dbContextFactory = null)
            : this(joiningOption: DbContextScopeOption.JoinExisting, readOnly: readOnly, isolationLevel: null, dbContextFactory: dbContextFactory)
        { }

        public DbContextScope(DbContextScopeOption joiningOption, bool readOnly, IsolationLevel? isolationLevel, IDbContextFactory? dbContextFactory = null)
        {
            if (isolationLevel.HasValue && joiningOption == DbContextScopeOption.JoinExisting)
                throw new ArgumentException($"Cannot join an ambient {nameof(DbContextScope)} when an explicit database transaction is required. When requiring explicit database transactions to be used (i.e. when the '{nameof(isolationLevel)}' parameter is set), you must not also ask to join the ambient context.");

            _disposed = false;
            _completed = false;
            _readOnly = readOnly;

            _parentScope = GetAmbientScope();
            if (_parentScope != null && joiningOption == DbContextScopeOption.JoinExisting)
            {
                if (_parentScope._readOnly && !this._readOnly)
                {
                    throw new InvalidOperationException($"Cannot nest a read/write {nameof(DbContextScope)} within a read-only {nameof(DbContextScope)}.");
                }

                _nested = true;
                _dbContexts = _parentScope._dbContexts;
            }
            else
            {
                _nested = false;
                _dbContexts = new DbContextCollection(readOnly, isolationLevel, dbContextFactory);
            }

            SetAmbientScope(this);
        }

        public int SaveChanges()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextScope));
            if (_completed)
                throw new InvalidOperationException($"You cannot call {nameof(SaveChanges)}() more than once on a {nameof(DbContextScope)}. A {nameof(DbContextScope)} is meant to encapsulate a business transaction: create the scope at the start of the business transaction and then call {nameof(SaveChanges)}() at the end. Calling {nameof(SaveChanges)}() mid-way through a business transaction doesn't make sense and most likely mean that you should refactor your service method into two separate service method that each create their own {nameof(DbContextScope)} and each implement a single business transaction.");

            // Only save changes if we're not a nested scope. Otherwise, let the top-level scope 
            // decide when the changes should be saved.
            var c = 0;
            if (!_nested)
            {
                c = CommitInternal();
            }

            _completed = true;

            return c;
        }

        public Task<int> SaveChangesAsync()
        {
            return SaveChangesAsync(CancellationToken.None);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancelToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbContextScope));
            if (_completed)
                throw new InvalidOperationException($"You cannot call {nameof(SaveChanges)}() more than once on a {nameof(DbContextScope)}. A {nameof(DbContextScope)} is meant to encapsulate a business transaction: create the scope at the start of the business transaction and then call {nameof(SaveChanges)}() at the end. Calling {nameof(SaveChanges)}() mid-way through a business transaction doesn't make sense and most likely mean that you should refactor your service method into two separate service method that each create their own {nameof(DbContextScope)} and each implement a single business transaction.");

            // Only save changes if we're not a nested scope. Otherwise, let the top-level scope 
            // decide when the changes should be saved.
            var c = 0;
            if (!_nested)
            {
                c = await CommitInternalAsync(cancelToken).ConfigureAwait(false);
            }

            _completed = true;
            return c;
        }

        private int CommitInternal()
        {
            return _dbContexts.Commit();
        }

        private Task<int> CommitInternalAsync(CancellationToken cancelToken)
        {
            return _dbContexts.CommitAsync(cancelToken);
        }

        private void RollbackInternal()
        {
            _dbContexts.Rollback();
        }

        public void RefreshEntitiesInParentScope(IEnumerable entities)
        {
            if (entities == null)
                return;

            if (_parentScope == null)
                return;

            if (_nested) // The parent scope uses the same DbContext instances as we do - no need to refresh anything
                return;

            foreach (var entityEntry in GetEntitiesInParentScopeToRefreshInternal(entities))
            {
                entityEntry.Reload();
            }
        }

        public async Task RefreshEntitiesInParentScopeAsync(IEnumerable entities)
        {
            // See comments in the sync version of this method for an explanation of what we're doing here.

            if (entities == null)
                return;

            if (_parentScope == null)
                return;

            if (_nested)
                return;

            foreach (var entityEntry in GetEntitiesInParentScopeToRefreshInternal(entities))
            {
                await entityEntry.ReloadAsync().ConfigureAwait(false);
            }
        }

        private IEnumerable<EntityEntry> GetEntitiesInParentScopeToRefreshInternal(IEnumerable entities)
        {
            foreach (var contextInCurrentScope in _dbContexts.InitializedDbContexts.Values)
            {
                var correspondingParentContext =
                    _parentScope?._dbContexts.InitializedDbContexts.Values.SingleOrDefault(parentContext =>
                        parentContext.GetType() == contextInCurrentScope.GetType());

                if (correspondingParentContext == null)
                    continue; // No DbContext of this type has been created in the parent scope yet. So no need to refresh anything for this DbContext type.

                // Both our scope and the parent scope have an instance of the same DbContext type. 
                // We can now look in the parent DbContext instance for entities that need to
                // be refreshed.
                foreach (var toRefresh in entities)
                {
                    // First, we need to find what the primary key for this entity is. 
                    // We need this in order to check if this entity has already
                    // been loaded in the parent DbContext's first-level cache.
                    var stateInCurrentScope = contextInCurrentScope.Entry(toRefresh);
                    if (stateInCurrentScope != null)
                    {
                        var entityType = stateInCurrentScope.Metadata?.GetType();

                        if (entityType == null)
                        {
                            throw new InvalidOperationException("Unrecognized entity type");
                        }

                        // Get the primary key properties.
                        // Note that entities may have composite primary keys.
                        var primaryKeyProperties = stateInCurrentScope.Metadata?.FindPrimaryKey()?.Properties.ToArray();

                        if (primaryKeyProperties == null || primaryKeyProperties.Length == 0)
                        {
                            throw new InvalidOperationException(
                                $"Cannot refresh entities in parent scope as no primary key was found for {entityType}");
                        }

                        // Create a map of primary key name(s) to their value(s).
                        var primaryKeyValues = primaryKeyProperties
                            .Select(p => new KeyValuePair<string, object?>(p.Name, entityType.GetProperty(p.Name)?.GetValue(stateInCurrentScope.Entity)))
                            .ToArray();

                        // Look for the corresponding entry in the parent context.
                        // First filter by entity type.
                        var source = from e in correspondingParentContext.ChangeTracker.Entries()
                                     where e.Entity.GetType() == stateInCurrentScope.Entity.GetType()
                                     select e;

                        // Then look for an entity which has matching primary key values.
                        foreach (var primaryKeyNameAndValue in primaryKeyValues)
                        {
                            source = from e in source
                                     where e.Entity.GetType().GetProperty(primaryKeyNameAndValue.Key)?.GetValue(e.Entity)?.Equals(primaryKeyNameAndValue.Value) ?? false
                                     select e;
                        }

                        var entityEntry = source.SingleOrDefault();

                        if (entityEntry != null && entityEntry.State == EntityState.Unchanged)
                        {
                            yield return correspondingParentContext.Entry(entityEntry.Entity);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Commit / Rollback and dispose all of our DbContext instances
            if (!_nested)
            {
                if (!_completed)
                {
                    // Do our best to clean up as much as we can but don't throw here as it's too late anyway.
                    try
                    {
                        if (_readOnly)
                        {
                            // Disposing a read-only scope before having called its SaveChanges() method
                            // is the normal and expected behavior. Read-only scopes get committed automatically.
                            CommitInternal();
                        }
                        else
                        {
                            // Disposing a read/write scope before having called its SaveChanges() method
                            // indicates that something went wrong and that all changes should be rolled-back.
                            RollbackInternal();
                        }
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                    }

                    _completed = true;
                }

                _dbContexts.Dispose();
            }

            // Pop ourself from the ambient scope stack
            var currentAmbientScope = GetAmbientScope();
            if (currentAmbientScope != this) // This is a serious programming error. Worth throwing here.
                throw new InvalidOperationException($"{nameof(DbContextScope)} instances must be disposed of in the order in which they were created!");

            RemoveAmbientScope();

            if (_parentScope != null)
            {
                if (_parentScope._disposed)
                {
                    /*
                     * If our parent scope has been disposed before us, it can only mean one thing:
                     * someone started a parallel flow of execution and forgot to suppress the
                     * ambient context before doing so. And we've been created in that parallel flow.
                     * 
                     * Since the CallContext flows through all async points, the ambient scope in the 
                     * main flow of execution ended up becoming the ambient scope in this parallel flow
                     * of execution as well. So when we were created, we captured it as our "parent scope". 
                     * 
                     * The main flow of execution then completed while our flow was still ongoing. When 
                     * the main flow of execution completed, the ambient scope there (which we think is our 
                     * parent scope) got disposed of as it should.
                     * 
                     * So here we are: our parent scope isn't actually our parent scope. It was the ambient
                     * scope in the main flow of execution from which we branched off. We should never have seen 
                     * it. Whoever wrote the code that created this parallel task should have suppressed
                     * the ambient context before creating the task - that way we wouldn't have captured
                     * this bogus parent scope.
                     * 
                     * While this is definitely a programming error, it's not worth throwing here. We can only 
                     * be in one of two scenario:
                     * 
                     * - If the developer who created the parallel task was mindful to force the creation of 
                     * a new scope in the parallel task (with IDbContextScopeFactory.CreateNew() instead of 
                     * JoinOrCreate()) then no harm has been done. We haven't tried to access the same DbContext
                     * instance from multiple threads.
                     * 
                     * - If this was not the case, they probably already got an exception complaining about the same
                     * DbContext or ObjectContext being accessed from multiple threads simultaneously (or a related
                     * error like multiple active result sets on a DataReader, which is caused by attempting to execute
                     * several queries in parallel on the same DbContext instance). So the code has already blown up.
                     * 
                     * So just record a warning here. Hopefully someone will see it and will fix the code.
                     */

                    var message = @$"PROGRAMMING ERROR - When attempting to dispose a {nameof(DbContextScope)}, we found that our parent {nameof(DbContextScope)} has already been disposed! This means that someone started a parallel flow of execution (e.g. created a TPL task, created a thread or enqueued a work item on the ThreadPool) within the context of a {nameof(DbContextScope)} without suppressing the ambient context first. 

In order to fix this:
1) Look at the stack trace below - this is the stack trace of the parallel task in question.
2) Find out where this parallel task was created.
3) Change the code so that the ambient context is suppressed before the parallel task is created. You can do this with {nameof(IDbContextScopeFactory.SuppressAmbientContext)}() (wrap the parallel task creation code block in this). 

Stack Trace:
" + Environment.StackTrace;

                    System.Diagnostics.Debug.WriteLine(message);
                }
                else
                {
                    SetAmbientScope(_parentScope);
                }
            }

            _disposed = true;

        }

        #region Ambient Context Logic

        /*
         * This is where all the magic happens. And there is not much of it.
         * 
         * This implementation is inspired by the source code of the
         * TransactionScope class in .NET 4.5.1 (the TransactionScope class
         * is prior versions of the .NET Fx didn't have support for async
         * operations).
         * 
         * In order to understand this, you'll need to be familiar with the
         * concept of async points. You'll also need to be familiar with the
         * ExecutionContext and CallContext and understand how and why they 
         * flow through async points. Stephen Toub has written an
         * excellent blog post about this - it's a highly recommended read:
         * https://devblogs.microsoft.com/pfxteam/executioncontext-vs-synchronizationcontext/
         * 
         * Overview: 
         * 
         * We want our DbContextScope instances to be ambient within 
         * the context of a logical flow of execution. This flow may be 
         * synchronous or it may be asynchronous.
         * 
         * If we only wanted to support the synchronous flow scenario, 
         * we could just store our DbContextScope instances in a ThreadStatic 
         * variable. That's the "traditional" (i.e. pre-async) way of implementing
         * an ambient context in .NET. You can see an example implementation of 
         * a TheadStatic-based ambient DbContext here: http://coding.abel.nu/2012/10/make-the-dbcontext-ambient-with-unitofworkscope/ 
         * 
         * But that would be hugely limiting as it would prevent us from being
         * able to use the new async features added to Entity Framework
         * in EF6 and .NET 4.5.
         * 
         * So we need a storage place for our DbContextScope instances 
         * that can flow through async points so that the ambient context is still 
         * available after an await (or any other async point). And this is exactly 
         * what CallContext is for.
         * 
         * There are however two issues with storing our DbContextScope instances 
         * in the CallContext:
         * 
         * 1) Items stored in the CallContext should be serializable. That's because
         * the CallContext flows not just through async points but also through app domain 
         * boundaries. I.e. if you make a remoting call into another app domain, the
         * CallContext will flow through this call (which will require all the values it
         * stores to get serialized) and get restored in the other app domain.
         * 
         * In our case, our DbContextScope instances aren't serializable. And in any case,
         * we most definitely don't want them to be flown accross app domains. So we'll
         * use the trick used by the TransactionScope class to work around this issue.
         * Instead of storing our DbContextScope instances themselves in the CallContext,
         * we'll just generate a unique key for each instance and only store that key in 
         * the CallContext. We'll then store the actual DbContextScope instances in a static
         * Dictionary against their key. 
         * 
         * That way, if an app domain boundary is crossed, the keys will be flown accross
         * but not the DbContextScope instances since a static variable is stored at the 
         * app domain level. The code executing in the other app domain won't see the ambient
         * DbContextScope created in the first app domain and will therefore be able to create
         * their own ambient DbContextScope if necessary.
         * 
         * 2) The CallContext flows through *all* async points. This means that if someone
         * decides to create multiple threads within the scope of a DbContextScope, our ambient scope
         * will flow through all the threads. Which means that all the threads will see that single 
         * DbContextScope instance as being their ambient DbContext. So clients need to be 
         * careful to always suppress the ambient context before kicking off a parallel operation
         * to avoid our DbContext instances from being accessed from multiple threads.
         */

        private static readonly string AmbientDbContextScopeKey = "AmbientDbcontext_" + Guid.NewGuid();

        // Use a ConditionalWeakTable instead of a simple ConcurrentDictionary to store our DbContextScope instances 
        // in order to prevent leaking DbContextScope instances if someone doesn't dispose them properly.
        //
        // For example, if we used a ConcurrentDictionary and someone let go of a DbContextScope instance without 
        // disposing it, our ConcurrentDictionary would still have a reference to it, preventing
        // the GC from being able to collect it => leak. With a ConditionalWeakTable, we don't hold a reference
        // to the DbContextScope instances we store in there, allowing them to get GCed.
        // The doc for ConditionalWeakTable isn't the best. This SO anser does a good job at explaining what 
        // it does: http://stackoverflow.com/a/18613811
        private static readonly ConditionalWeakTable<InstanceIdentifier, DbContextScope> DbContextScopeInstances = new ConditionalWeakTable<InstanceIdentifier, DbContextScope>();

        private readonly InstanceIdentifier _instanceIdentifier = new InstanceIdentifier();

        /// <summary>
        /// Makes the provided 'dbContextScope' available as the the ambient scope via the CallContext.
        /// </summary>
        internal static void SetAmbientScope(DbContextScope newAmbientScope)
        {
            if (newAmbientScope == null)
                throw new ArgumentNullException(nameof(newAmbientScope));

            var current = CallContext.LogicalGetData(AmbientDbContextScopeKey) as InstanceIdentifier;

            if (current == newAmbientScope._instanceIdentifier)
                return;

            // Store the new scope's instance identifier in the CallContext, making it the ambient scope
            CallContext.LogicalSetData(AmbientDbContextScopeKey, newAmbientScope._instanceIdentifier);

            // Keep track of this instance (or do nothing if we're already tracking it)
            DbContextScopeInstances.GetValue(newAmbientScope._instanceIdentifier, key => newAmbientScope);
        }

        /// <summary>
        /// Clears the ambient scope from the CallContext and stops tracking its instance. 
        /// Call this when a DbContextScope is being disposed.
        /// </summary>
        internal static void RemoveAmbientScope()
        {
            var current = CallContext.LogicalGetData(AmbientDbContextScopeKey) as InstanceIdentifier;
            CallContext.LogicalSetData(AmbientDbContextScopeKey, null);

            // If there was an ambient scope, we can stop tracking it now
            if (current != null)
            {
                DbContextScopeInstances.Remove(current);
            }
        }

        /// <summary>
        /// Clears the ambient scope from the CallContext but keeps tracking its instance. Call this to temporarily 
        /// hide the ambient context (e.g. to prevent it from being captured by parallel task).
        /// </summary>
        internal static void HideAmbientScope()
        {
            CallContext.LogicalSetData(AmbientDbContextScopeKey, null);
        }

        /// <summary>
        /// Get the current ambient scope or null if no ambient scope has been setup.
        /// </summary>
        internal static DbContextScope? GetAmbientScope()
        {
            // Retrieve the identifier of the ambient scope (if any)
            var instanceIdentifier = CallContext.LogicalGetData(AmbientDbContextScopeKey) as InstanceIdentifier;
            if (instanceIdentifier == null)
                return null; // Either no ambient context has been set or we've crossed an app domain boundary and have (intentionally) lost the ambient context

            // Retrieve the DbContextScope instance corresponding to this identifier
            DbContextScope? ambientScope;
            if (DbContextScopeInstances.TryGetValue(instanceIdentifier, out ambientScope))
                return ambientScope;

            // We have an instance identifier in the CallContext but no corresponding instance
            // in our DbContextScopeInstances table. This should never happen! The only place where
            // we remove the instance from the DbContextScopeInstances table is in RemoveAmbientScope(),
            // which also removes the instance identifier from the CallContext. 
            //
            // There's only one scenario where this could happen: someone let go of a DbContextScope 
            // instance without disposing it. In that case, the CallContext
            // would still contain a reference to the scope and we'd still have that scope's instance
            // in our DbContextScopeInstances table. But since we use a ConditionalWeakTable to store 
            // our DbContextScope instances and are therefore only holding a weak reference to these instances, 
            // the GC would be able to collect it. Once collected by the GC, our ConditionalWeakTable will return
            // null when queried for that instance. In that case, we're OK. This is a programming error 
            // but our use of a ConditionalWeakTable prevented a leak.
            System.Diagnostics.Debug.WriteLine($"Programming error detected. Found a reference to an ambient {nameof(DbContextScope)} in the {nameof(CallContext)} but didn't have an instance for it in our {nameof(DbContextScopeInstances)} table. This most likely means that this {nameof(DbContextScope)} instance wasn't disposed of properly. {nameof(DbContextScope)} instances must always be disposed. Review the code for any {nameof(DbContextScope)} instance used outside of a 'using' block and fix it so that all {nameof(DbContextScope)} instances are disposed of.");
            return null;
        }

        #endregion
    }

    /*
     * The idea of using an object reference as our instance identifier 
     * instead of simply using a unique string (which we could have generated
     * with Guid.NewGuid() for example) comes from the TransactionScope
     * class. As far as I can make out, a string would have worked just fine.
     * I'm guessing that this is done for optimization purposes. Creating
     * an empty class is cheaper and uses up less memory than generating
     * a unique string.
    */
    internal class InstanceIdentifier : MarshalByRefObject
    { }
}

