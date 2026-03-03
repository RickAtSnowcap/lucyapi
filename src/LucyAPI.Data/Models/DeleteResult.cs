namespace LucyAPI.Data.Models;

/// <summary>
/// Common return type for delete operations (deleted_count or sections_deleted).
/// </summary>
public sealed class DeleteResult
{
    public int DeletedCount { get; set; }
}
