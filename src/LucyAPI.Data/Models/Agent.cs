namespace LucyAPI.Data.Models;

public sealed class Agent
{
    public int AgentId { get; set; }
    public string AgentName { get; set; } = "";
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
}
