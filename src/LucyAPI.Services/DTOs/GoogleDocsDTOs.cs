namespace LucyAPI.Services.DTOs;

public sealed class CreateDocRequest
{
    public string Title { get; set; } = "";
    public string? Body { get; set; }
}

public sealed class UpdateDocRequest
{
    public string Content { get; set; } = "";
}

public sealed class AppendDocRequest
{
    public string Content { get; set; } = "";
}

public sealed class CreateFolderRequest
{
    public string Name { get; set; } = "";
    public string? ParentFolderId { get; set; }
}

public sealed class MoveFileRequest
{
    public string TargetFolderId { get; set; } = "";
}

public sealed class DocResponse
{
    public string DocumentId { get; set; } = "";
    public string? Title { get; set; }
    public string? Text { get; set; }
    public string Url { get; set; } = "";
}

public sealed class DocImageAppendResponse
{
    public string DocumentId { get; set; } = "";
    public string Url { get; set; } = "";
    public int ImageId { get; set; }
    public string ImageUrl { get; set; } = "";

    /// <summary>Width sent to Docs after clamping to the text width; null = the image's natural size (it fit).</summary>
    public double? WidthPt { get; set; }
}

public sealed class DocImagesResponse
{
    public string DocumentId { get; set; } = "";
    public string? Title { get; set; }
    public string Url { get; set; } = "";
    public List<DocImageInfo> Images { get; set; } = [];
}

public sealed class DocImageInfo
{
    public string ObjectId { get; set; } = "";
    public string? ContentUri { get; set; }
    public string? SourceUri { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public double? WidthPt { get; set; }
    public double? HeightPt { get; set; }
    public int StartIndex { get; set; }
    public string? ParagraphText { get; set; }
    public string? PrecedingText { get; set; }
}

public sealed class DriveFileInfo
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? MimeType { get; set; }
    public string? ModifiedTime { get; set; }
    public string? CreatedTime { get; set; }
    public string? Size { get; set; }
    public string? WebViewLink { get; set; }
    public List<string>? Parents { get; set; }
}

public sealed class DriveFileListResponse
{
    public string FolderId { get; set; } = "";
    public List<DriveFileInfo> Files { get; set; } = [];
}

public sealed class FolderResponse
{
    public string FolderId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class MoveFileResponse
{
    public string FileId { get; set; } = "";
    public string? Name { get; set; }
    public List<string>? Parents { get; set; }
}

public sealed class DeleteFileResponse
{
    public string FileId { get; set; } = "";
    public bool Trashed { get; set; }
}
