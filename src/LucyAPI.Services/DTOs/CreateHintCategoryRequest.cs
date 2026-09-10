namespace LucyAPI.Services.DTOs;

public sealed class CreateHintCategoryRequest
{
    public int ParentId { get; set; } = 0;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int SortOrder { get; set; } = 0;
}
