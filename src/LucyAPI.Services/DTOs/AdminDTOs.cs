namespace LucyAPI.Services.DTOs;

// ── Auth ──────────────────────────────────────────────────────

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginResponse
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public sealed class UserInfoResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string? Email { get; set; }
}

public sealed class OkResponse
{
    public bool Ok { get; set; }
}

// ── Admin Agent List ──────────────────────────────────────────

public sealed class AdminAgentItem
{
    public int AgentId { get; set; }
    public string Name { get; set; } = "";
    public AdminLastSession? LastSession { get; set; }
}

public sealed class AdminLastSession
{
    public int SessionId { get; set; }
    public string? StartedAt { get; set; }
    public string? Project { get; set; }
}

public sealed class AdminAgentListResponse
{
    public List<AdminAgentItem> Agents { get; set; } = [];
}

// ── Admin Agent-scoped responses ──────────────────────────────

public sealed class AdminTreeResponse<T>
{
    public string Agent { get; set; } = "";
    public List<TreeNode<T>> Tree { get; set; } = [];
}

public sealed class AdminNodeResponse<T>
{
    public T Node { get; set; } = default!;
    public List<T> Children { get; set; } = [];
}

public sealed class AdminCreatedResponse<T>
{
    public T Created { get; set; } = default!;
}

public sealed class AdminUpdatedResponse<T>
{
    public T Updated { get; set; } = default!;
}

public sealed class AdminDeletedResponse
{
    public int Deleted { get; set; }
    public int DescendantsDeleted { get; set; }
}

public sealed class AdminDeletedIdResponse
{
    public int Deleted { get; set; }
}

public sealed class AdminDeletedKeyResponse
{
    public string Deleted { get; set; } = "";
}

// ── Admin Resource Lists ──────────────────────────────────────

public sealed class AdminMemoryListResponse
{
    public string Agent { get; set; } = "";
    public List<LucyAPI.Data.Models.Memory> Memories { get; set; } = [];
}

public sealed class AdminHandoffListResponse
{
    public string Agent { get; set; } = "";
    public List<LucyAPI.Data.Models.Handoff> Handoffs { get; set; } = [];
}

public sealed class AdminSessionLastResponse
{
    public string Agent { get; set; } = "";
    public AdminLastSession? LastSession { get; set; }
}

public sealed class AdminHandoffCreateRequest
{
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
}

// ── Admin Resource endpoints ─────────────────────────────────

public sealed class AdminProjectListResponse
{
    public List<AdminProjectItem> Projects { get; set; } = [];
}

public sealed class AdminProjectItem
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? StatusLabel { get; set; }
    public string Access { get; set; } = "owned";
    public int PermissionLevel { get; set; } = 3;
}

public sealed class AdminProjectCreateRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? StatusId { get; set; }
}

public sealed class AdminProjectUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? StatusId { get; set; }
}

public sealed class AdminSectionCreateRequest
{
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? FilePath { get; set; }
}

public sealed class AdminSectionUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? FilePath { get; set; }
}

public sealed class AdminWikiListResponse
{
    public List<AdminWikiItem> Wikis { get; set; } = [];
}

public sealed class AdminWikiItem
{
    public int WikiId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string Access { get; set; } = "owned";
    public int PermissionLevel { get; set; } = 3;
}

public sealed class AdminWikiDetailResponse
{
    public AdminWikiItem Wiki { get; set; } = null!;
    public List<AdminWikiSectionItem> Sections { get; set; } = [];
}

