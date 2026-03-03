namespace LucyAPI.Data.Models;

/// <summary>
/// Lightweight agent reference returned by fn_agent_get_by_name (agent_id, user_id only).
/// </summary>
public sealed class AgentRef
{
    public int AgentId { get; set; }
    public int UserId { get; set; }
}
