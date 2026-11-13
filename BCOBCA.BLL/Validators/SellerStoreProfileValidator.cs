using BCOBCA.BLL.DTOs;
using FluentValidation;

namespace BCOBCA.BLL.Validators;

public sealed class SellerStoreProfileValidator : AbstractValidator<SellerStoreProfileInput>
{
    public SellerStoreProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotNull().MaximumLength(5000);
        RuleFor(x => x.Email).NotNull().MaximumLength(255).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).NotNull().Matches("^$|^\\+?[0-9]{9,15}$");
        RuleFor(x => x.LogoUrl).NotNull().MaximumLength(2000).Must(SafeUrl);
        RuleFor(x => x.CoverUrl).NotNull().MaximumLength(2000).Must(SafeUrl);
        RuleFor(x => x.Website).NotNull().MaximumLength(255).Must(SafeUrl);
        RuleFor(x => x.Provinces).NotNull().MaximumLength(1000);
        RuleFor(x => x.CategoryIds).NotNull().Must(ids => ids is not null && ids.Length <= 100);
        RuleFor(x => x.MainBranch).NotNull().ChildRules(branch =>
        {
            branch.RuleFor(x => x.Name).NotNull().MaximumLength(200);
            branch.RuleFor(x => x.Phone).NotNull().Matches("^$|^\\+?[0-9]{9,15}$");
            branch.RuleFor(x => x.Address).NotNull().MaximumLength(1000)
                .NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Phone), ApplyConditionTo.CurrentValidator);
        });
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.UpdatedAt).NotEmpty();
    }

    private static bool SafeUrl(string? value) => value == "" || value is not null
        && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && string.IsNullOrEmpty(uri.UserInfo) && !string.IsNullOrEmpty(uri.Host);
}
