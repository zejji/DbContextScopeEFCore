using Microsoft.EntityFrameworkCore;
using System;

namespace Zejji.Entity
{
    /// <summary>
    /// A read-only <see cref="DbContextScope"/>. Refer to the comments for <see cref="IDbContextScope"/>
    /// for more details.
    /// </summary>
    public interface IDbContextReadOnlyScope : IDisposable
    {
        /// <summary>
        /// The <see cref="DbContext"/> instances that this <see cref="DbContextScope"/> manages.
        /// </summary>
        IDbContextCollection DbContexts { get; }
    }
}
