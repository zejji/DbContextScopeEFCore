using System.Data;

namespace Zejji.Entity
{
    public class DbContextReadOnlyScope(
        DbContextScopeOption joiningOption,
        IsolationLevel? isolationLevel,
        IDbContextFactory? dbContextFactory = null
    ) : IDbContextReadOnlyScope
    {
        private readonly DbContextScope _internalScope =
            new(
                joiningOption: joiningOption,
                readOnly: true,
                isolationLevel: isolationLevel,
                dbContextFactory: dbContextFactory
            );

        public IDbContextCollection DbContexts => _internalScope.DbContexts;

        public DbContextReadOnlyScope(IDbContextFactory? dbContextFactory = null)
            : this(
                joiningOption: DbContextScopeOption.JoinExisting,
                isolationLevel: null,
                dbContextFactory: dbContextFactory
            ) { }

        public DbContextReadOnlyScope(
            IsolationLevel isolationLevel,
            IDbContextFactory? dbContextFactory = null
        )
            : this(
                joiningOption: DbContextScopeOption.ForceCreateNew,
                isolationLevel: isolationLevel,
                dbContextFactory: dbContextFactory
            ) { }

        public void Dispose()
        {
            _internalScope.Dispose();
        }
    }
}
