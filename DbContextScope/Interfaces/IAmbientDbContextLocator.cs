using DbContextScope.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    /// <summary>
    /// Convenience methods to retrieve ambient <see cref="DbContext"/> instances.
    /// </summary>
    public interface IAmbientDbContextLocator
    {
        /// <summary>
        /// If called within the scope of a <see cref="DbContextScope"/>, gets or creates
        /// the ambient <see cref="DbContext"/> instance for the provided <see cref="DbContext"/> type.
        ///
        /// Otherwise returns null.
        /// </summary>
        TDbContext? Get<TDbContext>()
            where TDbContext : DbContext;
        
        /// <summary>
        /// If called within the scope of a <see cref="DbContextScope"/>, gets or creates
        /// the ambient <see cref="DbContext"/> instance for the provided <see cref="DbContext"/> type.
        ///
        /// Otherwise throws a <see cref="NoAmbientDbContextScopeException"/>.
        /// </summary>
        /// <exception cref="NoAmbientDbContextScopeException">Thrown when there is no ambient <see cref="DbContextScope"/>.</exception>
        TDbContext GetRequired<TDbContext>()
            where TDbContext : DbContext;
    }
}
