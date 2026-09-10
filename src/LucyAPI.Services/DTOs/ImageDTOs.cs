namespace LucyAPI.Services.DTOs;

public sealed class GenImageRequest
{
    public string Prompt { get; set; } = "";
    public string Model { get; set; } = "nano-banana";
    public string AspectRatio { get; set; } = "1:1";
}

public sealed class EditImageRequest
{
    public string Prompt { get; set; } = "";
    public int? ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public string Model { get; set; } = "nano-banana";
}

public sealed class AnalyzeImageRequest
{
    public int? ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public string Prompt { get; set; } = "Describe this image in detail";
}

public sealed class KeepImageRequest
{
    public bool Keep { get; set; }
}

public sealed class ImageResponse
{
    public int ImageId { get; set; }
    public string Url { get; set; } = "";
    public string Filename { get; set; } = "";
    public string? Prompt { get; set; }
    public string? Model { get; set; }
    public bool Keep { get; set; }
    public int? SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string CreatedAt { get; set; } = "";
}

public sealed class AnalyzeImageResponse
{
    public string Text { get; set; } = "";
    public string ModelUsed { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ImageDeleteResponse
{
    public bool Deleted { get; set; }
    public int ImageId { get; set; }
    public string? Detail { get; set; }
}

public sealed class ImageCleanupResponse
{
    public int Deleted { get; set; }
}
