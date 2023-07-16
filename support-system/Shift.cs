namespace TicketSystem;

public class Shift
{
    public string Id { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Tag { get; set; } = "DAY";
    public bool IsCurrent(TimeSpan currentTime) => currentTime >= StartTime && currentTime < EndTime;

    public const string MORNING = "MORNING";
    public const string AFTERNOON = "AFTERNOON";
    public const string NIGHT = "NIGHT";
    public const string OFFICEHOURS = "OFFICEHOURS";

    public static List<Shift> All => new()
    {
        new Shift { Id = MORNING, StartTime = new(6, 0, 0), EndTime = new(14, 0, 0) },
        new Shift { Id = AFTERNOON, StartTime = new(14, 0, 0), EndTime = new(22, 0, 0) },
        new Shift { Id = NIGHT, StartTime = new(22, 0, 0), EndTime = new(6, 0, 0) },
        new Shift { Id = OFFICEHOURS, StartTime = new(9, 0, 0), EndTime = new(18, 0, 0) }
    };
}
