using DbContextScope.Tests.Helpers;
using DbContextScope.Tests.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Xunit;
using Zejji.Entity;

namespace DbContextScope.Tests;
public class DbContextScopeTests : IDisposable
{
    private readonly SqliteMemoryDatabaseLifetimeManager _databaseManager;
    private readonly IDbContextFactory _dbContextFactory;
    private readonly DbContextScopeFactory _dbContextScopeFactory;

    public DbContextScopeTests()
    {
        // Create a SQLite in-memory database which will last the duration of the test
        _databaseManager = new SqliteMemoryDatabaseLifetimeManager();

        // Get the connection string and create a DbContextScopeFactory
        var connectionString = _databaseManager.ConnectionString;
        _dbContextFactory = new TestDbContextFactory(connectionString);
        _dbContextScopeFactory = new DbContextScopeFactory(_dbContextFactory);

        // Ensure the database is created
        using (var dbContext =  _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            dbContext.Database.EnsureCreated();
        }
    }

    public void Dispose()
    {
        _databaseManager.Dispose();
    }

    [Fact]
    public void Nested_scopes_should_use_same_DbContext_by_default()
    {
        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            var outerDbContext = outerDbContextScope.DbContexts.Get<TestDbContext>();

            using (var innerDbContextScope = _dbContextScopeFactory.Create())
            {
                var innerDbContext = innerDbContextScope.DbContexts.Get<TestDbContext>();

                outerDbContext.Should().NotBeNull();
                innerDbContext.Should().NotBeNull();

                innerDbContext.Should().BeSameAs(outerDbContext);
            }
        }
    }

    [Fact]
    public void Calling_SaveChanges_on_a_nested_scope_has_no_effect()
    {
        var originalName = "Test User";
        var newName = "New name";

        // Arrange - add one user to the database
        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            dbContext.Users.Add(new User { Name = originalName });
            dbContext.SaveChanges();
        }
        
        // Act - create nested DbContextScopes and attempt to save on the inner scope
        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            using (var innerDbContextScope = _dbContextScopeFactory.Create())
            {
                var innerDbContext = innerDbContextScope.DbContexts.Get<TestDbContext>();
                var user = innerDbContext.Users.Single();
                user.Name = newName;
                innerDbContextScope.SaveChanges();
            }
        }

        // Assert - name should NOT have changed
        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            var user = dbContext.Users.Single();
            user.Name.Should().Be(originalName);
        }
    }

    [Fact]
    public void Calling_SaveChanges_on_the_outer_scope_saves_changes()
    {
        var originalName = "Test User";
        var newName = "New name";

        // Arrange - add one user to the database
        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            dbContext.Users.Add(new User { Name = originalName });
            dbContext.SaveChanges();
        }

        // Act - create an outer DbContextScope and attempt to save
        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            var dbContext = outerDbContextScope.DbContexts.Get<TestDbContext>();
            var user = dbContext.Users.Single();
            user.Name = newName;
            outerDbContextScope.SaveChanges();
        }

        // Assert - name should have changed
        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            var user = dbContext.Users.Single();
            user.Name.Should().Be(newName);
        }
    }

    [Fact]
    public void IDbContextReadOnlyScope_should_not_have_SaveChanges_method()
    {
        using (var dbContextScope = _dbContextScopeFactory.CreateReadOnly())
        {
            // Use reflection to check that there is no "SaveChanges" method on dbContextScope
            var type = dbContextScope.GetType();
            var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var saveChangesMethod = publicMethods.Where(m => m.Name == "SaveChanges").SingleOrDefault();
            saveChangesMethod.Should().BeNull();
        }
    }

    [Fact]
    public void AmbientDbContextLocator_should_return_ambient_scope()
    {
        using (var dbContextScope = _dbContextScopeFactory.Create())
        {
            var dbContext = dbContextScope.DbContexts.Get<TestDbContext>();
            
            var contextLocator = new AmbientDbContextLocator();
            var ambientDbContext = contextLocator.Get<TestDbContext>();

            ambientDbContext.Should().NotBeNull();
            ambientDbContext.Should().BeSameAs(dbContext);
        }
    }

    [Fact]
    public void ForceCreateNew_option_should_create_new_DbContext_in_nested_scope()
    {
        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            var outerDbContext = outerDbContextScope.DbContexts.Get<TestDbContext>();

            using (var innerDbContextScope = _dbContextScopeFactory.Create(DbContextScopeOption.ForceCreateNew))
            {
                var innerDbContext = innerDbContextScope.DbContexts.Get<TestDbContext>();

                outerDbContext.Should().NotBeNull();
                innerDbContext.Should().NotBeNull();

                innerDbContext.Should().NotBeSameAs(outerDbContext);
            }
        }
    }

    [Fact]
    public void RefreshEntitiesInParentScope_should_reload_changed_data_from_database()
    {
        var originalName1 = "Test User 1";
        var originalName2 = "Test User 2";
        var newName1 = "New name 1";
        var newName2 = "New name 2";

        // Arrange - add two users to the database
        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            dbContext.Users.AddRange(new User[]
            {
                new User { Name = originalName1 },
                new User { Name = originalName2 }
            });
            dbContext.SaveChanges();
        }

        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            var outerDbContext = outerDbContextScope.DbContexts.Get<TestDbContext>();
            var outerUsers = outerDbContext.Users.ToList();

            outerUsers.Count.Should().Be(2);

            // Arrange - modify the entity in an inner scope created with ForceCreateNew
            using (var innerDbContextScope = _dbContextScopeFactory.Create(DbContextScopeOption.ForceCreateNew))
            {
                var innerDbContext = innerDbContextScope.DbContexts.Get<TestDbContext>();
                var innerUsers = innerDbContext.Users.ToList();
                innerUsers[0].Name = newName1;
                innerUsers[1].Name = newName2;

                innerDbContext.SaveChanges();

                // Entities in outer scope should be unchanged at this point
                // since we have not refreshed the entities yet
                outerUsers[0].Name.Should().Be(originalName1);
                outerUsers[1].Name.Should().Be(originalName2);

                // Act - only refresh first user in parent scope, but not the second
                innerDbContextScope.RefreshEntitiesInParentScope(new User[] { innerUsers[0] });

                // Assert
                outerUsers[0].Name.Should().Be(newName1);
                outerUsers[1].Name.Should().Be(originalName2);
            }
        }
    }

    [Fact]
    public void RefreshEntitiesInParentScope_should_refresh_entities_with_composite_primary_keys()
    {
        // Arrange(1) - create two new users with associated courses and grades
        var course1 = new Course { Name = "Computing" };
        var course2 = new Course { Name = "English" };

        using (var dbContext = _dbContextFactory.CreateDbContext<TestDbContext>())
        {
            dbContext.Users.AddRange(new User[]
            {
                new User
                {
                    Name = "Test User 1",
                    CoursesUsers = new CourseUser[]
                    {
                        new CourseUser { Course = course1, Grade = "A" },
                        new CourseUser { Course = course2, Grade = "C" }
                    }
                },
                new User
                {
                    Name = "Test User 2",
                    CoursesUsers = new CourseUser[]
                    {
                    new CourseUser { Course = course1, Grade = "F" }
                    }
                }
            });
            dbContext.SaveChanges();
        }

        using (var outerDbContextScope = _dbContextScopeFactory.Create())
        {
            var outerDbContext = outerDbContextScope.DbContexts.Get<TestDbContext>();
            var outerUsers = outerDbContext.Users
                .Include(u => u.CoursesUsers)
                .ThenInclude(cu => cu.Course)
                .ToList();

            outerUsers.Count.Should().Be(2);

            // Arrange(2) - modify the CourseUser entities in an inner scope created with ForceCreateNew
            using (var innerDbContextScope = _dbContextScopeFactory.Create(DbContextScopeOption.ForceCreateNew))
            {
                var innerDbContext = innerDbContextScope.DbContexts.Get<TestDbContext>();
                var innerUsers = innerDbContext.Users
                    .Include(u => u.CoursesUsers)
                    .ThenInclude(cu => cu.Course)
                    .ToList();

                var innerUser1CoursesUsers = innerUsers[0].CoursesUsers.ToList();
                innerUser1CoursesUsers[0].Grade = "B";
                innerUser1CoursesUsers[1].Grade = "D";

                var innerUser2CoursesUsers = innerUsers[1].CoursesUsers.ToList();
                innerUser2CoursesUsers[0].Grade = "E";

                innerDbContext.SaveChanges();

                // Entities in outer scope should be unchanged at this point
                // since we have not refreshed the entities yet
                var outerUser1CoursesUsers = outerUsers[0].CoursesUsers.ToList();
                outerUser1CoursesUsers[0].Grade.Should().Be("A");
                outerUser1CoursesUsers[1].Grade.Should().Be("C");

                var outerUser2CoursesUsers = outerUsers[1].CoursesUsers.ToList();
                outerUser2CoursesUsers[0].Grade.Should().Be("F");

                // Act - only refresh the first user's CoursesUsers in the parent scope,
                // but NOT the second user's
                innerDbContextScope.RefreshEntitiesInParentScope(new CourseUser[]
                {
                    outerUser1CoursesUsers[0],
                    outerUser1CoursesUsers[1]
                });

                // Assert
                outerUser1CoursesUsers[0].Grade.Should().Be("B"); // new value
                outerUser1CoursesUsers[1].Grade.Should().Be("D"); // new value
                outerUser2CoursesUsers[0].Grade.Should().Be("F"); // unchanged from original value
            }
        }
    }
}