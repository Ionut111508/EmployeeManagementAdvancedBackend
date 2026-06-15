using System.Security.Claims;
using EmployeeManagement.Controllers;
using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using EmployeeManagement.Entities;
using EmployeeManagement.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EmployeeManagement.Tests;

public class WorkflowTests
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

    [Theory]
    [InlineData(TaskStatuses.Backlog, TaskStatuses.Ready, true)]
    [InlineData(TaskStatuses.Ready, TaskStatuses.Completed, false)]
    [InlineData(TaskStatuses.InProgress, TaskStatuses.Completed, true)]
    [InlineData(TaskStatuses.Completed, TaskStatuses.InProgress, true)]
    public void TaskWorkflow_OnlyAllowsExplicitTransitions(string current, string next, bool expected)
    {
        Assert.Equal(expected, TaskStatuses.CanTransition(current, next));
    }

    [Fact]
    public async Task Timesheet_SubmissionIsPendingAndManagerCanApproveIt()
    {
        await using var context = CreateContext();
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8 });
        context.Accounts.AddRange(
            new Account { AccountId = "AE", Username = "employee", Password = "hash", Role = RoleNames.Employee },
            new Account { AccountId = "AM", Username = "manager", Password = "hash", Role = RoleNames.Manager });
        context.Employees.AddRange(
            new Employee { EmployeeId = "E1", FirstName = "Ana", LastName = "Employee", Email = "e@test.ro", PhoneNumber = "1", AccountId = "AE", WorkNormId = "N1" },
            new Employee { EmployeeId = "M1", FirstName = "Mara", LastName = "Manager", Email = "m@test.ro", PhoneNumber = "2", AccountId = "AM", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.Add(new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "Task" });
        context.TaskItems.Add(new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "Task", DescriptionId = "D1", Status = TaskStatuses.InProgress });
        context.ProjectManagers.Add(new ProjectManager { EmployeeId = "M1", ProjectId = "P1" });
        context.Allocations.Add(new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = new DateTime(2026, 6, 15), AllocationEndDate = new DateTime(2026, 6, 19), AllocatedHours = 8 });
        await context.SaveChangesAsync();

        var access = new AccessScopeService(context);
        var audit = new AuditLogService(context);
        var controller = new TimesheetsController(context, access, audit) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = User("E1", RoleNames.Employee) } } };
        var submitted = await controller.Add(new TimesheetRequest { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", WorkDate = new DateTime(2026, 6, 15), WorkedHours = 6 });

        Assert.IsType<OkObjectResult>(submitted);
        Assert.Equal(TimesheetStatuses.Pending, (await context.Timesheets.SingleAsync()).Status);

        controller.ControllerContext.HttpContext.User = User("M1", RoleNames.Manager);
        var reviewed = await controller.Review("P1", "T1", "E1", new DateTime(2026, 6, 15), new TimesheetReviewRequest { Status = TimesheetStatuses.Approved, Comment = "Verified" });

        Assert.IsType<OkObjectResult>(reviewed);
        var entry = await context.Timesheets.SingleAsync();
        Assert.Equal(TimesheetStatuses.Approved, entry.Status);
        Assert.Equal("M1", entry.ReviewedByEmployeeId);
        Assert.Equal(2, await context.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Notifications_ReportStaffingDeficitAndDailyOverallocation()
    {
        await using var context = CreateContext();
        var workDate = DateTime.Today;
        while (workDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) workDate = workDate.AddDays(1);
        context.WorkNorms.Add(new WorkNorm { WorkNormId = "N1", WorkNormName = "Full", WorkHours = 8 });
        context.Accounts.Add(new Account { AccountId = "A1", Username = "employee", Password = "hash", Role = RoleNames.Employee });
        context.Employees.Add(new Employee { EmployeeId = "E1", FirstName = "Ana", LastName = "Employee", Email = "e@test.ro", PhoneNumber = "1", AccountId = "A1", WorkNormId = "N1" });
        context.Projects.Add(new Project { ProjectId = "P1", ProjectName = "Project" });
        context.Descriptions.AddRange(
            new TaskDescription { DescriptionId = "D1", TaskDescriptionText = "One" },
            new TaskDescription { DescriptionId = "D2", TaskDescriptionText = "Two" });
        context.TaskItems.AddRange(
            new TaskItem { ProjectId = "P1", TaskId = "T1", TaskName = "One", DescriptionId = "D1", EstimatedHours = 40, Status = TaskStatuses.InProgress, PlannedStartDate = workDate },
            new TaskItem { ProjectId = "P1", TaskId = "T2", TaskName = "Two", DescriptionId = "D2", EstimatedHours = 40, Status = TaskStatuses.InProgress, PlannedStartDate = workDate });
        context.Allocations.AddRange(
            new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T1", AllocationStartDate = workDate, AllocationEndDate = workDate, AllocatedHours = 5 },
            new Allocation { EmployeeId = "E1", ProjectId = "P1", TaskId = "T2", AllocationStartDate = workDate, AllocationEndDate = workDate, AllocatedHours = 5 });
        await context.SaveChangesAsync();

        var access = new AccessScopeService(context);
        var allocationService = new AllocationService(context);
        var controller = new NotificationsController(context, access, allocationService) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = User("ADMIN", RoleNames.Admin) } } };
        var action = await controller.Get(7);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var notifications = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(ok.Value).ToList();

        Assert.Contains(notifications, item => item.Type == "StaffingDeficit");
        Assert.Contains(notifications, item => item.Type == "OverAllocation" && item.EmployeeId == "E1");
    }
}
