namespace LucyAPI.Data.Models;

public sealed class ShareCreated
{
    public int ShareId { get; set; }
    public int SharedToUserId { get; set; }
    public int ObjectTypeId { get; set; }
    public int ObjectId { get; set; }
    public int PermissionLevel { get; set; }
}
