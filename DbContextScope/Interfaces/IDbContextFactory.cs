using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    /// <summary>
    /// Factory for <see cref="DbContext"/>-derived classes that don't expose
    /// a default constructor.
    /// </summary>
    /// <remarks>
    /// If your <see cref="DbContext"/>-derived classes have a default constructor,
    /// you can ignore this factory. <see cref="DbContextScope"/> will take care of
    /// instantiating your <see cref="DbContext"/> class with <see cref="System.Activator.CreateInstance{T}()"/>
    /// when needed.
    ///
    /// If your <see cref="DbContext"/>-derived classes don't expose a default constructor
    /// however, you must implement this interface and provide it to <see cref="DbContextScope"/>
    /// so that it can create instances of your <see cref="DbContext"/>s.
    ///
    /// A typical situation where this would be needed is in the case of your <see cref="DbContext"/>-derived
    /// class having a dependency on some other component in your application. For example,
    /// some data in your database may be encrypted and you might want your <see cref="DbContext"/>-derived
    /// class to automatically decrypt this data on entity materialization. It would therefore
    /// have a mandatory dependency on an IDataDecryptor component that knows how to do that.
    /// In that case, you'll want to implement this interface and pass it to the <see cref="DbContextScope"/>
    /// you're creating so that <see cref="DbContextScope"/> is able to create your <see cref="DbContext"/> instances correctly.
    /// </remarks>
    public interface IDbContextFactory
    {
        TDbContext CreateDbContext<TDbContext>()
            where TDbContext : DbContext;
    }
}
