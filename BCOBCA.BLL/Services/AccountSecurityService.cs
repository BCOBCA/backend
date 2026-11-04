using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class AccountSecurityService(ICommerceStore store, IConfiguration config) : IAccountSecurityService
{
    private readonly PasswordHasher<Account> hasher = new();
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static void ValidatePassword(string value)
    {
        if (!Regex.IsMatch(value, "^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9]).{12,128}$")) throw new BusinessException("Mật khẩu cần 12–128 ký tự, có chữ hoa, chữ thường và số");
    }

    private void RequireMailConfiguration()
    {
        if (string.IsNullOrWhiteSpace(config["Smtp:Host"]) || string.IsNullOrWhiteSpace(config["Smtp:From"]) || config.GetValue<int>("Smtp:Port") <= 0 || !Uri.TryCreate(config["App:PublicUrl"], UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
            throw new BusinessException("Dịch vụ email chưa được cấu hình", 503);
    }

    public async Task ForgotAsync(ForgotPasswordRequest input, CancellationToken ct)
    {
        if (input.Email.Length > 255 || !MailAddress.TryCreate(input.Email, out _)) throw new BusinessException("Email không hợp lệ");
        RequireMailConfiguration();
        var email = input.Email.Trim().ToLowerInvariant();
        var account = await store.Set<Account>().SingleOrDefaultAsync(x => x.Email == email && x.Status == 1, ct);
        if (account is not null) await SendAsync(account, "RESET_PASSWORD", "reset-password", ct);
    }

    public async Task SendVerificationAsync(Guid accountId, CancellationToken ct)
    {
        var account = await store.Set<Account>().SingleAsync(x => x.Id == accountId, ct);
        if (string.IsNullOrWhiteSpace(account.Email)) throw new BusinessException("Tài khoản chưa có email để xác minh");
        RequireMailConfiguration();
        if (!account.EmailVerified) await SendAsync(account, "VERIFY_EMAIL", "verify-email", ct);
    }

    private async Task SendAsync(Account account, string action, string path, CancellationToken ct)
    {
        var lifetime = config.GetValue<int>("Auth:ActionTokenMinutes");
        if (lifetime <= 0) throw new BusinessException("Chưa cấu hình thời hạn liên kết xác thực", 503);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var link = config["App:PublicUrl"]!.TrimEnd('/') + "/" + path + "?token=" + Uri.EscapeDataString(token);
        var row = new AccountActionToken { AccountId = account.Id, TokenHash = Hash(token), Action = action, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(lifetime) };
        store.Set<AccountActionToken>().Add(row);
        await store.SaveChangesAsync(ct);
        using var mail = new MailMessage(config["Smtp:From"]!, account.Email)
        {
            Subject = action == "VERIFY_EMAIL" ? "BCOBCA — Xác minh email" : "BCOBCA — Đặt lại mật khẩu",
            Body = $"Vui lòng mở liên kết sau trong {lifetime} phút:\n{link}\nNếu bạn không yêu cầu, hãy bỏ qua email này.", IsBodyHtml = false
        };
        using var smtp = new SmtpClient(config["Smtp:Host"], config.GetValue<int>("Smtp:Port")) { EnableSsl = config.GetValue<bool>("Smtp:EnableSsl") };
        if (!string.IsNullOrWhiteSpace(config["Smtp:Username"])) smtp.Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]);
        try { await smtp.SendMailAsync(mail, ct); }
        catch (SmtpException) { throw new BusinessException("Không thể gửi email lúc này. Vui lòng thử lại sau", 503); }
    }

    private async Task<AccountActionToken> ActionToken(string token, string action, CancellationToken ct)
    {
        if (token.Length != 64) throw new BusinessException("Liên kết không hợp lệ hoặc đã hết hạn");
        var hash = Hash(token);
        return await store.Set<AccountActionToken>().SingleOrDefaultAsync(x => x.TokenHash == hash && x.Action == action && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, ct)
            ?? throw new BusinessException("Liên kết không hợp lệ hoặc đã hết hạn");
    }

    public async Task ResetAsync(ResetPasswordRequest input, CancellationToken ct)
    {
        ValidatePassword(input.NewPassword);
        await using var tx = await store.BeginTransactionAsync(ct);
        var token = await ActionToken(input.Token, "RESET_PASSWORD", ct);
        var account = await store.Set<Account>().SingleAsync(x => x.Id == token.AccountId && x.Status == 1, ct);
        account.PasswordHash = hasher.HashPassword(account, input.NewPassword); account.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeAllAsync(account.Id, ct);
        foreach (var outstanding in await store.Set<AccountActionToken>().Where(x => x.AccountId == account.Id && x.Action == "RESET_PASSWORD" && x.UsedAt == null).ToListAsync(ct)) outstanding.UsedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task ChangeAsync(Guid accountId, ChangePasswordRequest input, CancellationToken ct)
    {
        ValidatePassword(input.NewPassword);
        if (input.CurrentPassword.Length > 128) throw new BusinessException("Mật khẩu không hợp lệ");
        await using var tx = await store.BeginTransactionAsync(ct);
        var account = await store.Set<Account>().SingleAsync(x => x.Id == accountId, ct);
        if (hasher.VerifyHashedPassword(account, account.PasswordHash, input.CurrentPassword) == PasswordVerificationResult.Failed) throw new BusinessException("Mật khẩu hiện tại không đúng");
        account.PasswordHash = hasher.HashPassword(account, input.NewPassword); account.UpdatedAt = DateTimeOffset.UtcNow;
        await RevokeAllAsync(accountId, ct);
        foreach (var token in await store.Set<AccountActionToken>().Where(x => x.AccountId == accountId && x.Action == "RESET_PASSWORD" && x.UsedAt == null).ToListAsync(ct)) token.UsedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task RevokeAllAsync(Guid accountId, CancellationToken ct)
    {
        foreach (var token in await store.Set<RefreshToken>().Where(x => x.AccountId == accountId && x.RevokedAt == null).ToListAsync(ct)) token.RevokedAt = DateTimeOffset.UtcNow;
    }

    public async Task VerifyAsync(VerifyEmailRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        var token = await ActionToken(input.Token, "VERIFY_EMAIL", ct);
        var account = await store.Set<Account>().SingleAsync(x => x.Id == token.AccountId, ct);
        account.EmailVerified = true; account.UpdatedAt = DateTimeOffset.UtcNow; token.UsedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<SessionDto>> SessionsAsync(Guid accountId, CancellationToken ct)
        => await store.Set<RefreshToken>().AsNoTracking().Where(x => x.AccountId == accountId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow).OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new SessionDto(x.Id, x.DeviceName, x.CreatedAt, x.ExpiresAt)).ToListAsync(ct);

    public async Task RevokeAsync(Guid accountId, Guid sessionId, CancellationToken ct)
    {
        var session = await store.Set<RefreshToken>().SingleOrDefaultAsync(x => x.Id == sessionId && x.AccountId == accountId, ct) ?? throw new BusinessException("Không tìm thấy phiên đăng nhập", 404);
        foreach (var token in await store.Set<RefreshToken>().Where(x => x.FamilyId == session.FamilyId && x.RevokedAt == null).ToListAsync(ct)) token.RevokedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct);
    }
}
