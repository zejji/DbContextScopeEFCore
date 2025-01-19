using System;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;
using Zejji.Entity;
using Zejji.Tests.Helpers;
using Zejji.Tests.Models;

namespace Zejji.Tests
{
    public sealed class RegisteredDbContextFactoryTests : IDisposable
    {
        private readonly SqliteMemoryDatabaseLifetimeManager _databaseManager;
        private readonly RegisteredDbContextFactory _dbContextFactory;

        public RegisteredDbContextFactoryTests()
        {
            // Create a SQLite in-memory database which will last the duration of the test
            _databaseManager = new SqliteMemoryDatabaseLifetimeManager();

            _dbContextFactory = new RegisteredDbContextFactory();
        }

        public void Dispose()
        {
            _databaseManager.Dispose();
        }

        [Fact]
        public void RegisteredDbContextFactory_should_call_registered_factory_function_for_type()
        {
            var connectionString = _databaseManager.ConnectionString;
            var testDbContextFactoryCallCount = 0;
            var emptyDbContextFactoryCallCount = 0;

            // Arrange - register factory functions for the TestDbContext and EmptyDbContext types
            _dbContextFactory.RegisterDbContextType<TestDbContext>(() =>
            {
                testDbContextFactoryCallCount += 1;
                return new TestDbContext(connectionString);
            });

            _dbContextFactory.RegisterDbContextType<EmptyDbContext>(() =>
            {
                emptyDbContextFactoryCallCount += 1;
                return new EmptyDbContext();
            });

            // Act - ask the factory for some DbContexts
            var testDbContext1 = _dbContextFactory.CreateDbContext<TestDbContext>();
            var testDbContext2 = _dbContextFactory.CreateDbContext<TestDbContext>();
            var emptyDbContext = _dbContextFactory.CreateDbContext<EmptyDbContext>();

            // Assert
            testDbContextFactoryCallCount.ShouldBe(2);
            emptyDbContextFactoryCallCount.ShouldBe(1);

            testDbContext1.ShouldNotBeNull();
            testDbContext2.ShouldNotBeNull();
            emptyDbContext.ShouldNotBeNull();

            testDbContext1.ShouldNotBeSameAs(testDbContext2);

            var testDbContext1ConnectionString = testDbContext1
                .Database.GetDbConnection()
                .ConnectionString;
            testDbContext1ConnectionString.ShouldBe(connectionString);

            var testDbContext2ConnectionString = testDbContext2
                .Database.GetDbConnection()
                .ConnectionString;
            testDbContext2ConnectionString.ShouldBe(connectionString);
        }
    }
}
