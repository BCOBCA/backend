using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Interfaces;
using BCOBCA.DAL.Entities;
using BCOBCA.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BCOBCA.BLL.Services;

public sealed class PlatformFeeConfigurationService(ICommerceStore store) : IPlatformFeeConfigurationService
{
    private async Task RequireAdmin(Guid accountId, CancellationToken ct)
    {
        if (!await store.Set<Account>().AnyAsync(x => x.Id == accountId && x.Status == 1, ct)
            || !await store.Set<AccountRole>().AnyAsync(x => x.AccountId == accountId && (x.Role == "Admin" || x.Role == "SuperAdmin"), ct))
            throw new BusinessException("Cần quyền quản trị cấu hình phí sàn", 403);
    }

    private static PlatformFeeConfigurationDto Map(PlatformFeeConfiguration value)
        => new(value.PlatformFeeRate, value.Revision, value.UpdatedAt, value.UpdatedByAccountId);

    public async Task<PlatformFeeConfigurationDto> GetAsync(Guid accountId, CancellationToken ct)
    {
        await RequireAdmin(accountId, ct);
        var settings = await store.Set<PlatformFeeConfiguration>().AsNoTracking().SingleAsync(x => x.Id == PlatformFeeConfiguration.SingletonId, ct);
        return Map(settings);
    }

    public async Task<PlatformFeeConfigurationDto> UpdateAsync(Guid accountId, UpdatePlatformFeeRequest input, CancellationToken ct)
    {
        await using var tx = await store.BeginTransactionAsync(ct);
        await RequireAdmin(accountId, ct);
        PlatformFeeRules.Validate(input.PlatformFeeRate);
        if (input.ExpectedRevision is null || input.ExpectedRevision == Guid.Empty) throw new BusinessException("Vui lòng tải cấu hình hiện tại trước khi cập nhật.");
        var settings = await store.Set<PlatformFeeConfiguration>().SingleAsync(x => x.Id == PlatformFeeConfiguration.SingletonId, ct);
        if (settings.Revision != input.ExpectedRevision) throw new BusinessException("Phí sàn vừa được cập nhật. Vui lòng tải lại cấu hình.", 409);
        if (settings.PlatformFeeRate == input.PlatformFeeRate) return Map(settings);
        settings.PlatformFeeRate = input.PlatformFeeRate!.Value;
        settings.Revision = Guid.NewGuid();
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedByAccountId = accountId;
        store.Set<AuditLog>().Add(new AuditLog { AccountId = accountId, Action = "UPDATE_PLATFORM_FEE", EntityType = "PlatformFeeConfiguration", EntityId = settings.Id, CreatedAt = settings.UpdatedAt.Value });
        await store.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(settings);
    }
}
