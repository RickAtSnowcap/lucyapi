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
