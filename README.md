# DbContextScope.EFCore

![CI](https://github.com/zejji/DbContextScopeEFCore/actions/workflows/dotnet.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/dt/Zejji.DbContextScope.EFCore.svg)](https://www.nuget.org/packages/Zejji.DbContextScope.EFCore)
[![NuGet version (Zejji.DbContextScope.EFCore)](https://img.shields.io/nuget/v/Zejji.DbContextScope.EFCore.svg?style=flat-square)](https://www.nuget.org/packages/Zejji.DbContextScope.EFCore/)

A library for managing the lifetime of Entity Framework Core DbContext instances.

**NB:** Please use the version of this library which matches your EF Core version. For EF Core 6, the NuGet package can be found at [Zejji.DbContextScope.EFCore6](https://www.nuget.org/packages/Zejji.DbContextScope.EFCore6/). For EF Core 7 onwards, the decision was taken to remove the EF Core version from the package name and instead follow the EF Core versioning - the NuGet package can be found at [Zejji.DbContextScope.EFCore](https://www.nuget.org/packages/Zejji.DbContextScope.EFCore/).

This package is based on the original [DbContextScope repository](https://github.com/mehdime/DbContextScope) by Mehdi El Gueddari with the following changes:

- updated for .NET 6+ and EF Core (including replacing usages of `CallContext` with `AsyncLocal`);
- added fix for `RefreshEntitiesInParentScope` method so that it works correctly for entities with composite primary keys;
- added fix for `DbContextCollection`'s `Commit` and `CommitAsync` methods so that `SaveChanges` can be called more than once if there is a `DbUpdateConcurrencyException` (see [this](https://github.com/mehdime/DbContextScope/pull/31) unmerged pull request in the original `DbContextScope` repository);
- added the `RegisteredDbContextFactory` class as a concrete implementation of the `IDbContextFactory` interface, which allows users to easily register factory functions for one or more `DbContext` type(s) during startup; and
- added unit tests.

## Description

Mehdi El Gueddari's original article describing the thinking behind the `DbContextScope` library can be found [here](https://mehdi.me/ambient-dbcontext-in-ef6/).

In summary, the library addresses the problem that injecting `DbContext` instances as a scoped dependency (which ordinarily results in one instance per web request) offers insufficient control over the lifetime of `DbContext` instances in more complex scenarios.

The `DbContextScope` library allows users to create scopes which control the lifetime of ambient `DbContext` instances, as well giving control over the exact time at which changes are saved.

For general usage instructions, see article referred to above and the original GitHub repository readme file (a copy of which is included in this repository [here](./ORIGINAL_README.md)). Please note the `Mehdime.Entity` namespace has been renamed to `Zejji.Entity`.

The new `RegisteredDbContextFactory` class can be used as follows:

- In `Startup.cs`, register a `RegisteredDbContextFactory` instance as a singleton and register one or more `DbContext` factory functions on that instance, e.g.:
``` csharp
using Zejji.Entity;
...
public void ConfigureServices(IServiceCollection services)
{
    ...
    // Create an instance of the RegisteredDbContextFactory
    var dbContextFactory = new RegisteredDbContextFactory();

    // Register factory functions for each of the required DbContext types
    dbContextFactory.RegisterDbContextType<DbContextOne>(() =>
        new DbContextOne(Configuration.GetConnectionString("DatabaseOne")));
    dbContextFactory.RegisterDbContextType<DbContextTwo>(() =>
        new DbContextTwo(Configuration.GetConnectionString("DatabaseTwo")));

    // Register the RegisteredDbContextFactory instance as a singleton
    // with the dependency injection container.
    services.AddSingleton<IDbContextFactory>(dbContextFactory);
    ...
}
```

See also the unit tests for `RegisteredDbContextFactory` [here](./DbContextScope.Tests/RegisteredDbContextFactoryTests.cs).

## Getting Started

### Dependencies

- .NET 6+
- EF Core (version equal to or higher than Zejji.DbContextScope.EFCore package version)

### Installing

- `dotnet add package Zejji.DbContextScope.EFCore`

## License

This project is licensed under the MIT License - see the [LICENSE.txt](./DbContextScope/LICENSE.txt) file for details

## Acknowledgments

Many thanks to Mehdi El Gueddari for creating the original `DbContextScope` library.
