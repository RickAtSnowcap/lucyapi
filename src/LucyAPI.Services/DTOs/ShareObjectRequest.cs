namespace LucyAPI.Services.DTOs;

public sealed class ShareObjectRequest
{
    public int SharedToUserId { get; set; }
    public int ObjectTypeId { get; set; }
    public int ObjectId { get; set; }
    public int PermissionLevel { get; set; } = 1;
}
