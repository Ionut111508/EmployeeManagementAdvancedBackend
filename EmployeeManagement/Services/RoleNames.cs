namespace EmployeeManagement.Services;

public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";

    public static string Normalize(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "admin" => Admin,
        "manager" => Manager,
        "employee" => Employee,
        _ => Employee
    };

    public static bool IsValid(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized is "admin" or "manager" or "employee";
    }

    public static IReadOnlyList<string> GetPermissions(string role) => Normalize(role) switch
    {
        Admin => new[]
        {
            "accounts.manage",
            "roles.manage",
            "employees.view.all",
            "employees.manage",
            "projects.view.all",
            "projects.manage",
            "tasks.view.all",
            "tasks.manage",
            "allocations.view.all",
            "allocations.manage",
            "allocations.simulate",
            "availability.view",
            "leaves.manage",
            "timesheets.view.all"
        },
        Manager => new[]
        {
            "projects.view.managed",
            "tasks.view.managed",
            "tasks.manage.managed",
            "allocations.view.managed",
            "allocations.manage.managed",
            "allocations.simulate",
            "availability.view",
            "employees.view.available",
            "employees.manage.managed",
            "leaves.view.team",
            "timesheets.view.team"
        },
        _ => new[]
        {
            "profile.view",
            "projects.view.assigned",
            "tasks.view.assigned",
            "allocations.view.own",
            "timesheets.manage.own",
            "leaves.request"
        }
    };
}
