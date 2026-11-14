using BCOBCA.BLL.DTOs;
using FluentValidation;
using System.Text.RegularExpressions;

namespace BCOBCA.BLL.Validators;

public sealed class SellerCreateProductValidator : AbstractValidator<SellerCreateProductInput>
{
    public SellerCreateProductValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.BrandId).NotEmpty().When(x => !x.UseOtherBrand);
        RuleFor(x => x.BrandId).Empty().When(x => x.UseOtherBrand);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).NotNull().MaximumLength(20000);
        RuleFor(x => x.ShortDescription).NotNull().MaximumLength(2000);
        RuleFor(x => x.Price).InclusiveBetween(1, 9007199254740991m).Must(value => value == decimal.Truncate(value)).When(x => x.Variants is null);
        RuleFor(x => x.SalePrice).Must(value => value is null || value >= 1 && value <= 9007199254740991m && value == decimal.Truncate(value.Value)).When(x => x.Variants is null);
        RuleFor(x => x).Must(value => value.SalePrice is null || value.SalePrice <= value.Price).WithMessage("Giá khuyến mãi không được vượt quá giá bán.").When(x => x.Variants is null);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0).When(x => x.Variants is null);
        RuleFor(x => x.ImageUrls).NotNull().Must(values => values is not null && values.Count is >= 1 and <= 5 && values.Distinct().Count() == values.Count);
        RuleForEach(x => x.ImageUrls).Must(value => value is not null && value.Length <= 2000
            && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Host.Length > 0);
        RuleFor(x => x.ShippingWeightGrams).InclusiveBetween(1, 50000);
        RuleFor(x => x.ShippingLengthCm).InclusiveBetween(1, 200);
        RuleFor(x => x.ShippingWidthCm).InclusiveBetween(1, 200);
        RuleFor(x => x.ShippingHeightCm).InclusiveBetween(1, 200);
        RuleFor(x => x.SpecificationText).MaximumLength(20000);
        RuleFor(x => x).Must(x => x.SpecificationText is null || x.Specifications is null || x.Specifications.Count == 0)
            .WithMessage("Chỉ gửi một kiểu thông số sản phẩm.");
        When(x => x.Variants is not null, () => {
            RuleFor(x => x.SpecificationText).NotNull();
            RuleFor(x => x.Variants).Must(values => values is not null && values.Count is >= 1 and <= 100 && values.All(v => v is not null)
                && values.Sum(v => (long)v.Stock) <= int.MaxValue
                && values.Select(v => Regex.Replace(v.Name?.Trim() ?? "", @"\s+", " ").Normalize().ToUpperInvariant()).Distinct().Count() == values.Count)
                .WithMessage("Cần 1–100 phân loại không trùng tên; tổng tồn kho không vượt quá 2.147.483.647.");
            RuleForEach(x => x.Variants).SetValidator(new SellerCreateVariantValidator());
        });
    }
}

public sealed class SellerCreateVariantValidator : AbstractValidator<SellerCreateVariantInput>
{
    public SellerCreateVariantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).Must(name => name is not null && !name.Any(char.IsControl));
        RuleFor(x => x.Price).InclusiveBetween(1, 9007199254740991m).Must(value => value == decimal.Truncate(value));
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalePrice).Must(value => value is null || value >= 1 && value == decimal.Truncate(value.Value));
        RuleFor(x => x).Must(x => x.SalePrice is null || x.SalePrice <= x.Price).WithMessage("Giá khuyến mãi vượt giá bán của phân loại.");
    }
}
