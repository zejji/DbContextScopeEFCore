using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    /// <summary>
    /// Indicates whether or not a new <see cref="DbContextScope"/> will join the ambient scope.
    /// </summary>
    public enum DbContextScopeOption
    {
        /// <summary>
        /// Join the ambient <see cref="DbContextScope"/> if one exists. Creates a new
        /// one otherwise.
        ///
        /// This is what you want in most cases. Joining the existing ambient scope
        /// ensures that all code within a business transaction uses the same <see cref="DbContext"/>
        /// instance and that all changes made by service methods called within that
        /// business transaction are either committed or rolled back atomically when the top-level
        /// scope completes (i.e. it ensures that there are no partial commits).
        /// </summary>
        JoinExisting,

        /// <summary>
        /// Ignore the ambient <see cref="DbContextScope"/> (if any) and force the creation of
        /// a new <see cref="DbContextScope"/>.
        ///
        /// This is an advanced feature that should be used with great care.
        ///
        /// When forcing the creation of a new scope, new <see cref="DbContext"/> instances will be
        /// created within that inner scope instead of re-using the <see cref="DbContext"/> instances that
        /// the parent scope (if any) is using.
        ///
        /// Any changes made to entities within that inner scope will therefore get persisted
        /// to the database when <see cref="DbContextScope.SaveChanges()"/> is called in the inner scope regardless of whether
        /// or not the parent scope is successful.
        ///
        /// You would typically do this to ensure that the changes made within the inner scope
        /// are always persisted regardless of the outcome of the overall business transaction
        /// (e.g. to persist the results of an operation, such as a remote API call, that
        /// cannot be rolled back or to persist audit or log entries that must not be rolled back
        /// regardless of the outcome of the business transaction).
        /// </summary>
        ForceCreateNew
    }
}
