using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAccountSecurityService
{
    Task ForgotAsync(ForgotPasswordRequest input, CancellationToken ct);
    Task ResetAsync(ResetPasswordRequest input, CancellationToken ct);
    Task ChangeAsync(Guid accountId, ChangePasswordRequest input, CancellationToken ct);
    Task SendVerificationAsync(Guid accountId, CancellationToken ct);
    Task VerifyAsync(VerifyEmailRequest input, CancellationToken ct);
    Task<IReadOnlyList<SessionDto>> SessionsAsync(Guid accountId, CancellationToken ct);
    Task RevokeAsync(Guid accountId, Guid sessionId, CancellationToken ct);
}
