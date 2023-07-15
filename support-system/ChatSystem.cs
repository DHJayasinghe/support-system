using System.Collections.Concurrent;

namespace TicketSystem;

public class ChatSystem
{
    //private readonly ConcurrentDictionary<int,>
    private readonly Queue<ChatSession> chatQueue;
    private readonly List<Agent> _agents;
    private readonly List<Agent> overflowTeam;
    private int maxQueueLength;
    private int currentSessionId;

    public ChatSystem()
    {
        chatQueue = new Queue<ChatSession>();
        _agents = new List<Agent>();
        overflowTeam = new List<Agent>();
        maxQueueLength = 0;
        currentSessionId = 1;
    }

    public void AddAgents(IEnumerable<Agent> agents) => _agents.AddRange(agents);

    public void AddOverflowTeam(List<Agent> team)
    {
        overflowTeam.AddRange(team);
    }

    public string CreateChatSession(int userId)
    {
        if (IsMaximumQueueReached() && IsOfficeHours())
            AssignOverflowTeam();

        if (IsMaximumQueueReached()) return ChatRefusedMessage();

        var session = new ChatSession
        {
            Id = currentSessionId++,
        };
        chatQueue.Enqueue(session);
        AssignChatToAgent(session);
        return "OK";
    }

    private static string ChatRefusedMessage() => "Chat is refused. Queue is full.";

    private bool IsMaximumQueueReached()
    {
        return maxQueueLength == chatQueue.Count;
    }

    private static bool IsOfficeHours()
    {
        // TODO: office hours check
        return true;
    }

    private void AssignOverflowTeam()
    {
        // TODO: assign overflow team to available agent pool
        CalculateCapacity();
    }

    private void AssignChatToAgent(ChatSession session)
    {
        var agentToAssign = RoundRobinAgentPick();

        if (agentToAssign != null)
        {
            agentToAssign.CurrentChats++;
            session.AssignedAgent = agentToAssign;
        }
        else
        {
            Console.WriteLine("No available agents to assign the chat.");
        }
    }

    private Agent RoundRobinAgentPick() => _agents
        .Where(agent => agent.IsAvailable)
        .OrderBy(agent => agent.Seniority)
        .OrderBy(agent => agent.CurrentChats)
        .FirstOrDefault();

    public void ProcessChatQueue()
    {
        while (chatQueue.Count > 0)
        {
            var session = chatQueue.Peek();
            if (session.IsActive)
            {
                Console.WriteLine($"Processing chat session {session.Id}");
                session.IsActive = false;
                PollChatSession(session);
            }
            else
            {
                chatQueue.Dequeue();
            }
        }
    }

    private void PollChatSession(ChatSession session)
    {
        int pollCount = 0;
        while (pollCount < 3 && session.IsActive)
        {
            // Poll the chat session
            // If response is OK, set session.IsActive = true; to keep it active
            // Otherwise, increment pollCount

            pollCount++;
            Thread.Sleep(1000); // Wait for 1 second before polling again
        }

        if (pollCount >= 3)
        {
            session.AssignedAgent.CurrentChats--;
            //session.AssignedAgent.IsAvailable = true;
            Console.WriteLine($"Chat session {session.Id} marked as inactive.");
        }
    }

    private void CalculateCapacity()
    {
        maxQueueLength = (int)(_agents.Sum(agent => agent.RemainingCapactiy) * 1.5);
    }
}
