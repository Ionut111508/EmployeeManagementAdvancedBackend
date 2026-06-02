using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using EmployeeManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmployeeManagement.Tests;

public class AllocationServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void CountWorkingDays_ExcludesWeekend()
    {
        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = service.CountWorkingDays(new DateTime(2026, 5, 18), new DateTime(2026, 5, 24));

        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculateTotalAllocationHours_UsesWorkingDays()
    {
        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = service.CalculateTotalAllocationHours(new DateTime(2026, 5, 18), new DateTime(2026, 5, 22), 4m);

        Assert.Equal(20m, result);
    }

    [Fact]
    public void DatesOverlap_ReturnsTrueForIntersectingIntervals()
    {
        using var context = CreateContext();
        var service = new AllocationService(context);

        var result = service.DatesOverlap(
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 10),
            new DateTime(2026, 5, 5),
            new DateTime(2026, 5, 20));

        Assert.True(result);
    }

    [Fact]
    public async Task CreateAllocationAsync_CreatesValidAllocation()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "user", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ion", LastName = "Pop", Email = "ion@test.ro", PhoneNumber = "0700000000", AccountId = "C1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.Add(new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Task description" });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Task", EstimatedHours = 40m, DescriptionId = "D1" });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T1",
            AllocationStartDate = new DateTime(2026, 5, 18),
            AllocationEndDate = new DateTime(2026, 5, 22),
            AllocatedHours = 4m
        });

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(result.Allocation);
        Assert.Single(context.Allocations);
    }

    [Fact]
    public async Task CreateAllocationAsync_RejectsWorkNormExceeded()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "user", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ion", LastName = "Pop", Email = "ion@test.ro", PhoneNumber = "0700000000", AccountId = "C1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.Add(new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Task description" });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Task", EstimatedHours = 100m, DescriptionId = "D1" });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T2", TaskName = "Task 2", EstimatedHours = 100m, DescriptionId = "D1" });
        context.Allocations.Add(new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = new DateTime(2026, 5, 18), AllocationEndDate = new DateTime(2026, 5, 22), AllocatedHours = 6m });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T2",
            AllocationStartDate = new DateTime(2026, 5, 18),
            AllocationEndDate = new DateTime(2026, 5, 22),
            AllocatedHours = 4m
        });

        Assert.False(result.Success);
        Assert.Equal("Work norm exceeded.", result.Error);
    }

    [Fact]
    public async Task CreateAllocationAsync_RejectsEmployeeBelowRequiredSkill()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "user", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ion", LastName = "Pop", Email = "ion@test.ro", PhoneNumber = "0700000000", AccountId = "C1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.Add(new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Task description" });
        context.Skills.AddRange(
            new Skill { SkillId = "DOTNET_JR", SkillName = ".NET", SkillLevel = "Junior" },
            new Skill { SkillId = "DOTNET_MID", SkillName = ".NET", SkillLevel = "Medium" });
        context.EmployeeSkills.Add(new EmployeeSkill { EmployeeId = "E1", SkillId = "DOTNET_JR", AcquiredDate = new DateTime(2026, 1, 1) });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Task", EstimatedHours = 40m, DescriptionId = "D1", RequiredSkillId = "DOTNET_MID" });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T1",
            AllocationStartDate = new DateTime(2026, 5, 18),
            AllocationEndDate = new DateTime(2026, 5, 22),
            AllocatedHours = 4m
        });

        Assert.False(result.Success);
        Assert.Equal("Employee does not meet the task required skill level.", result.Error);
    }

    [Fact]
    public async Task GetAvailabilityAsync_AllowsSeniorForMediumRequirement()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "user", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ion", LastName = "Pop", Email = "ion@test.ro", PhoneNumber = "0700000000", AccountId = "C1", WorkNormId = "N1" });
        context.Skills.AddRange(
            new Skill { SkillId = "QA_MID", SkillName = "QA", SkillLevel = "Medium" },
            new Skill { SkillId = "QA_SENIOR", SkillName = "QA", SkillLevel = "Senior" });
        context.EmployeeSkills.Add(new EmployeeSkill { EmployeeId = "E1", SkillId = "QA_SENIOR", AcquiredDate = new DateTime(2026, 1, 1) });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.GetAvailabilityAsync(new AllocationAvailabilityRequest
        {
            SkillId = "QA_MID",
            StartDate = new DateTime(2026, 5, 18),
            EndDate = new DateTime(2026, 5, 22),
            RequiredHoursPerDay = 4m
        });

        var employee = Assert.Single(result);
        Assert.True(employee.MeetsSkillRequirement);
        Assert.True(employee.CanTakeRequestedHours);
        Assert.Equal("QA_SENIOR", employee.MatchedSkillId);
    }
}
