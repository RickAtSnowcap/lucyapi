using LucyAPI.Services.DTOs;

namespace LucyAPI.Services.Interfaces;

public interface IGoogleDocsService
{
    Task<DocResponse> CreateDocumentAsync(int userId, string title, string? bodyText, CancellationToken ct = default);
    Task<DocResponse> ReadDocumentAsync(int userId, string documentId, CancellationToken ct = default);
    Task<DocResponse> UpdateDocumentAsync(int userId, string documentId, string content, CancellationToken ct = default);
    Task<DocResponse> AppendToDocumentAsync(int userId, string documentId, string content, CancellationToken ct = default);
    Task<DriveFileListResponse> ListFilesAsync(int userId, string? folderId, CancellationToken ct = default);
    Task<FolderResponse> CreateFolderAsync(int userId, string name, string? parentFolderId, CancellationToken ct = default);
    Task<MoveFileResponse> MoveFileAsync(int userId, string fileId, string targetFolderId, CancellationToken ct = default);
    Task<DeleteFileResponse> DeleteFileAsync(int userId, string fileId, CancellationToken ct = default);
    Task<DriveFileInfo> GetFileMetadataAsync(int userId, string fileId, CancellationToken ct = default);
}
