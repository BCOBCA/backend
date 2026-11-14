using BCOBCA.BLL.DTOs;
using FluentValidation;

namespace BCOBCA.BLL.Validators;

public sealed class SellerCreateProductValidator : AbstractValidator<SellerCreateProductInput>
{
    public SellerCreateProductValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.BrandId).NotEmpty(); RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotNull().MaximumLength(20000);
        RuleFor(x => x.ShortDescription).NotNull().MaximumLength(2000);
        RuleFor(x => x.Price).InclusiveBetween(1, 9007199254740991m).Must(value => value == decimal.Truncate(value));
        RuleFor(x => x.SalePrice).Must(value => value is null || value >= 1 && value <= 9007199254740991m && value == decimal.Truncate(value.Value));
        RuleFor(x => x).Must(value => value.SalePrice is null || value.SalePrice <= value.Price).WithMessage("Giá khuyến mãi không được vượt quá giá bán.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ImageUrls).NotNull().Must(values => values is not null && values.Count is >= 1 and <= 8 && values.Distinct().Count() == values.Count);
        RuleForEach(x => x.ImageUrls).Must(value => value is not null && value.Length <= 2000
            && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Host.Length > 0);
        RuleFor(x => x.ShippingWeightGrams).InclusiveBetween(1, 50000);
        RuleFor(x => x.ShippingLengthCm).InclusiveBetween(1, 200);
        RuleFor(x => x.ShippingWidthCm).InclusiveBetween(1, 200);
        RuleFor(x => x.ShippingHeightCm).InclusiveBetween(1, 200);
    }
}
