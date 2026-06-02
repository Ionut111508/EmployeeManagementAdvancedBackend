using EmployeeManagement.Data;
using EmployeeManagement.DTOs;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Services;

public class UserRoleService : IUserRoleService
{
    private const string Admin = "Admin";
    private const string Manager = "Manager";
    private const string Employee = "Employee";
    private readonly AppDbContext _context;

    public UserRoleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GetRoleForEmployeeAsync(string employeeId, string username)
    {
        if (IsAdminUsername(username))
            return Admin;

        var accountId = await _context.Employees
            .AsNoTracking()
            .Where(e => e.EmployeeId == employeeId)
            .Select(e => e.AccountId)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(accountId))
        {
            var accountRole = await ReadAccountRoleAsync(accountId);
            if (accountRole != null)
                return accountRole;
        }

        var isManager = await _context.ProjectManagers
            .AsNoTracking()
            .AnyAsync(pm => pm.EmployeeId == employeeId);

        return isManager ? Manager : Employee;
    }

    public async Task<string> GetRoleForAccountAsync(string accountId, string username)
    {
        if (IsAdminUsername(username))
            return Admin;

        var accountRole = await ReadAccountRoleAsync(accountId);
        if (accountRole != null)
            return accountRole;

        var employeeId = await _context.Employees
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .Select(e => e.EmployeeId)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(employeeId))
            return Employee;

        var isManager = await _context.ProjectManagers
            .AsNoTracking()
            .AnyAsync(pm => pm.EmployeeId == employeeId);

        return isManager ? Manager : Employee;
    }

    public async Task<List<EmployeeRoleDto>> GetEmployeeRolesAsync()
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .Include(e => e.Account)
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .ToListAsync();

        var result = new List<EmployeeRoleDto>();
        foreach (var employee in employees)
        {
            var username = employee.Account?.Username ?? string.Empty;
            result.Add(new EmployeeRoleDto
            {
                EmployeeId = employee.EmployeeId,
                FullName = $"{employee.FirstName} {employee.LastName}",
                Username = username,
                Role = await GetRoleForEmployeeAsync(employee.EmployeeId, username)
            });
        }

        return result;
    }

    private async Task<string?> ReadAccountRoleAsync(string accountId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Role FROM Account WHERE AccountId = @accountId";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@accountId";
            parameter.Value = accountId;
            command.Parameters.Add(parameter);

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var value = (await command.ExecuteScalarAsync())?.ToString();
            return NormalizeRole(value);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAdminUsername(string username) =>
        string.Equals(username, Admin, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "admin" => Admin,
        "manager" => Manager,
        "employee" => Employee,
        _ => null
    };
}
