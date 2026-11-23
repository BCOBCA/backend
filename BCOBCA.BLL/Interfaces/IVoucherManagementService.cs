using BCOBCA.BLL.DTOs;
namespace BCOBCA.BLL.Interfaces;

public interface IVoucherManagementService
{
    Task<IReadOnlyList<VoucherStoreOptionDto>> StoresAsync(Guid accountId, CancellationToken ct);
    Task<VoucherManagementPageDto> ListAsync(Guid accountId, Guid? storeId, VoucherManagementQuery query, bool admin, CancellationToken ct);
    Task<VoucherManagementDetailDto> DetailAsync(Guid accountId, Guid? storeId, Guid id, int page, bool admin, CancellationToken ct);
    Task<ManagedVoucherDto> SaveAsync(Guid accountId, Guid storeId, Guid? id, SellerVoucherInput input, CancellationToken ct);
    Task<ManagedVoucherDto> DuplicateAsync(Guid accountId, Guid storeId, Guid id, SellerVoucherInput input, CancellationToken ct);
    Task<ManagedVoucherDto> DisableAsync(Guid accountId, Guid? storeId, Guid id, VoucherDisableInput input, bool admin, CancellationToken ct);
}
