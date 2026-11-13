using BCOBCA.BLL.DTOs;
using FluentValidation;

namespace BCOBCA.BLL.Validators;

public sealed class SellerBranchValidator : AbstractValidator<SellerBranchInput>
{
    public SellerBranchValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches("^\\+?[0-9]{9,15}$");
        RuleFor(x => x.Address).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ProvinceCode).NotEmpty().Matches("^[0-9]{1,2}$");
        RuleFor(x => x.WardCode).NotEmpty().Matches("^[0-9]{1,5}$");
        RuleFor(x => x.UpdatedAt).NotEmpty();
    }
}
