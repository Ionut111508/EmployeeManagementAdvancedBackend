using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmployeeManagement.Entities;
using Microsoft.IdentityModel.Tokens;

namespace EmployeeManagement.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(Account account, Employee employee, string role)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var expiresAt = DateTime.UtcNow.AddHours(8);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.AccountId),
            new Claim(ClaimTypes.NameIdentifier, account.AccountId),
            new Claim(ClaimTypes.Name, account.Username),
            new Claim(ClaimTypes.Role, role),
            new Claim("employee_id", employee.EmployeeId),
            new Claim("full_name", $"{employee.FirstName} {employee.LastName}")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiresAt, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
