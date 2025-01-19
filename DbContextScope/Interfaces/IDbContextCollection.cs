using System;
using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    /// <summary>
    /// Maintains a list of lazily-created <see cref="DbContext"/> instances.
    /// </summary>
    public interface IDbContextCollection : IDisposable
    {
        /// <summary>
        /// Get or create a <see cref="DbContext"/> instance of the specified type.
        /// </summary>
        TDbContext Get<TDbContext>()
            where TDbContext : DbContext;
    }
}
