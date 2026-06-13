using EmployeeManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Services;

public static class AccountPasswordMigration
{
    public static async Task MigrateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var accounts = await context.Accounts.ToListAsync();
        var changed = false;

        foreach (var account in accounts.Where(a => !passwordService.IsHashed(a.Password)))
        {
            account.Password = passwordService.HashPassword(account.Password);
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync();
    }
}
