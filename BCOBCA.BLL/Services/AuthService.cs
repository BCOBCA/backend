using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BCOBCA.BLL.Services;

public sealed class AuthService(ICommerceStore store, IConfiguration config, IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator, IValidator<UpdateProfileRequest> profileValidator) : IAuthService
{
    private readonly PasswordHasher<Account> hasher = new();
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(48));

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest input, CancellationToken ct)
    {
        input = input with { Email = (input.Email ?? "").Trim().ToLowerInvariant(), Phone = (input.Phone ?? "").Trim() };
        await registerValidator.ValidateAndThrowAsync(input, ct);
        var email = input.Email;
        if (await store.Set<Account>().IgnoreQueryFilters().AnyAsync(x => email != "" && x.Email == email || input.Phone != "" && x.Phone == input.Phone, ct))
            throw new BusinessException("Email hoặc số điện thoại đã được sử dụng", 409);
        await using var tx = await store.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var account = new Account { Email = email, Phone = input.Phone, Role = "Customer", Status = 1, CreatedAt = now, UpdatedAt = now };
        account.PasswordHash = hasher.HashPassword(account, input.Password);
        var user = new User { AccountId = account.Id, FullName = input.FullName.Trim(), ReferralCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), CreatedAt = now, UpdatedAt = now };
        if (!string.IsNullOrWhiteSpace(input.ReferralCode))
        {
            var code = input.ReferralCode.Trim().ToUpperInvariant();
            var referrer = await (from candidate in store.Set<User>() join profile in store.Set<AffiliateProfile>() on candidate.Id equals profile.UserId join owner in store.Set<Account>() on candidate.AccountId equals owner.Id
                where candidate.ReferralCode == code && profile.Status == "ACTIVE" && owner.Status == 1 && (email == "" || owner.Email != email) && (input.Phone == "" || owner.Phone != input.Phone) select candidate).SingleOrDefaultAsync(ct)
                ?? throw new BusinessException("Mã giới thiệu không hợp lệ");
            user.ReferredByUserId = referrer.Id;
            store.Set<AffiliateReferral>().Add(new AffiliateReferral { ReferrerUserId = referrer.Id, ReferredUserId = user.Id, ReferralCode = code, Status = "PENDING", CreatedAt = now });
            var referringProfile = await store.Set<AffiliateProfile>().SingleOrDefaultAsync(x => x.UserId == referrer.Id, ct);
            if (referringProfile is not null) referringProfile.TotalReferrals++;
        }
        store.Set<Account>().Add(account);
        store.Set<User>().Add(user);
        store.Set<AccountRole>().Add(new AccountRole { AccountId = account.Id, Role = "Customer" });
        store.Set<Cart>().Add(new Cart { UserId = user.Id, CreatedAt = now, UpdatedAt = now });
        var tier = await store.Set<MembershipTier>().Where(x => x.IsActive).OrderBy(x => x.SortOrder).FirstAsync(ct);
        store.Set<UserMembership>().Add(new UserMembership { UserId = user.Id, TierId = tier.Id, MemberCode = "BCA" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), JoinedAt = now });
        var affiliate = new AffiliateProfile { UserId = user.Id, AffiliateCode = user.ReferralCode, Status = "ACTIVE", JoinedAt = now };
        store.Set<AffiliateProfile>().Add(affiliate);
        store.Set<AffiliateWallet>().Add(new AffiliateWallet { AffiliateProfileId = affiliate.Id, Currency = "VND", UpdatedAt = now });
        await store.SaveChangesAsync(ct);
        var result = new RegistrationResult(await GetProfileAsync(account.Id, ct));
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest input, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(input, ct);
        await using var tx = await store.BeginTransactionAsync(ct);
        var identifier = input.Identifier.Trim().ToLowerInvariant();
        var account = await store.Set<Account>().SingleOrDefaultAsync(x => x.Email == identifier || x.Phone == identifier, ct);
        if (account is null || account.Status != 1 || hasher.VerifyHashedPassword(account, account.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
            throw new BusinessException("Thông tin đăng nhập không đúng", 401);
        account.LastLoginAt = DateTimeOffset.UtcNow;
        var result = await IssueAsync(account, Guid.NewGuid(), input.DeviceName, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) throw new BusinessException("Phiên đăng nhập không hợp lệ", 401);
        await using var tx = await store.BeginTransactionAsync(ct);
        var hash = Hash(token);
        var stored = await store.Set<RefreshToken>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new BusinessException("Phiên đăng nhập không hợp lệ", 401);
        if (stored.RevokedAt is not null)
        {
            var family = await store.Set<RefreshToken>().Where(x => x.FamilyId == stored.FamilyId && x.RevokedAt == null).ToListAsync(ct);
            foreach (var member in family) member.RevokedAt = DateTimeOffset.UtcNow;
            await store.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new BusinessException("Phiên đã bị thu hồi. Vui lòng đăng nhập lại", 401);
        }
        if (stored.ExpiresAt <= DateTimeOffset.UtcNow) throw new BusinessException("Phiên đăng nhập đã hết hạn", 401);
        var account = await store.Set<Account>().SingleOrDefaultAsync(x => x.Id == stored.AccountId && x.Status == 1, ct)
            ?? throw new BusinessException("Tài khoản không khả dụng", 401);
        stored.RevokedAt = DateTimeOffset.UtcNow;
        var result = await IssueAsync(account, stored.FamilyId, stored.DeviceName, ct);
        stored.ReplacedByTokenId = (await store.Set<RefreshToken>().SingleAsync(x => x.TokenHash == Hash(result.RefreshToken), ct)).Id;
        await store.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string token, CancellationToken ct)
    {
        var hash = Hash(token);
        var stored = await store.Set<RefreshToken>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (stored is null) return;
        var sessions = await store.Set<RefreshToken>().Where(x => x.FamilyId == stored.FamilyId && x.RevokedAt == null).ToListAsync(ct);
        foreach (var session in sessions) session.RevokedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> IssueAsync(Account account, Guid familyId, string device, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var accessMinutes = config.GetValue<int>("Jwt:AccessTokenMinutes");
        var refreshDays = config.GetValue<int>("Jwt:RefreshTokenDays");
        var roles = await store.Set<AccountRole>().Where(x => x.AccountId == account.Id).Select(x => x.Role).ToListAsync(ct);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, account.Id.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), new("sid", familyId.ToString()) };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, now.UtcDateTime, now.AddMinutes(accessMinutes).UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256));
        var refresh = NewToken();
        store.Set<RefreshToken>().Add(new RefreshToken { AccountId = account.Id, TokenHash = Hash(refresh), FamilyId = familyId, DeviceName = device, CreatedAt = now, ExpiresAt = now.AddDays(refreshDays) });
        await store.SaveChangesAsync(ct);
        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, now.AddMinutes(accessMinutes), await GetProfileAsync(account.Id, ct));
    }

    public async Task<Guid> GetUserIdAsync(Guid accountId, CancellationToken ct)
        => await store.Set<User>().Where(x => x.AccountId == accountId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new BusinessException("Không tìm thấy hồ sơ", 404);

    public async Task<ProfileDto> GetProfileAsync(Guid accountId, CancellationToken ct)
    {
        var user = await store.Set<User>().SingleOrDefaultAsync(x => x.AccountId == accountId, ct) ?? throw new BusinessException("Không tìm thấy hồ sơ", 404);
        var account = await store.Set<Account>().SingleAsync(x => x.Id == accountId && x.Status == 1, ct);
        var roles = await store.Set<AccountRole>().Where(x => x.AccountId == accountId).Select(x => x.Role).ToListAsync(ct);
        var referrerNickname = user.ReferredByUserId is { } referrerId ? await store.Set<User>()
            .Where(u => u.Id == referrerId && u.Nickname != "" && store.Set<Account>().Any(a => a.Id == u.AccountId && a.Status == 1))
            .Select(u => u.Nickname).SingleOrDefaultAsync(ct) : null;
        return new ProfileDto(user.Id, accountId, account.Email, account.Phone, user.FullName, user.AvatarUrl, user.ReferralCode, roles, user.Nickname,
            user.CoverUrl, user.Gender, user.DateOfBirth == default ? null : user.DateOfBirth, user.ReferredByUserId, referrerNickname,
            user.CreatedAt == default ? null : user.CreatedAt, user.UpdatedAt == default ? null : user.UpdatedAt, account.Status, account.EmailVerified, account.PhoneVerified);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid accountId, UpdateProfileRequest input, CancellationToken ct)
    {
        await profileValidator.ValidateAndThrowAsync(input, ct);
        var user = await store.Set<User>().SingleAsync(x => x.AccountId == accountId, ct);
        var account = await store.Set<Account>().SingleAsync(x => x.Id == accountId, ct);
        user.FullName = input.FullName.Trim(); user.Nickname = input.Nickname.Trim(); user.AvatarUrl = input.AvatarUrl; user.UpdatedAt = DateTimeOffset.UtcNow;
        if (input.CoverUrl is not null) user.CoverUrl = input.CoverUrl;
        if (input.Gender is { } gender) user.Gender = gender;
        if (input.DateOfBirth is not null) user.DateOfBirth = input.DateOfBirth == "" ? default : DateOnly.ParseExact(input.DateOfBirth, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (account.Phone != input.Phone) account.PhoneVerified = false;
        account.Phone = input.Phone; account.UpdatedAt = DateTimeOffset.UtcNow;
        await store.SaveChangesAsync(ct);
        return await GetProfileAsync(accountId, ct);
    }
}
