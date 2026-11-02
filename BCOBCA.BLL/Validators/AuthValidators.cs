using BCOBCA.BLL.DTOs;
using FluentValidation;

namespace BCOBCA.BLL.Validators;

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).MinimumLength(12).MaximumLength(128).Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(20).Matches("^$|^\\+?[0-9]{9,15}$");
        RuleFor(x => x.ReferralCode).MaximumLength(40);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DeviceName).MaximumLength(150);
    }
}

public sealed class ProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public ProfileValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(20).Matches("^$|^\\+?[0-9]{9,15}$");
        RuleFor(x => x.Nickname).MaximumLength(100);
        RuleFor(x => x.AvatarUrl).MaximumLength(2000).Must(x => x == "" || Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https");
    }
}

public sealed class AddressValidator : AbstractValidator<AddressRequest>
{
    public AddressValidator()
    {
        RuleFor(x => x.RecipientName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).Matches("^\\+?[0-9]{9,15}$");
        RuleFor(x => x.ProvinceCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.WardCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(1000);
    }
}
