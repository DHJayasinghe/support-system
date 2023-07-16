namespace TicketSystem;

public class Program
{
    private static readonly ChatSystem chatSystem = new();
    private static readonly Random randomTimeWindows = new Random();

    public static void Main()
    {
        Start();
    }

    public static void Start()
    {
        Thread backgroundThread = new(chatSystem.StartBackgroundTasks);
        backgroundThread.Start();

        chatSystem.AddAgents(new List<Agent>()
        {
            //new Agent { Seniority = Seniority.TeamLead},
            new Agent { Seniority = Seniority.MidLevel },
            new Agent { Seniority = Seniority.MidLevel },
            new Agent { Seniority = Seniority.Junior },
            new Agent { Seniority = Seniority.Junior }
        });
        chatSystem.AddOverflowTeam(new List<Agent>
        {
            new Agent { Seniority = Seniority.Junior },
            //new Agent { Seniority = Seniority.Junior },
            //new Agent { Seniority = Seniority.Junior },
            //new Agent { Seniority = Seniority.Junior },
            //new Agent { Seniority = Seniority.Junior },
            //new Agent { Seniority = Seniority.Junior }
        });

        for (int userId = 1; userId < 35; userId++)
        {
            CreateNewChatWindow(userId);
        }

        // Keep the main thread running. Wait to reduce HIGH CPU usage
        while (true) WaitASecond();
    }

    private static void CreateNewChatWindow(int userId)
    {
        var response = chatSystem.CreateChatSession(userId);
        Console.Write("Create session {0} -> ", userId);
        if (response == "OK")
        {
            var thread = new Thread(PollChatSession);
            thread.Start(userId);
            Console.Write("OK");
        }
        else
        {
            Console.Write("No agent available at the moment");
        }
        Console.WriteLine();
    }

    private static void PollChatSession(object data)
    {
        var userId = (int)data;
        var startTime = DateTime.UtcNow;
        var durationToKeepChatWindowLive = TimeSpan.FromMinutes(randomTimeWindows.Next(1, 3));
        while (DateTime.UtcNow - startTime < durationToKeepChatWindowLive)
        {
            var r = chatSystem.KeepSessionActive(userId);
            if (r == "WAITING") startTime = DateTime.UtcNow; // just keep the session little longer for testing
            WaitASecond();
        }
    }

    private static void WaitASecond() => Thread.Sleep(1000);
}


