using FacilityRealtime.Domain.Enums;

namespace FacilityRealtime.Domain.Entities;

/// <summary>CONTEXT.md, Round Window, in Asia/Bangkok wall-clock time. A night window may end after midnight (EndTime &lt; StartTime).</summary>
public class PointRoundWindow
{
    public int Id { get; set; }
    public int ServicePointId { get; set; }
    public Shift Shift { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTime CreatedAt { get; set; }

    public ServicePoint? ServicePoint { get; set; }
}
