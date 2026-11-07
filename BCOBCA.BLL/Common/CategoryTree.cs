using BCOBCA.DAL.Entities;

namespace BCOBCA.BLL.Common;

public static class CategoryTree
{
    public static IReadOnlyList<Category> Active(IEnumerable<Category> source)
    {
        var all = source.ToDictionary(x => x.Id);
        bool Visible(Category category)
        {
            var seen = new HashSet<Guid>();
            while (true)
            {
                if (!category.IsActive || !seen.Add(category.Id)) return false;
                if (category.ParentId is not { } parent) return true;
                if (!all.TryGetValue(parent, out category!)) return false;
            }
        }
        return all.Values.Where(Visible).ToArray();
    }

    public static Guid[] Descendants(Guid id, IReadOnlyList<Category> active)
    {
        if (!active.Any(x => x.Id == id)) return [];
        var result = new HashSet<Guid> { id };
        var queue = new Queue<Guid>();
        queue.Enqueue(id);
        while (queue.TryDequeue(out var parent))
            foreach (var child in active.Where(x => x.ParentId == parent))
                if (result.Add(child.Id)) queue.Enqueue(child.Id);
        return result.ToArray();
    }
}
