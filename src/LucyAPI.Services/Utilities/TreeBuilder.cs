using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Utilities;

public static class TreeBuilder
{
    /// <summary>
    /// Builds a tree from a flat list using parent_id references.
    /// Items with parentId == rootParentId become root nodes.
    /// </summary>
    public static List<TreeNode<T>> Build<T>(
        IEnumerable<T> items,
        Func<T, int> getId,
        Func<T, int> getParentId,
        int rootParentId = 0)
    {
        var lookup = new Dictionary<int, TreeNode<T>>();
        var roots = new List<TreeNode<T>>();

        foreach (var item in items)
        {
            lookup[getId(item)] = new TreeNode<T> { Data = item };
        }

        foreach (var item in items)
        {
            var node = lookup[getId(item)];
            var parentId = getParentId(item);

            if (parentId == rootParentId)
            {
                roots.Add(node);
            }
            else if (lookup.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        return roots;
    }
}
