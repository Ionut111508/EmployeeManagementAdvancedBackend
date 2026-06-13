namespace EmployeeManagement.Services;

public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string storedPassword, string providedPassword, out bool needsRehash);
    bool IsHashed(string storedPassword);
}
