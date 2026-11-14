using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IVerificationDocumentStorage
{
    Task UploadAsync(string publicId, VerificationFileInput file, CancellationToken ct);
    Task DeleteAsync(string publicId, CancellationToken ct);
    Task<byte[]> ReadAsync(VerificationDocument document, CancellationToken ct);
}
