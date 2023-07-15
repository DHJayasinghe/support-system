namespace TicketSystem;

public class Program
{
    private static readonly ChatSystem chatSystem = new();

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
            new Agent { Seniority = Seniority.Junior }
        });
        //chatSystem.AddOverflowTeam(new List<Agent>
        //{
        //    new Agent { Seniority = Seniority.Junior },
        //    new Agent { Seniority = Seniority.Junior },
        //    new Agent { Seniority = Seniority.Junior },
        //    new Agent { Seniority = Seniority.Junior },
        //    new Agent { Seniority = Seniority.Junior },
        //    new Agent { Seniority = Seniority.Junior }
        //});

        for (int userId = 1; userId < 31; userId++)
        {
            CreateNewChatWindow(userId);
        }

        // Keep the main thread running. Wait to reduce HIGH CPU usage
        while (true) WaitASecond();
    }

    private static void CreateNewChatWindow(int userId)
    {
        Thread thread = new Thread(NewChatWindow);
        thread.Start(userId);
    }

    public static void NewChatWindow(object data)
    {
        var userId = (int)data;
        var response = chatSystem.CreateChatSession(userId);
        if (response == "OK")
        {
            PollChatSession(chatSystem, userId);
        }
        else
        {
            Console.WriteLine("No agent available at the moment");
        }

    }

    private static void PollChatSession(ChatSystem chatSystem, int userId)
    {
        while (true)
        {
            chatSystem.KeepSessionActive(userId);
            WaitASecond();
        }
    }

    private static void WaitASecond() => Thread.Sleep(1000);
}


