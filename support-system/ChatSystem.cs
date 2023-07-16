using System.Collections.Concurrent;

namespace TicketSystem;

public class ChatSystem
{
    private readonly ConcurrentDictionary<int, DateTime> UserActivityLog = new();
    private readonly ConcurrentDictionary<int, ChatSession> ChatSessions = new();
    private readonly ConcurrentQueue<int> sessions = new();
    private readonly List<Agent> _agents = new();
    private readonly List<Agent> overflowTeam = new();
    private int maxQueueLength = 0;
    private int noOfActiveSessions = 0;
    private int noOfSessionsWaiting = 0;

    public void AddAgents(IEnumerable<Agent> agents) => _agents.AddRange(agents);

    public void AddOverflowTeam(IEnumerable<Agent> team)
    {
        team.ToList().ForEach(agent => agent.Tag = "OVERFLOW");
        overflowTeam.AddRange(team);
    }

    public string CreateChatSession(int userId)
    {
        CalculateCapacity();
        if (IsMaximumQueueReached() && IsOfficeHours()) AssignOverflowTeam();

        if (IsMaximumQueueReached()) return "NOK";

        var session = new ChatSession
        {
            Id = userId,
        };
        AddToSessionQueue(userId, session);

        return "OK";
    }

    private void AddToSessionQueue(int userId, ChatSession session)
    {
        noOfActiveSessions++;
        sessions.Enqueue(session.Id);
        ChatSessions.TryAdd(userId, session);
        UserActivityLog.TryAdd(userId, DateTime.UtcNow);
    }

    private bool IsMaximumQueueReached() => maxQueueLength == noOfActiveSessions;

    private static bool IsOfficeHours()
    {
        var officeStartTime = new TimeSpan(8, 30, 0);  // 09:00 AM
        var officeEndTime = new TimeSpan(18, 0, 0); // 06:00 PM

        var currentTime = DateTime.Now; // Get the current local time
        var officeStartDateTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, officeStartTime.Hours, officeStartTime.Minutes, officeStartTime.Seconds);
        var officeEndDateTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, officeEndTime.Hours, officeEndTime.Minutes, officeEndTime.Seconds);

        var isOfficeHours = currentTime >= officeStartDateTime && currentTime < officeEndDateTime;
        Console.WriteLine("Office hours: {0}", isOfficeHours);
        return isOfficeHours;
    }

    private void AssignOverflowTeam()
    {
        if (!overflowTeam.Any() || !overflowTeam.Any(agent => !agent.Assigned)) return;
        Console.WriteLine("Assigning overflow team...");

        _agents.AddRange(overflowTeam);
        overflowTeam.ForEach(agent => agent.Assigned = true);
        CalculateCapacity();
    }

    private void UnAssignOverflowTeam()
    {
        if (overflowTeam.Any(agent => agent.Assigned)) return;
        Console.WriteLine("Assigning overflow team...");
        overflowTeam.ForEach(agent =>
        {
            _agents.Remove(agent);
            agent.Assigned = false;
        });
        CalculateCapacity();
    }

    private void AssignChatToAgent(int sessionId)
    {
        var session = ChatSessions[sessionId];
        var agentToAssign = RoundRobinAgentPick();

        if (agentToAssign != null)
        {
            agentToAssign.CurrentChats++;
            session.AssignedAgent = agentToAssign;
            Console.WriteLine("Session {0} -> Assigning agent {1} ({2}/{3})", session.Id, session.AssignedAgent.Id, session.AssignedAgent.Seniority, session.AssignedAgent.Tag);
        }
        else
        {
            Console.WriteLine("No available agents to assign the chat.");
        }
    }

    private Agent RoundRobinAgentPick() => _agents
        .Where(agent => agent.IsAvailable)
        .OrderBy(agent => agent.AssignmentHashCode)
        .FirstOrDefault();

    private bool AgentAreAvailable() => _agents.Any(agent => agent.IsAvailable);

    public string KeepSessionActive(int userId)
    {
        UserActivityLog.AddOrUpdate(userId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
        ChatSessions.TryGetValue(userId, out ChatSession session);
        if (session.AssignedAgent != null) return "ACTIVE";
        return "WAITING";
    }
    public void StartBackgroundTasks()
    {
        while (true)
        {
            MonitorAndAssignToAvailableAgents();
            MonitorAndRemoveInactiveSessions();
            Thread.Sleep(1000);
        }
    }
    private void MonitorAndAssignToAvailableAgents()
    {
        if (!sessions.Any()) return;  // No sessions to assign
        while (AgentAreAvailable())
        {
            if (sessions.TryDequeue(out int sessionId)) AssignChatToAgent(sessionId);
        }
        if (sessions.Any() && noOfSessionsWaiting != sessions.Count)
        {
            noOfSessionsWaiting = sessions.Count;
            Console.WriteLine("{0} sessions waiting on the queue", noOfSessionsWaiting);
        }
    }

    private void MonitorAndRemoveInactiveSessions()
    {
        foreach (var activity in UserActivityLog)
        {
            if (IfLastActivityWas3SecsAgo(activity))
            {
                var sessionId = activity.Key;
                Console.WriteLine("Session {0} (Inactive) -> Ending chat", sessionId);
                var session = ChatSessions[sessionId];
                session.MarkInactive();
                session.AssignedAgent.UnAssignChat();
                UserActivityLog.TryRemove(sessionId, out _);
                noOfActiveSessions--;
            }
        }
    }

    private static bool IfLastActivityWas3SecsAgo(KeyValuePair<int, DateTime> activity)
    {
        return DateTime.UtcNow.Subtract(activity.Value).Seconds > 3;
    }

    private void CalculateCapacity()
    {
        //Console.WriteLine("Capacity re-calculating...");
        maxQueueLength = (int)(_agents.Sum(agent => agent.RemainingCapactiy) * 1.5);
        //Console.WriteLine("Capacity is {0}", maxQueueLength);
    }
}
