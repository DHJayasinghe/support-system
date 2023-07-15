namespace TicketSystem;

public class ChatSession
{
    public int Id { get; init; }
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public Agent AssignedAgent { get; set; }
    public bool IsActive { get; set; } = true;
}

