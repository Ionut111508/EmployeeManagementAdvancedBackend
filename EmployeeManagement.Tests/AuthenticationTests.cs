using EmployeeManagement.Data;
using EmployeeManagement.Entities;
using EmployeeManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmployeeManagement.Tests;

public class AuthenticationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void PasswordService_HashesAndVerifiesPassword()
    {
        var service = new PasswordService();

        var hash = service.HashPassword("Secure123!");

        Assert.NotEqual("Secure123!", hash);
        Assert.True(service.IsHashed(hash));
        Assert.True(service.VerifyPassword(hash, "Secure123!", out var needsRehash));
        Assert.False(needsRehash);
        Assert.False(service.VerifyPassword(hash, "WrongPassword", out _));
    }

    [Fact]
    public void PasswordService_AcceptsLegacyPasswordForMigration()
    {
        var service = new PasswordService();

        var valid = service.VerifyPassword("Legacy123", "Legacy123", out var needsRehash);

        Assert.True(valid);
        Assert.True(needsRehash);
    }

    [Fact]
    public async Task UserRoleService_UsesAccountRoleAsSourceOfTruth()
    {
        await using var context = CreateContext();
        context.Accounts.Add(new Account { AccountId = "A1", Username = "manager-looking-user", Password = "hash", Role = RoleNames.Employee });
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8 });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ana", LastName = "Test", Email = "ana@test.ro", PhoneNumber = "0700000000", AccountId = "A1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.ProjectManagers.Add(new ProjectManager { EmployeeId = "E1", ProjectId = "P1" });
        await context.SaveChangesAsync();

        var service = new UserRoleService(context);
        var role = await service.GetRoleForEmployeeAsync("E1", "manager-looking-user");

        Assert.Equal(RoleNames.Employee, role);
    }
}
