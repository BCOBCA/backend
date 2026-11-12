using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class WithdrawalConfigurationService(ICommerceStore store) : IWithdrawalConfigurationService
{
    private static WithdrawalConfigurationDto Map(WithdrawalConfiguration value) => new(value.FeeAmount, value.MinimumAmount, value.MaximumAmount, value.Revision, value.UpdatedAt, value.UpdatedByAccountId);
    public async Task<WithdrawalConfigurationDto> GetAsync(CancellationToken ct) => Map(await store.Set<WithdrawalConfiguration>().AsNoTracking().SingleAsync(x => x.Id == WithdrawalConfiguration.SingletonId, ct));

    public async Task<WithdrawalConfigurationDto> UpdateAsync(Guid accountId, UpdateWithdrawalConfigurationRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        if (!await store.Set<Account>().AnyAsync(a => a.Id == accountId && a.Status == 1, ct)
            || !await store.Set<AccountRole>().AnyAsync(r => r.AccountId == accountId && (r.Role == "Admin" || r.Role == "SuperAdmin"), ct)) throw new BusinessException("Cần quyền quản trị cấu hình rút tiền", 403);
        if (input.FeeAmount is null || input.MinimumAmount is null || input.MaximumAmount is null || input.ExpectedRevision is null || input.ExpectedRevision == Guid.Empty) throw new BusinessException("Vui lòng nhập đầy đủ cấu hình và phiên bản hiện tại");
        var amounts = new[] { input.FeeAmount.Value, input.MinimumAmount.Value, input.MaximumAmount.Value };
        if (amounts.Any(a => a < 0 || a > 9999999999999999.99m || decimal.Round(a, 2) != a) || input.MinimumAmount <= input.FeeAmount || input.MaximumAmount < input.MinimumAmount) throw new BusinessException("Phí và giới hạn rút tiền không hợp lệ");
        var settings = await store.Set<WithdrawalConfiguration>().SingleAsync(x => x.Id == WithdrawalConfiguration.SingletonId, ct);
        if (settings.Revision != input.ExpectedRevision) throw new BusinessException("Cấu hình rút tiền vừa thay đổi. Vui lòng tải lại.", 409);
        if (settings.FeeAmount == input.FeeAmount && settings.MinimumAmount == input.MinimumAmount && settings.MaximumAmount == input.MaximumAmount) return Map(settings);
        settings.FeeAmount = input.FeeAmount.Value; settings.MinimumAmount = input.MinimumAmount.Value; settings.MaximumAmount = input.MaximumAmount.Value;
        settings.Revision = Guid.NewGuid(); settings.UpdatedAt = DateTimeOffset.UtcNow; settings.UpdatedByAccountId = accountId;
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "UPDATE_WITHDRAWAL_CONFIGURATION", EntityType = "WithdrawalConfiguration", EntityId = settings.Id, CreatedAt = settings.UpdatedAt.Value });
        await store.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(settings);
    }
}
