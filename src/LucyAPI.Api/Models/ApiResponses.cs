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

public sealed class AgentPreferenceTreeResponse
{
    public string Agent { get; set; } = "";
    public List<TreeNode<Preference>> Preferences { get; set; } = [];
}

public sealed class ProjectDetailResponse
{
    public Project Project { get; set; } = null!;
    public List<TreeNode<ProjectSection>> Sections { get; set; } = [];
}

public sealed class ProjectCompactResponse
{
    public ProjectCompact Project { get; set; } = null!;
    public List<ProjectSectionCompact> Sections { get; set; } = [];
}

public sealed class NudgeListResponse
{
    public List<Nudge> Nudges { get; set; } = [];
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

public sealed class BootResponse
{
    public BootEndpointMap Endpoints { get; set; } = new();
    public string Agent { get; set; } = "";
    public List<TreeNode<AlwaysLoadItem>> AlwaysLoad { get; set; } = [];
}

public sealed class BootEndpointMap
{
    public string Time { get; set; } = "";
    public string Context { get; set; } = "";
    public string AlwaysLoad { get; set; } = "";
    public string AlwaysLoadItem { get; set; } = "";
    public string Memories { get; set; } = "";
    public string MemoryItem { get; set; } = "";
    public string Preferences { get; set; } = "";
    public string PreferenceItem { get; set; } = "";
    public string Projects { get; set; } = "";
    public string ProjectItem { get; set; } = "";
    public string ProjectSection { get; set; } = "";
    public string ProjectDocument { get; set; } = "";
    public string SessionStart { get; set; } = "";
    public string SessionLast { get; set; } = "";
    public BootSaveEndpoint Save { get; set; } = new();
}

public sealed class BootSaveEndpoint
{
    public string Url { get; set; } = "";
    public string Usage { get; set; } = "";
}
