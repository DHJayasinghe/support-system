namespace TicketSystem;

public record Agent
{
    private static int _id = 1;
    public int Id { get; private set; }
    private const int MaxConcurrency = 10;
    public Seniority Seniority { get; init; }
    public int CurrentChats { get; set; }
    public bool Assigned { get; set; }
    public string Tag { get; set; } = "REGULAR";

    private int Capactiy => Convert.ToInt32(EfficiencyMultiplier * MaxConcurrency);
    public int RemainingCapactiy => Capactiy - CurrentChats;
    public bool IsAvailable => RemainingCapactiy > 0;
    public double EfficiencyMultiplier => Seniority switch
    {
        Seniority.TeamLead => 0.5,
        Seniority.Senior => 0.8,
        Seniority.MidLevel => 0.6,
        Seniority.Junior => 0.4,
        _ => 0
    };

    public Agent()
    {
        Id = _id++;
    }

    public void UnAssignChat()
    {
        CurrentChats--;
    }
    // This will decide how round robin algorithm gonna pick the agent
    public int AssignmentHashCode => int.Parse($"{(Tag == "REGULAR" ? 1 : 2)}{(int)Seniority}{CurrentChats}");
}

