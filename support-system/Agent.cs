namespace TicketSystem;

public record Agent
{
    private static int _id = 1;
    public int Id = _id++;
    private const int MaxConcurrency = 10;
    public Seniority Seniority { get; init; }
    public int CurrentChats { get; set; }
    public bool Assigned { get; set; }

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
}

