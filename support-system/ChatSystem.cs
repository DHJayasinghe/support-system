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

    public void AddAgents(IEnumerable<Agent> agents) => _agents.AddRange(agents);

    public void AddOverflowTeam(IEnumerable<Agent> team) => overflowTeam.AddRange(team);

    public string CreateChatSession(int userId)
    {
        CalculateCapacity();
        if (IsMaximumQueueReached() && IsOfficeHours())
            AssignOverflowTeam();

        if (IsMaximumQueueReached()) return "NOK";

        var session = new ChatSession
        {
            Id = userId,
        };
        noOfActiveSessions++;
        sessions.Enqueue(session.Id);
        ChatSessions.TryAdd(userId, session);
        UserActivityLog.TryAdd(userId, DateTime.UtcNow);
        Console.WriteLine("Queue session {0}", session.Id);

        return "OK";
    }

    private bool IsMaximumQueueReached() => maxQueueLength == noOfActiveSessions;

    private static bool IsOfficeHours()
    {
        // TODO: office hours check
        return true;
    }

    private void AssignOverflowTeam()
    {
        if (overflowTeam.Any(agent => !agent.Assigned)) return;
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

    private void AssignChatToAgent(ChatSession session)
    {
        var agentToAssign = RoundRobinAgentPick();

        if (agentToAssign != null)
        {
            agentToAssign.CurrentChats++;
            session.AssignedAgent = agentToAssign;
            Console.WriteLine("Assigining chat session {0} -> agent {1} ({2})", session.Id, session.AssignedAgent.Id, session.AssignedAgent.Seniority);
        }
        else
        {
            Console.WriteLine("No available agents to assign the chat.");
        }
    }

    private Agent RoundRobinAgentPick() => _agents
        .Where(agent => agent.IsAvailable)
        .OrderBy(agent => int.Parse($"{(int)agent.Seniority}{agent.CurrentChats}"))
        .FirstOrDefault();

    private bool AgentAreAvailable() => _agents.Any(agent => agent.IsAvailable);

    public void KeepSessionActive(int userId)
    {
        UserActivityLog.AddOrUpdate(userId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
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
        if (!sessions.Any())
        {
            //Console.WriteLine("No sessions to assign");
            return;
        }
        if (!AgentAreAvailable())
        {
            //Console.WriteLine("Agents are busy. Waiting for agents to be available");
            return;
        }
        if (!sessions.Any()) return;

        sessions.TryDequeue(out int sessionId);
        //Console.WriteLine("Assiging chat session: {0}", sessionId);
        var session = ChatSessions[sessionId];
        AssignChatToAgent(session);
    }

    private void MonitorAndRemoveInactiveSessions()
    {
        foreach (var activity in UserActivityLog)
        {
            if (IfLastActivityWas3SecsAgo(activity))
            {
                var sessionId = activity.Key;
                Console.WriteLine("Session {0} is inactive. Marking as inactive.", sessionId);
                var session = ChatSessions[sessionId];
                session.MarkInactive();
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
