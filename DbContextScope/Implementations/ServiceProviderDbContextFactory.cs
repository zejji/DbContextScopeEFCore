using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Zejji.Entity;

/// <summary>
/// A factory to create <see cref="DbContext"/> instances
/// using an <see cref="IServiceProvider"/> instance.
/// </summary>
/// <remarks>
/// <para>
/// This should be registered in a dependency injection container.
/// Ordinarily, it should be registered with a scoped lifetime, to allow scoped dependencies
/// to be injected into created <see cref="DbContext"/> instances, e.g. a tenant ID accessor
/// in a multi-tenant application.
/// </para>
/// <para>
/// If you are sure you will never need any scoped dependencies and know what you are doing,
/// it may be registered as a singleton.
/// </para>
/// </remarks>
public class ServiceProviderDbContextFactory(IServiceProvider serviceProvider) : IDbContextFactory
{
    public TDbContext CreateDbContext<TDbContext>()
        where TDbContext : DbContext
    {
        return (TDbContext)ActivatorUtilities.CreateInstance(serviceProvider, typeof(TDbContext));
    }
}
