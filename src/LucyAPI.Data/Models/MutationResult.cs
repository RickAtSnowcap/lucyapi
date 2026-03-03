namespace LucyAPI.Data.Models;

/// <summary>
/// Common return type for update operations (pkid/id + title confirmation).
/// </summary>
public sealed class MutationResult
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}
