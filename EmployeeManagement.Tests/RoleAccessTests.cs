using System.Security.Claims;
using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using EmployeeManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EmployeeManagement.Tests;

public class RoleAccessTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal User(string employeeId, string role) => new(new ClaimsIdentity(new[]
    {
        new Claim("employee_id", employeeId),
        new Claim(ClaimTypes.Role, role)
    }, "test"));

    [Fact]
    public async Task Employee_CannotViewAnotherEmployee()
    {
        await using var context = CreateContext();
        var service = new AccessScopeService(context);

        Assert.True(await service.CanViewEmployeeAsync(User("E1", RoleNames.Employee), "E1"));
        Assert.False(await service.CanViewEmployeeAsync(User("E1", RoleNames.Employee), "E2"));
    }

    [Fact]
    public async Task Manager_CanOnlyViewEmployeesFromManagedProjects()
    {
        await using var context = CreateContext();
        SeedEmployees(context);
        context.Projects.AddRange(
            new Project { ProjectId = "P1", ProjectName = "Managed" },
            new Project { ProjectId = "P2", ProjectName = "Other" });
        context.Descriptions.AddRange(
            new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Managed task" },
            new TaskDescription { DescriptionId = "D2", TaskDescriptionText = "Other task" });
        context.TaskItems.AddRange(
            new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Managed task", DescriptionId = "D1" },
            new TaskItem { ProjectId = "P2", TaskId = "T2", TaskName = "Other task", DescriptionId = "D2" });
        context.ProjectManagers.Add(new ProjectManager { EmployeeId = "M1", ProjectId = "P1" });
        context.Allocations.AddRange(
            new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = DateTime.Today, AllocatedHours = 4 },
            new Allocation { EmployeeId = "E2", ProjectId = "P2", TaskId = "T2", AllocationStartDate = DateTime.Today, AllocatedHours = 4 });
        await context.SaveChangesAsync();

        var service = new AccessScopeService(context);
        var manager = User("M1", RoleNames.Manager);

        Assert.True(await service.CanViewEmployeeAsync(manager, "E1"));
        Assert.False(await service.CanViewEmployeeAsync(manager, "E2"));
        Assert.True(await service.CanManageProjectAsync(manager, "P1"));
        Assert.False(await service.CanManageProjectAsync(manager, "P2"));
    }

    [Fact]
    public async Task PromotionToManager_RequiresProjectAndCreatesAssignment()
    {
        await using var context = CreateContext();
        SeedEmployees(context);
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Managed" });
        await context.SaveChangesAsync();
        var service = new UserRoleService(context);

        var invalid = await service.UpdateEmployeeRoleAsync("E1", new UpdateEmployeeRoleDto { Role = RoleNames.Manager });
        var valid = await service.UpdateEmployeeRoleAsync("E1", new UpdateEmployeeRoleDto
        {
            Role = RoleNames.Manager,
            ManagedProjectIds = new List<string> { "P1" }
        });

        Assert.False(invalid.Success);
        Assert.Equal("A manager must be assigned to at least one project.", invalid.Error);
        Assert.True(valid.Success);
        Assert.Equal(RoleNames.Manager, context.Accounts.Single(account => account.AccountId == "A1").Role);
        Assert.True(await context.ProjectManagers.AnyAsync(pm => pm.EmployeeId == "E1" && pm.ProjectId == "P1"));
    }

    private static void SeedEmployees(AppDbContext context)
    {
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8 });
        context.Accounts.AddRange(
            new Account { AccountId = "AM", Username = "manager", Password = "hash", Role = RoleNames.Manager },
            new Account { AccountId = "A1", Username = "one", Password = "hash", Role = RoleNames.Employee },
            new Account { AccountId = "A2", Username = "two", Password = "hash", Role = RoleNames.Employee });
        context.Employees.AddRange(
            new Employee { EmployeeId = "M1", FirstName = "Maria", LastName = "Manager", Email = "m@test.ro", PhoneNumber = "1", AccountId = "AM", WorkNormId = "N1" },
            new Employee { EmployeeId = "E1", FirstName = "Ana", LastName = "One", Email = "1@test.ro", PhoneNumber = "2", AccountId = "A1", WorkNormId = "N1" },
            new Employee { EmployeeId = "E2", FirstName = "Dan", LastName = "Two", Email = "2@test.ro", PhoneNumber = "3", AccountId = "A2", WorkNormId = "N1" });
    }
}
