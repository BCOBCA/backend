using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface ISellerVerificationService
{
    Task<SellerStoreProfileDto> SubmitAsync(Guid accountId, Guid storeId, SellerVerificationInput input, CancellationToken ct);
    Task<VerificationDownload> DownloadAsync(Guid accountId, Guid storeId, Guid verificationId, string kind, CancellationToken ct);
}
