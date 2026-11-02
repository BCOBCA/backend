namespace BCOBCA.DAL.Entities;

public sealed class ProductReviewImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductReviewId { get; set; }
    public string ImageUrl { get; set; } = "";
    public int SortOrder { get; set; }
}
