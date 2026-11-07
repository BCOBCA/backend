using BCOBCA.BLL.Common;
using BCOBCA.DAL.Entities;
using Xunit;

namespace BCOBCA.Tests;

public sealed class CategoryTreeTests
{
    [Fact]
    public void ParentSelectionIncludesAllActiveDescendants()
    {
        var root=new Category { IsActive=true };
        var child=new Category { ParentId=root.Id, IsActive=true };
        var leaf=new Category { ParentId=child.Id, IsActive=true };
        var hidden=new Category { ParentId=root.Id, IsActive=false };
        var hiddenChild=new Category { ParentId=hidden.Id, IsActive=true };
        var active=CategoryTree.Active([root,child,leaf,hidden,hiddenChild]);
        Assert.Equal(3,CategoryTree.Descendants(root.Id,active).Length);
        Assert.DoesNotContain(active,x=>x.Id==hiddenChild.Id);
        Assert.Empty(CategoryTree.Descendants(Guid.NewGuid(),active));
    }

    [Fact]
    public void CyclesAndMissingParentsCannotBecomeVisible()
    {
        var a=new Category { IsActive=true };
        var b=new Category { ParentId=a.Id,IsActive=true };
        a.ParentId=b.Id;
        var orphan=new Category { ParentId=Guid.NewGuid(),IsActive=true };
        Assert.Empty(CategoryTree.Active([a,b,orphan]));
    }
}
