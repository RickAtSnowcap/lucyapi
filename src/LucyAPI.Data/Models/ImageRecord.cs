namespace LucyAPI.Data.Models;

public sealed class ImageRecord
{
    public int ImageId { get; set; }
    public string Filename { get; set; } = "";
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool Keep { get; set; }
    public int? SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public sealed class ImageDeleteResult
{
    public int ImageId { get; set; }
    public string Filename { get; set; } = "";
    public bool Deleted { get; set; }
}
