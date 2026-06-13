using EmployeeManagement.Entities;

namespace EmployeeManagement.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(Account account, Employee employee, string role);
}
