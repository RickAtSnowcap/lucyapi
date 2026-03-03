namespace LucyAPI.Services.DTOs;

public sealed class TreeNode<T>
{
    public required T Data { get; init; }
    public List<TreeNode<T>> Children { get; set; } = [];
}
