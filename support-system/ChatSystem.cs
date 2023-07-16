using supportSystem;
using System.Collections.Concurrent;

namespace TicketSystem;

public class ChatSystem
{
    public const double QUEUELENGTHMULTIPLIER = 1.5;
    private readonly ConcurrentDictionary<int, DateTime> UserActivityLog = new();
    public readonly ConcurrentDictionary<int, ChatSession> ChatSessions = new();
    private readonly ConcurrentQueue<int> sessions = new();
    private readonly List<Agent> _agents = new();
    private readonly List<Shift> _shifts = Shift.All;
    private int maxQueueLength = 0;
    private int noOfActiveSessions = 0;
    private int noOfSessionsWaiting = 0;
    private bool overflowTeamEnabled = false;
    private DateTimeProviderContext dateTimeProviderContext = null;

    public void AddTeam(Agent agent) => AddTeam(new List<Agent> { agent });
    public void AddTeam(IEnumerable<Agent> agents) => _agents.AddRange(agents);
    public void AddOverflowTeam(IEnumerable<Agent> team)
    {
        team.ToList().ForEach(agent => agent.Tag = "OVERFLOW");
        _agents.AddRange(team);
    }

    public string CreateChatSession(int userId)
    {
        CalculateCapacity();
        if (IsMaximumQueueReached() && IsOfficeHours()) AssignOverflowTeam();

        if (IsMaximumQueueReached()) return "NOK";

        var session = new ChatSession
        {
            Id = userId
        };
        AddToSessionQueue(userId, session);

        return "OK";
    }
    public string PollChatSession(int userId)
    {
        var currentUtcTime = GetCurrentUtcTime();
        UserActivityLog.AddOrUpdate(userId, currentUtcTime, (_, _) => currentUtcTime);
        ChatSessions.TryGetValue(userId, out ChatSession session);
        if (session.AssignedAgent != null) return "ACTIVE";
        return "WAITING";
    }

