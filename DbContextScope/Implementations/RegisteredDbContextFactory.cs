using Microsoft.EntityFrameworkCore;

namespace Zejji.Entity
{
    /// <summary>
    /// A factory to create DbContext instances.
    /// </summary>
    /// <remarks>
    /// This factory is used by the DbContextScope class library.
    /// See https://mehdi.me/ambient-dbcontext-in-ef6/
    /// It should be registered as a singleton.
    /// </remarks>
    public class RegisteredDbContextFactory : IDbContextFactory
    {
        private readonly FuncDictionaryByType _dictionary = new();

        public void RegisterDbContextType<TDbContext>(Func<TDbContext> createDbContextFunc) where TDbContext : DbContext
        {
            if (createDbContextFunc == null)
            {
                throw new ArgumentException($"{nameof(createDbContextFunc)} must not be null.", nameof(createDbContextFunc));
            }

            _dictionary.Add(createDbContextFunc);
        }

        public TDbContext CreateDbContext<TDbContext>() where TDbContext : DbContext
        {
            var isTypeRegistered = _dictionary.TryGet<TDbContext>(out var createDbContextFunc);

            if (!isTypeRegistered)
            {
                throw new InvalidOperationException($"{typeof(TDbContext).Name} was not registered with the {typeof(RegisteredDbContextFactory).Name} instance. Make sure you call {nameof(RegisterDbContextType)}.");
            }

            if (createDbContextFunc != default)
            {
                return createDbContextFunc();
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized DbContext type: {typeof(TDbContext).Name}.");
            }
        }
    }
}
