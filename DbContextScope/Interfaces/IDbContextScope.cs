using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Zejji.Entity
{
    /// <summary>
    /// Creates and manages the <see cref="DbContextScope"/> instances used by this code block.
    ///
    /// You typically use a <see cref="DbContextScope"/> at the business logic service level. Each
    /// business transaction (i.e. each service method) that uses Entity Framework must
    /// be wrapped in a <see cref="DbContextScope"/>, ensuring that the same DbContext instances
    /// are used throughout the business transaction and are committed or rolled
    /// back atomically.
    ///
    /// Think of it as TransactionScope but for managing <see cref="DbContext"/> instances instead
    /// of database transactions. Just like a TransactionScope, a <see cref="DbContextScope"/> is
    /// ambient, can be nested and supports async execution flows.
    ///
    /// And just like TransactionScope, it does not support parallel execution flows.
    /// You therefore MUST suppress the ambient <see cref="DbContextScope"/> before kicking off parallel
    /// tasks or you will end up with multiple threads attempting to use the same <see cref="DbContext"/>
    /// instances (use <see cref="IDbContextScopeFactory.SuppressAmbientContext()"/> for this).
    ///
    /// You can access the <see cref="DbContext"/> instances that this scopes manages via either:
    /// - its <see cref="DbContextScope.DbContexts" /> property, or
    /// - an <see cref="IAmbientDbContextLocator"/>
    ///
    /// (you would typically use the later in the repository / query layer to allow your repository
    /// or query classes to access the ambient <see cref="DbContext"/> instances without giving them access to the actual
    /// <see cref="DbContextScope"/>).
    ///
    /// </summary>
    public interface IDbContextScope : IDisposable
    {
        /// <summary>
        /// Saves the changes in all the <see cref="DbContext"/> instances that were created within this scope.
        /// This method can only be called once per scope.
        /// </summary>
        int SaveChanges();

        /// <summary>
        /// Saves the changes in all the <see cref="DbContext"/> instances that were created within this scope.
        /// This method can only be called once per scope.
        /// </summary>
        Task<int> SaveChangesAsync();

        /// <summary>
        /// Saves the changes in all the <see cref="DbContext"/> instances that were created within this scope.
        /// This method can only be called once per scope.
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancelToken);

        /// <summary>
        /// Reloads the provided persistent entities from the data store
        /// in the <see cref="DbContext"/> instances managed by the parent scope.
        ///
        /// If there is no parent scope (i.e. if this <see cref="DbContextScope"/>
        /// is the top-level scope), does nothing.
        ///
        /// This is useful when you have forced the creation of a new
        /// <see cref="DbContextScope"/> and want to make sure that the parent scope
        /// (if any) is aware of the entities you've modified in the
        /// inner scope.
        ///
        /// (this is a pretty advanced feature that should be used
        /// with parsimony).
        /// </summary>
        void RefreshEntitiesInParentScope(IEnumerable entities);

        /// <summary>
        /// Reloads the provided persistent entities from the data store
        /// in the <see cref="DbContext"/> instances managed by the parent scope.
        ///
        /// If there is no parent scope (i.e. if this <see cref="DbContextScope"/>
        /// is the top-level scope), does nothing.
        ///
        /// This is useful when you have forced the creation of a new
        /// <see cref="DbContextScope"/> and want to make sure that the parent scope
        /// (if any) is aware of the entities you've modified in the
        /// inner scope.
        ///
        /// (this is a pretty advanced feature that should be used
        /// with parsimony).
        /// </summary>
        Task RefreshEntitiesInParentScopeAsync(IEnumerable entities);

        /// <summary>
        /// The <see cref="DbContext"/> instances that this <see cref="DbContextScope"/> manages.
        /// Don't call <see cref="DbContext.SaveChanges()"/> on the <see cref="DbContext"/> themselves!
        /// Save the scope instead.
        /// </summary>
        IDbContextCollection DbContexts { get; }
    }
}
