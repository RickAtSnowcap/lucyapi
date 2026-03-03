using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Api.Models;

public sealed class AgentAlwaysLoadListResponse
{
    public string Agent { get; set; } = "";
    public List<TreeNode<AlwaysLoadItem>> AlwaysLoad { get; set; } = [];
}

public sealed class AgentMemoryListResponse
{
    public string Agent { get; set; } = "";
    public List<Memory> Memories { get; set; } = [];
}

public sealed class AgentPreferenceListResponse
{
    public string Agent { get; set; } = "";
    public List<PreferenceTopLevel> Preferences { get; set; } = [];
}

public sealed class ProjectDetailResponse
{
    public Project Project { get; set; } = null!;
    public List<TreeNode<ProjectSection>> Sections { get; set; } = [];
}

public sealed class SecretKeyListResponse
{
    public List<string> Keys { get; set; } = [];
}

public sealed class DeleteCountResponse
{
    public int DeletedCount { get; set; }
}

public sealed class SectionsDeletedResponse
{
    public int SectionsDeleted { get; set; }
}

public sealed class KeyStatusResponse
{
    public string Key { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class StatusResponse
{
    public string Status { get; set; } = "";
}

public sealed class WikiDetailResponse
{
    public Wiki Wiki { get; set; } = null!;
    public List<TreeNode<WikiSection>> Sections { get; set; } = [];
}

public sealed class WikiTagListResponse
{
    public int WikiId { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class TagSearchResponse
{
    public string Tag { get; set; } = "";
    public List<WikiTagSearchResult> Results { get; set; } = [];
}
