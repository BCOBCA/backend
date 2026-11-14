namespace BCOBCA.BLL.DTOs;

public sealed record VerificationFileInput(string Kind, string FileName, string ContentType, byte[] Content);
public sealed record SellerVerificationInput(Guid SubmissionId, string LegalName, string TaxCode,
    string RepresentativeName, DateTimeOffset UpdatedAt, IReadOnlyList<VerificationFileInput> Files);
public sealed record VerificationDocument(string Kind, string FileName, string ContentType, long Size, string PublicId);
public sealed record VerificationDocumentDto(string Kind, string FileName, string ContentType, long Size);
public sealed record VerificationDownload(byte[] Content, string ContentType, string FileName);
