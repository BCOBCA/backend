using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.API.Extensions;

public static class AdminProvisioning
{
    public static async Task ProvisionFirstSuperAdminAsync(this WebApplication app, CancellationToken ct)
    {
        using var scope = app.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICommerceStore>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var config = app.Configuration;
        if (await store.Set<AccountRole>().AnyAsync(x => x.Role == "SuperAdmin", ct)) throw new InvalidOperationException("A SuperAdmin already exists. Use the authorized role-management API.");
        var email = config["Bootstrap:Email"] ?? throw new InvalidOperationException("Configure Bootstrap__Email in the environment.");
        var password = config["Bootstrap:Password"] ?? throw new InvalidOperationException("Configure Bootstrap__Password in the environment.");
        var fullName = config["Bootstrap:FullName"] ?? throw new InvalidOperationException("Configure Bootstrap__FullName in the environment.");
        var exists = await store.Set<Account>().AnyAsync(x => x.Email == email.Trim().ToLowerInvariant(), ct);
        var session = exists
            ? await auth.LoginAsync(new LoginRequest(email, password, "CLI provisioning"), ct)
            : await auth.RegisterAsync(new RegisterRequest(email, password, fullName), ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        if (await store.Set<AccountRole>().AnyAsync(x => x.Role == "SuperAdmin", ct)) throw new InvalidOperationException("A SuperAdmin was provisioned concurrently. Sign in with that account.");
        store.Set<AccountRole>().Add(new AccountRole { AccountId = session.User.AccountId, Role = "SuperAdmin" });
        store.Set<AuditLog>().Add(new AuditLog { AccountId = session.User.AccountId, Action = "PROVISION_FIRST_SUPERADMIN", EntityType = "Account", EntityId = session.User.AccountId, CreatedAt = DateTimeOffset.UtcNow });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await auth.LogoutAsync(session.RefreshToken, ct);
        app.Logger.LogInformation("First SuperAdmin provisioned. Sign in to obtain a new session with the assigned role.");
    }
}
