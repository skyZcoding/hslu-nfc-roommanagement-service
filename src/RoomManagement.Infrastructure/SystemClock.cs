using RoomManagement.Application.Common;

namespace RoomManagement.Infrastructure;

internal sealed class SystemClock : IClock
{
    private const long TicksPerMicrosecond = 10;

    // PostgreSQL stores microseconds. Cutting the clock to that precision makes a timestamp that is read back
    // equal to the one that was handed out (for example in the response that created it).
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TicksPerMicrosecond));
        }
    }
}
