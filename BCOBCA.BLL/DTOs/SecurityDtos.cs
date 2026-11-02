namespace BCOBCA.BLL.DTOs;

public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record VerifyEmailRequest(string Token);
public sealed record SessionDto(Guid Id, string DeviceName, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
