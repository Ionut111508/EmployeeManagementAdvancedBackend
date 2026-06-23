using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using EmployeeManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EmployeeManagement.Tests;

public class AllocationServiceTests
{
    private static DateTime NextMonday()
    {
        var date = DateTime.Today;
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

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
        var startDate = NextMonday();
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T1",
            AllocationStartDate = startDate,
            AllocationEndDate = startDate.AddDays(4),
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
        var startDate = NextMonday();
        context.Allocations.Add(new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = startDate, AllocationEndDate = startDate.AddDays(4), AllocatedHours = 6m });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T2",
            AllocationStartDate = startDate,
            AllocationEndDate = startDate.AddDays(4),
            AllocatedHours = 4m
        });

        Assert.False(result.Success);
        Assert.Equal("Work norm exceeded.", result.Error);
    }

    [Fact]
    public async Task CreateAllocationAsync_RejectsPastStartDate()
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
            AllocationStartDate = DateTime.Today.AddDays(-1),
            AllocationEndDate = DateTime.Today,
            AllocatedHours = 4m
        });

        Assert.False(result.Success);
        Assert.Equal("Allocation start date cannot be in the past.", result.Error);
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
        var startDate = NextMonday();
        var result = await service.CreateAllocationAsync(new CreateAllocationRequest
        {
            EmployeeId = "E1",
            ProjectId = "P1",
            TaskId = "T1",
            AllocationStartDate = startDate,
            AllocationEndDate = startDate.AddDays(4),
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

    [Fact]
    public async Task BuildTaskPlanAsync_UsesMultipleQualifiedEmployeesWithoutExceedingDailyCapacity()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.AddRange(
            new Account { AccountId = "C1", Username = "one", Password = "pass" },
            new Account { AccountId = "C2", Username = "two", Password = "pass" });
        context.Employees.AddRange(
            new Employee { EmployeeId = "E1", FirstName = "Ana", LastName = "One", Email = "one@test.ro", PhoneNumber = "1", AccountId = "C1", WorkNormId = "N1" },
            new Employee { EmployeeId = "E2", FirstName = "Dan", LastName = "Two", Email = "two@test.ro", PhoneNumber = "2", AccountId = "C2", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.AddRange(
            new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Existing one" },
            new TaskDescription { DescriptionId = "D2", TaskDescriptionText = "Existing two" });
        context.TaskItems.AddRange(
            new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Existing one", EstimatedHours = 100m, DescriptionId = "D1" },
            new TaskItem { ProjectId = "P1", TaskId = "T2", TaskName = "Existing two", EstimatedHours = 100m, DescriptionId = "D2" });
        context.Allocations.AddRange(
            new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = new DateTime(2026, 6, 15), AllocationEndDate = new DateTime(2026, 6, 19), AllocatedHours = 6m },
            new Allocation { EmployeeId = "E2", ProjectId = "P1", TaskId = "T2", AllocationStartDate = new DateTime(2026, 6, 15), AllocationEndDate = new DateTime(2026, 6, 19), AllocatedHours = 6m });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.BuildTaskPlanAsync(new TaskPlanningPreviewRequest
        {
            ProjectId = "P1",
            EstimatedHours = 20m,
            PlannedStartDate = new DateTime(2026, 6, 15),
            PlannedEndDate = new DateTime(2026, 6, 19)
        });

        Assert.True(result.CanFullyStaff);
        Assert.Equal(2, result.AutomaticPlan.Count);
        Assert.All(result.AutomaticPlan, allocation => Assert.Equal(2m, allocation.HoursPerDay));
        Assert.Equal(20m, result.AutomaticPlan.Sum(allocation => allocation.TotalHours));
    }

    [Fact]
    public async Task BuildTaskPlanAsync_DoesNotReportRoundingGapWhenCapacityIsSufficient()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "available", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Available", LastName = "Person", Email = "available@test.ro", PhoneNumber = "1", AccountId = "C1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.BuildTaskPlanAsync(new TaskPlanningPreviewRequest
        {
            ProjectId = "P1",
            EstimatedHours = 40m,
            PlannedStartDate = new DateTime(2026, 6, 15),
            PlannedEndDate = new DateTime(2026, 6, 29)
        });

        Assert.True(result.CanFullyStaff);
        Assert.InRange(result.RemainingUncoveredHours, 0m, 0.05m);
        Assert.Equal(3.636m, Assert.Single(result.AutomaticPlan).HoursPerDay);
    }

    [Fact]
    public async Task BuildTaskPlanAsync_ExcludesEmployeeAtFullDailyNorm()
    {
        using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8m });
        context.Accounts.Add(new Account { AccountId = "C1", Username = "full", Password = "pass" });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Full", LastName = "Person", Email = "full@test.ro", PhoneNumber = "1", AccountId = "C1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.Add(new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Existing" });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Existing", EstimatedHours = 100m, DescriptionId = "D1" });
        context.Allocations.Add(new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = new DateTime(2026, 6, 15), AllocationEndDate = new DateTime(2026, 6, 19), AllocatedHours = 8m });
        await context.SaveChangesAsync();

        var service = new AllocationService(context);
        var result = await service.BuildTaskPlanAsync(new TaskPlanningPreviewRequest
        {
            ProjectId = "P1",
            EstimatedHours = 10m,
            PlannedStartDate = new DateTime(2026, 6, 15),
            PlannedEndDate = new DateTime(2026, 6, 19)
        });

        Assert.False(result.CanFullyStaff);
        Assert.Empty(result.AutomaticPlan);
        Assert.Equal(0m, Assert.Single(result.Candidates).MaxAssignableHours);
        Assert.Equal(10m, result.RemainingUncoveredHours);
    }
}
