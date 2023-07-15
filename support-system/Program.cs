namespace TicketSystem;

public class Program
{
    public static void Start()
    {
        // Create agents
        var chatSystem = new ChatSystem();
        chatSystem.AddAgents(new List<Agent>()
        {
            new Agent { Seniority = Seniority.TeamLead},
            new Agent { Seniority = Seniority.MidLevel },
            new Agent { Seniority = Seniority.MidLevel },
            new Agent { Seniority = Seniority.Junior }
        });

        //// Create overflow team
        //var overflowTeam = new List<Agent>
        //{
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true },
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true },
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true },
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true },
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true },
        //    new Agent { Seniority = Seniority.Junior, MaxConcurrency = 10, EfficiencyMultiplier = 0.4, IsAvailable = true }
        //};

        //chatSystem.AddAgents(agents[0]);
        //chatSystem.AddAgents(agents[1]);
        //chatSystem.AddAgents(agents[2]);
        //chatSystem.AddAgents(agents[3]);
        //chatSystem.AddOverflowTeam(overflowTeam);
        //chatSystem.UpdateMaxQueueLength();

        var r = chatSystem.CreateChatSession(1);
        if (r == "OK")
        {
            chatSystem.ProcessChatQueue();
        }
        else
        {
            Console.WriteLine(r);
        }
        chatSystem.CreateChatSession(2);
        chatSystem.CreateChatSession(3);
        chatSystem.CreateChatSession(4);
        chatSystem.CreateChatSession(5);
        chatSystem.CreateChatSession(6);
        chatSystem.CreateChatSession(7);

        chatSystem.ProcessChatQueue();
    }
}
