using BCOBCA.BLL.DTOs;

namespace BCOBCA.BLL.Interfaces;

public interface IAuthService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest input, CancellationToken ct);
    Task<RegistrationResult> RegisterSellerAsync(RegisterRequest input, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest input, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task<ProfileDto> GetProfileAsync(Guid accountId, CancellationToken ct);
    Task<ProfileDto> UpdateProfileAsync(Guid accountId, UpdateProfileRequest input, CancellationToken ct);
    Task<Guid> GetUserIdAsync(Guid accountId, CancellationToken ct);
}
