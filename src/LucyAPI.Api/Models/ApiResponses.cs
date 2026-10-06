using LucyAPI.Data.Models;
using LucyAPI.Services.DTOs;

namespace LucyAPI.Api.Models;

public sealed class AgentAlwaysLoadListResponse
{
    public string Agent { get; set; } = "";
    public List<TreeNode<AlwaysLoadItem>> AlwaysLoad { get; set; } = [];
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

public sealed class KeyStatusResponse
{
    public string Key { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class WikiDetailResponse
{
    public Wiki Wiki { get; set; } = null!;
    public List<TreeNode<WikiSection>> Sections { get; set; } = [];
}
