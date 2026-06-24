namespace EmployeeManagement.Services;

public static class TimesheetStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class TaskStatuses
{
    public const string Backlog = "Backlog";
    public const string Ready = "Ready";
    public const string InProgress = "InProgress";
    public const string Blocked = "Blocked";
    public const string Delayed = "Delayed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Backlog, Ready, InProgress, Blocked, Completed, Cancelled };

    public static bool CanTransition(string current, string next) => current switch
    {
        Backlog => next is Ready or Cancelled,
        Ready => next is InProgress or Blocked or Cancelled,
        InProgress => next is Blocked or Completed or Cancelled,
        Blocked => next is InProgress or Cancelled,
        Completed => next is InProgress,
        Cancelled => next is Backlog,
        _ => false
    };

    public static string Resolve(string workflowStatus, DateTime? plannedEndDate, decimal? estimatedHours, decimal approvedWorkedHours, DateTime today)
    {
        if (workflowStatus is Completed or Cancelled)
            return workflowStatus;

        var estimated = estimatedHours ?? 0;
        return plannedEndDate.HasValue &&
            plannedEndDate.Value.Date < today.Date &&
            estimated > 0 &&
            approvedWorkedHours + 0.05m < estimated
                ? Delayed
                : workflowStatus;
    }
}
