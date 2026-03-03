namespace LucyAPI.Data.Models;

public sealed class ShareRecord
{
    public int ShareId { get; set; }
    public int SharedUserId { get; set; }
    public string SharedUsername { get; set; } = "";
    public int ObjectTypeId { get; set; }
    public string ObjectTypeName { get; set; } = "";
    public int ObjectId { get; set; }
    public int PermissionLevel { get; set; }
}
