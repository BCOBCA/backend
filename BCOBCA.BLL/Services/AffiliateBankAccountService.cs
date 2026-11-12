using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCOBCA.BLL.Services;

public sealed class AffiliateBankAccountService(ICommerceStore store, IConfiguration config) : IAffiliateBankAccountService
{
    private async Task RequireUser(Guid userId, CancellationToken ct)
    {
        if (!await (from user in store.Set<User>() join account in store.Set<Account>() on user.AccountId equals account.Id
            join affiliate in store.Set<AffiliateProfile>() on user.Id equals affiliate.UserId
            where user.Id == userId && account.Status == 1 && affiliate.Status == "ACTIVE" select user.Id).AnyAsync(ct)) throw new BusinessException("Affiliate chưa hoạt động", 409);
    }
    private AffiliateBankAccountDto Map(AffiliateBankAccount value) => new(value.Id, value.BankName, value.MaskedAccountNumber, BankDataProtection.Decrypt(config, value.DetailsEncrypted).AccountHolder);

    public async Task<IReadOnlyList<AffiliateBankAccountDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        await RequireUser(userId, ct);
        var items = await store.Set<AffiliateBankAccount>().AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Take(10).ToListAsync(ct);
        return items.Select(Map).ToArray();
    }

    public async Task<AffiliateBankAccountDto> AddAsync(Guid userId, BankDetailsDto input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct); await RequireUser(userId, ct);
        var details = BankDataProtection.Validate(input);
        var existing = await store.Set<AffiliateBankAccount>().Where(a => a.UserId == userId).ToListAsync(ct);
        foreach (var row in existing)
        {
            var saved = BankDataProtection.Decrypt(config, row.DetailsEncrypted);
            if (saved.BankName.Equals(details.BankName, StringComparison.OrdinalIgnoreCase) && saved.AccountNumber == details.AccountNumber) throw new BusinessException("Tài khoản ngân hàng đã được thêm", 409);
        }
        if (existing.Count >= 10) throw new BusinessException("Bạn chỉ có thể lưu tối đa 10 tài khoản ngân hàng", 409);
        var item = new AffiliateBankAccount { UserId = userId, BankName = details.BankName, MaskedAccountNumber = "****" + details.AccountNumber[^4..], DetailsEncrypted = BankDataProtection.Encrypt(config, details) };
        store.Set<AffiliateBankAccount>().Add(item); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(item);
    }
}
