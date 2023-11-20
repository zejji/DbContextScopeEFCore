using DbContextScope.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    public class AmbientDbContextLocator : IAmbientDbContextLocator
    {
        public TDbContext? Get<TDbContext>()
            where TDbContext : DbContext
        {
            var ambientDbContextScope = DbContextScope.GetAmbientScope();
            return ambientDbContextScope?.DbContexts.Get<TDbContext>();
        }

        public TDbContext GetRequired<TDbContext>()
            where TDbContext : DbContext
        {
            var ambientDbContextScope =
                DbContextScope.GetAmbientScope()
                ?? throw new NoAmbientDbContextScopeException(
                    $"No ambient {nameof(DbContext)} of type {typeof(TDbContext).Name} found. This usually means that this repository method has been called outside of the scope of a {nameof(DbContextScope)}. A repository must only be accessed within the scope of a {nameof(DbContextScope)}, which takes care of creating the {nameof(DbContext)} instances that the repositories need and making them available as ambient contexts. This is what ensures that, for any given {nameof(DbContext)}-derived type, the same instance is used throughout the duration of a business transaction. To fix this issue, use {nameof(IDbContextScopeFactory)} in your top-level business logic service method to create a {nameof(DbContextScope)} that wraps the entire business transaction that your service method implements. Then access this repository within that scope. Refer to the comments in the {nameof(IDbContextScope)}.cs file for more details."
                );

            return ambientDbContextScope.DbContexts.Get<TDbContext>();
        }
    }
}
