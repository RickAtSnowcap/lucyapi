namespace LucyAPI.Api.Models;

public sealed class TimeResponse
{
    public string UtcTime { get; set; } = "";
    public string MountainTime { get; set; } = "";
    public string Timezone { get; set; } = "";
    public string DayOfWeek { get; set; } = "";
}