public sealed class AdminWikiSectionItem
{
    public int SectionId { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class AdminWikiCreateRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class AdminWikiUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public sealed class AdminWikiSectionCreateRequest
{
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed class AdminWikiSectionUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed class AdminHintListResponse
{
    public List<AdminHintItem> Hints { get; set; } = [];
}

public sealed class AdminHintItem
{
    public int HintId { get; set; }
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int HintCategoryId { get; set; }
    public string Access { get; set; } = "owned";
    public int PermissionLevel { get; set; } = 3;
}

public sealed class AdminHintCategoryCreateRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class AdminHintCreateRequest
{
    public int ParentId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class AdminHintUpdateRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public sealed class AdminSecretListResponse
{
    public List<AdminSecretKey> Secrets { get; set; } = [];
}

public sealed class AdminSecretKey
{
    public string Key { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class AdminSecretCreateRequest
{
    public string Value { get; set; } = "";
}

public sealed class AdminSecretValueResponse
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class AdminUserListResponse
{
    public List<UserInfoResponse> Users { get; set; } = [];
}

public sealed class AdminShareListResponse
{
    public List<AdminShareItem> Shares { get; set; } = [];
}

public sealed class AdminShareItem
{
    public int ShareId { get; set; }
    public int ObjectTypeId { get; set; }
    public string ObjectType { get; set; } = "";
    public int ObjectId { get; set; }
    public int? SharedToUserId { get; set; }
    public string? SharedToName { get; set; }
    public int? SharedByUserId { get; set; }
    public string? SharedByName { get; set; }
    public int PermissionLevel { get; set; }
    public string? ObjectTitle { get; set; }
}

public sealed class AdminShareCreateRequest
{
    public int SharedToUserId { get; set; }
    public int ObjectTypeId { get; set; }
    public int ObjectId { get; set; }
    public int PermissionLevel { get; set; } = 1;
}

public sealed class AdminShareUpdateRequest
{
    public int PermissionLevel { get; set; }
}

public sealed class AdminShareCreatedResponse
{
    public AdminShareCreatedItem Shared { get; set; } = null!;
}

public sealed class AdminShareCreatedItem
{
    public int ShareId { get; set; }
    public int ObjectTypeId { get; set; }
    public int ObjectId { get; set; }
    public int SharedToUserId { get; set; }
    public int PermissionLevel { get; set; }
}

public sealed class AdminRevokedResponse
{
    public int Revoked { get; set; }
}

// ── Admin Images ─────────────────────────────────────────────

public sealed class AdminGenImageRequest
{
    public string Prompt { get; set; } = "";
    public string Model { get; set; } = "nano-banana";
    public string AspectRatio { get; set; } = "1:1";
}

public sealed class AdminKeepRequest
{
    public bool Keep { get; set; }
}

// ── Dashboard ────────────────────────────────────────────────

public sealed class AdminDashboardResponse
{
    public AdminDashboardStats Stats { get; set; } = null!;
    public List<AdminRecentSession> RecentSessions { get; set; } = [];
}

public sealed class AdminDashboardStats
{
    public long Agents { get; set; }
    public long Projects { get; set; }
    public long Wikis { get; set; }
    public long HintCategories { get; set; }
    public long Secrets { get; set; }
    public long Images { get; set; }
    public long PendingHandoffs { get; set; }
    public long SharedToMe { get; set; }
}

public sealed class AdminRecentSession
{
    public int SessionId { get; set; }
    public string AgentName { get; set; } = "";
    public string? StartedAt { get; set; }
    public string? Project { get; set; }
}

public sealed class AdminProjectStatusListResponse
{
    public List<LucyAPI.Data.Models.ProjectStatus> Statuses { get; set; } = [];
}

public sealed class AdminSectionsDeletedResponse
{
    public int Deleted { get; set; }
    public int SectionsDeleted { get; set; }
}

public sealed class AdminWikiTagListResponse
{
    public int WikiId { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class AdminPickedUpResponse
{
    public AdminPickedUpItem PickedUp { get; set; } = null!;
}

public sealed class AdminPickedUpItem
{
    public int HandoffId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset? PickedUpAt { get; set; }
}

public sealed class AdminSavedResponse
{
    public AdminSavedItem Saved { get; set; } = null!;
}

public sealed class AdminSavedItem
{
    public int SecretId { get; set; }
    public string Key { get; set; } = "";
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class AdminImageDeletedResponse
{
    public bool Deleted { get; set; }
    public int ImageId { get; set; }
}

public sealed class AdminCleanupResponse
{
    public int Deleted { get; set; }
}
