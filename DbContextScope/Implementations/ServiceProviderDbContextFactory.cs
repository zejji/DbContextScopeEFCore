using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Zejji.Entity;

/// <summary>
/// A factory to create <see cref="DbContext"/> instances
/// using an <see cref="IServiceProvider"/> instance.
/// </summary>
/// <remarks>
/// It should be registered in a dependency injection container as a singleton.
/// </remarks>
public class ServiceProviderDbContextFactory(IServiceProvider serviceProvider)
    : IDbContextFactory
{
    public TDbContext CreateDbContext<TDbContext>()
        where TDbContext : DbContext
    {
        return (TDbContext)ActivatorUtilities.CreateInstance(serviceProvider, typeof(TDbContext));
    }
}
