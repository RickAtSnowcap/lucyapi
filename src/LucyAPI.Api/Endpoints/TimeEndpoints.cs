using LucyAPI.Api.Models;

namespace LucyAPI.Api.Endpoints;

public static class TimeEndpoints
{
    public static void MapTimeEndpoints(this WebApplication app)
    {
        app.MapGet("/time", () =>
        {
            var utcNow = DateTimeOffset.UtcNow;
            var mountainZone = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
            var mountainNow = TimeZoneInfo.ConvertTime(utcNow, mountainZone);

            return Results.Ok(new TimeResponse
            {
                UtcTime = utcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                MountainTime = mountainNow.ToString("yyyy-MM-dd HH:mm:ss"),
                Timezone = mountainZone.IsDaylightSavingTime(mountainNow) ? "MDT" : "MST",
                DayOfWeek = mountainNow.DayOfWeek.ToString()
            });
        });
    }
}
