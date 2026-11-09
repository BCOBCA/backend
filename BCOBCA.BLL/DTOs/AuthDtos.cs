namespace BCOBCA.BLL.DTOs;

public sealed record RegisterRequest(string Email, string Password, string FullName, string Phone = "", string? ReferralCode = null);
public sealed record LoginRequest(string Identifier, string Password, string DeviceName = "BCOBCA");
public sealed record RefreshRequest(string? RefreshToken);
public sealed record AuthResult(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, ProfileDto User);
public sealed record RegistrationResult(ProfileDto User);
public sealed record RegistrationConfirmationDto(string Message = "Đăng ký thành công. Vui lòng đăng nhập để tiếp tục.");
public sealed record ProfileDto(Guid Id, Guid AccountId, string Email, string Phone, string FullName, string AvatarUrl, string ReferralCode, IReadOnlyList<string> Roles, string Nickname = "");
public sealed record UpdateProfileRequest(string FullName, string Phone, string Nickname = "", string AvatarUrl = "");
public sealed record AddressRequest(string RecipientName, string Phone, string ProvinceCode, string WardCode, string AddressLine, bool IsDefault);
public sealed record AddressDto(Guid Id, string RecipientName, string Phone, string ProvinceCode, string WardCode, string AddressLine, bool IsDefault);