    private void AddToSessionQueue(int userId, ChatSession session)
    {
        noOfActiveSessions++;
        sessions.Enqueue(session.Id);
        ChatSessions.TryAdd(userId, session);
        UserActivityLog.TryAdd(userId, GetCurrentUtcTime());
    }
    private bool IsMaximumQueueReached() => maxQueueLength == noOfActiveSessions;
    private bool IsOfficeHours()
    {
        var officeHourShift = _shifts.First(shift => shift.Id == Shift.OFFICEHOURS);
        var currentLocalDateTime = GetCurrentLocalTime();
        var officeStartDateTime = new DateTime(currentLocalDateTime.Year, currentLocalDateTime.Month, currentLocalDateTime.Day, officeHourShift.StartTime.Hours, officeHourShift.StartTime.Minutes, officeHourShift.StartTime.Seconds);
        var officeEndDateTime = new DateTime(currentLocalDateTime.Year, currentLocalDateTime.Month, currentLocalDateTime.Day, officeHourShift.EndTime.Hours, officeHourShift.EndTime.Minutes, officeHourShift.EndTime.Seconds);

        var isOfficeHours = currentLocalDateTime >= officeStartDateTime && currentLocalDateTime < officeEndDateTime;
        return isOfficeHours;
    }
    private void AssignOverflowTeam()
    {
        if (overflowTeamEnabled) return;
        Console.WriteLine("Enabling overflow team...");

        overflowTeamEnabled = true;
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
    private Agent RoundRobinAgentPick() => ActiveAgentSelectionQuery()
        .Where(agent => agent.IsAvailable)
        .OrderBy(agent => agent.AssignmentHashCode)
        .FirstOrDefault();
    private bool AgentAreAvailable() => ActiveAgentSelectionQuery().Any(agent => agent.IsAvailable);

    private void MonitorAndUnAssignOverflowTeam(object state)
    {
        if (overflowTeamEnabled)
        {
            Console.WriteLine("Disabling overflow team...");
            overflowTeamEnabled = false;
            CalculateCapacity();
        }

        // Reschedule the timer for the next day
        DateTime nextDay = GetCurrentUtcTime().AddDays(1);
        var officeHourShift = _shifts.First(shift => shift.Id == Shift.OFFICEHOURS);
        var officeEndDateTime = new DateTime(nextDay.Year, nextDay.Month, nextDay.Day, officeHourShift.EndTime.Hours, officeHourShift.EndTime.Minutes, officeHourShift.EndTime.Seconds);
        var timeUntilExecution = officeEndDateTime - GetCurrentUtcTime();
        var timer = new Timer(MonitorAndUnAssignOverflowTeam, null, timeUntilExecution, Timeout.InfiniteTimeSpan);
    }
    private void MonitorAndAssignToAvailableAgents()
    {
        while (true)
        {
            if (!sessions.Any()) return;  // No sessions to assign
            while (AgentAreAvailable() && sessions.Any())
                if (sessions.TryDequeue(out int sessionId)) AssignChatToAgent(sessionId);
            if (sessions.Any() && noOfSessionsWaiting != sessions.Count)
            {
                noOfSessionsWaiting = sessions.Count;
                Console.WriteLine("{0} sessions waiting on the queue", noOfSessionsWaiting);
            }
            Thread.Sleep(1000);
        }
    }
    private void MonitorAndRemoveInactiveSessions()
    {
        while (true)
        {
            foreach (var activity in UserActivityLog)
            {
                if (!IfLastActivityWas3SecsAgo(activity)) continue;

                var sessionId = activity.Key;
                Console.WriteLine("Session {0} (Inactive) -> Ending chat", sessionId);
                var session = ChatSessions[sessionId];
                session.MarkInactive();
                if (session.AssignedAgent != null) session.AssignedAgent.UnAssignChat();
                UserActivityLog.TryRemove(sessionId, out _);
                noOfActiveSessions--;
            }
            Thread.Sleep(1000);
        }
    }
    
    private IEnumerable<Agent> ActiveAgentSelectionQuery()
    {
        var currentTime = GetCurrentLocalTime();
        var timespan = new TimeSpan(currentTime.Hour, currentTime.Minute, currentTime.Second);
        return _agents.Join(_shifts, agent => agent.ShiftId, shift => shift.Id, (agent, shift) => (agent, shift))
        .Where(d => d.shift.IsCurrent(timespan))
        .Select(d => d.agent)
        .Where(agent => overflowTeamEnabled ? agent.Tag == agent.Tag : agent.Tag != "OVERFLOW");
    }
    private bool IfLastActivityWas3SecsAgo(KeyValuePair<int, DateTime> activity) => GetCurrentUtcTime().Subtract(activity.Value).Seconds > 3;
    private void CalculateCapacity() => maxQueueLength = (int)(ActiveAgentSelectionQuery().Sum(agent => agent.RemainingCapactiy) * QUEUELENGTHMULTIPLIER);
    private DateTime GetCurrentLocalTime() => dateTimeProviderContext?.ContextDateTimeNow.ToLocalTime() ?? DateTimeProvider.Now;
    private DateTime GetCurrentUtcTime() => dateTimeProviderContext?.ContextDateTimeNow ?? DateTimeProvider.UtcNow;

    public List<Agent> GetActiveAgents() => ActiveAgentSelectionQuery().ToList();
    public void ChangeDateTimeProviderContext(DateTimeProviderContext context) => dateTimeProviderContext = context;
    public void StartBackgroundTasks()
    {
        var currentTime = GetCurrentLocalTime();
        var officeHourShift = _shifts.First(shift => shift.Id == Shift.OFFICEHOURS);
        var officeEndDateTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, officeHourShift.EndTime.Hours, officeHourShift.EndTime.Minutes, officeHourShift.EndTime.Seconds);
        var timeUntilExecution = officeEndDateTime < currentTime ? officeEndDateTime.AddDays(1) - currentTime : officeEndDateTime - currentTime;
        var timer = new Timer(MonitorAndUnAssignOverflowTeam, null, timeUntilExecution, Timeout.InfiniteTimeSpan);

        Thread backgroundThread1 = new(MonitorAndAssignToAvailableAgents);
        Thread backgroundThread2 = new(MonitorAndRemoveInactiveSessions);
        backgroundThread1.Start();
        backgroundThread2.Start();
    }
}