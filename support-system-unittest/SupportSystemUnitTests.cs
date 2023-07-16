using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using supportSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TicketSystem;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
namespace support_system_unittest
{
    [TestClass]
    public class SupportSystemUnitTests
    {
        private ChatSystem _sut;
        private List<Agent> teamA;
        private List<Agent> teamB;
        private List<Agent> teamC;
        private List<Agent> overflowTeam;

        [TestInitialize]
        public void Initialize()
        {
            _sut = new();
            teamA = new()
            {
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.MORNING },
                new Agent { Seniority = Seniority.MidLevel, ShiftId = Shift.MORNING },
                new Agent { Seniority = Seniority.MidLevel, ShiftId = Shift.MORNING },
                new Agent { Seniority = Seniority.TeamLead, ShiftId = Shift.MORNING }
            };
            teamB = new()
            {
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.AFTERNOON },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.AFTERNOON },
                new Agent { Seniority = Seniority.MidLevel, ShiftId = Shift.AFTERNOON },
                new Agent { Seniority = Seniority.Senior, ShiftId = Shift.AFTERNOON }
            };
            teamC = new()
            {
                new Agent { Seniority = Seniority.MidLevel, ShiftId = Shift.NIGHT },
                new Agent { Seniority = Seniority.MidLevel, ShiftId = Shift.NIGHT }
            };
            overflowTeam = new()
            {
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.OFFICEHOURS }
            };
        }

        [TestMethod]
        public void Should_EveryCreateSessionRequestAccept_When_CapacityIsEnough()
        {
            int capacity = (int)(teamA.Sum(agent => agent.Capactiy) * ChatSystem.QUEUELENGTHMULTIPLIER);
            _sut.AddTeam(teamA);

            using var context = new DateTimeProviderContext(PickTimeFromMorningShift());
            _sut.ChangeDateTimeProviderContext(context);
            var responses = new List<string>();
            for (int userId = 1; userId <= capacity; userId++)
            {
                var response = _sut.CreateChatSession(userId);
                responses.Add(response);
            }

            responses.Should().AllBeEquivalentTo("OK");
        }

        [TestMethod]
        public void Should_CreateSessionRequestsRefuse_When_QueueIsFull()
        {
            int capacity = (int)(teamA.Sum(agent => agent.Capactiy) * ChatSystem.QUEUELENGTHMULTIPLIER) + 1;
            _sut.AddTeam(teamA);

            using var context = new DateTimeProviderContext(PickTimeFromMorningShift());
            var responses = new List<string>();
            for (int userId = 1; userId <= capacity; userId++)
            {
                var response = _sut.CreateChatSession(userId);
                responses.Add(response);
            }
            var acceptedResponses = responses.Count(r => r == "OK");
            var refusedResponses = responses.Count - acceptedResponses;

            acceptedResponses.Should().NotBe(0);
            refusedResponses.Should().NotBe(0);
        }

        [TestMethod]
        public void Should_SessionRequestsAssignedRoundRobin_To_AvailableAgents()
        {
            var agents = new List<Agent>()
            {
                new Agent { Seniority = Seniority.Senior, ShiftId = Shift.MORNING },
                new Agent { Seniority = Seniority.Junior, ShiftId = Shift.MORNING }
            };
            _sut.AddTeam(agents);

            using var context = new DateTimeProviderContext(PickTimeFromMorningShift());
            _sut.ChangeDateTimeProviderContext(context);
            var responses = new List<string>();
            for (int userId = 1; userId <= 5; userId++)
            {
                var response = _sut.CreateChatSession(userId);
                responses.Add(response);
            }
            StartBackgroundProcess(_sut);
            WaitLittleUntilBackgroundProcessToRun();

            agents.Where(agent => agent.Seniority == Seniority.Junior).Single().CurrentChats.Should().Be(4);
            agents.Where(agent => agent.Seniority == Seniority.Senior).Single().CurrentChats.Should().Be(1);
        }

        private static void StartBackgroundProcess(ChatSystem sut)
        {
            Thread backgroundThread = new(sut.StartBackgroundTasks);
            backgroundThread.Start();
        }

        private static void WaitLittleUntilBackgroundProcessToRun(int secondsToWait = 2)
        {
            Thread.Sleep(secondsToWait * 1000);
        }

        [TestMethod]
        public void Should_CreatedSessionsRemainWaitingOnQueue_When_CapacityIsBusy()
        {
            int capacity = (int)(teamA.Sum(agent => agent.Capactiy) * ChatSystem.QUEUELENGTHMULTIPLIER);
            _sut.AddTeam(teamA);

            using var context = new DateTimeProviderContext(PickTimeFromMorningShift());
            _sut.ChangeDateTimeProviderContext(context);
            for (int userId = 1; userId <= capacity; userId++)
                _sut.CreateChatSession(userId);
            StartBackgroundProcess(_sut);
            WaitLittleUntilBackgroundProcessToRun(4);
            var responses = new List<string>();
            for (int userId = 1; userId <= capacity; userId++)
            {
                var response = _sut.PollChatSession(userId);
                responses.Add(response);
            }
            var activeSessions = responses.Count(r => r == "ACTIVE");
            var waitingSessions = responses.Count(r => r == "WAITING");

            activeSessions.Should().NotBe(0);
            waitingSessions.Should().NotBe(0);
        }

        [TestMethod]
        public void Should_ChatSessionMarkInactive_When_InitiatorIsIdleFor3Secs()
        {
            int userId = 1;
            var agent = new Agent { Seniority = Seniority.Junior, ShiftId = Shift.MORNING };
            _sut.AddTeam(agent);

            using var context = new DateTimeProviderContext(PickTimeFromMorningShift());
            _sut.ChangeDateTimeProviderContext(context);

            _sut.CreateChatSession(userId);
            StartBackgroundProcess(_sut);
            var chatSessionPreviousState = _sut.ChatSessions[userId].IsActive;
            WaitLittleUntilBackgroundProcessToRun(5);
            var chatSessionAfterState = _sut.ChatSessions[userId].IsActive;

            chatSessionPreviousState.Should().BeTrue();
            chatSessionAfterState.Should().BeFalse();
        }

        [TestMethod]
        public async Task Should_ChatSessionKeepActive_When_InitiatorKeepPolling()
        {
            int userId = 1;
            var chatSessionPreviousState = false;
            var agent = new Agent { Seniority = Seniority.Junior, ShiftId = Shift.MORNING };
            _sut.AddTeam(agent);

            using (var context = new DateTimeProviderContext(PickTimeFromMorningShift()))
            {
                _sut.ChangeDateTimeProviderContext(context);
                _sut.CreateChatSession(userId);
                StartBackgroundProcess(_sut);
                chatSessionPreviousState = _sut.ChatSessions[userId].IsActive;
                for (int i = 0; i < 3; i++)
                {
                    _sut.PollChatSession(userId);
                    Thread.Sleep(1000);
                }
                WaitLittleUntilBackgroundProcessToRun(2);
            }
            var chatSessionAfterState = _sut.ChatSessions[userId].IsActive;

            chatSessionPreviousState.Should().BeTrue();
            chatSessionAfterState.Should().BeTrue();
        }

        [TestMethod]
        public async Task Should_ChatSessionAssignedAgentReleaseBack_After_SessionIsInactiveAsync()
        {
            int userId = 1;
            var agent = new Agent { Seniority = Seniority.Junior, ShiftId = Shift.MORNING };
            _sut.AddTeam(agent);

            var time = PickTimeFromMorningShift();
            using var context = new DateTimeProviderContext(time);
            _sut.ChangeDateTimeProviderContext(context);

            _sut.CreateChatSession(userId);
            StartBackgroundProcess(_sut);
            WaitLittleUntilBackgroundProcessToRun();
            var assignedAgentCapacityBefore = agent.RemainingCapactiy;
            WaitLittleUntilBackgroundProcessToRun(3);
            var assignedAgentCapacityAfter = agent.RemainingCapactiy;

            assignedAgentCapacityBefore.Should().Be(agent.Capactiy - 1);
            assignedAgentCapacityAfter.Should().Be(agent.Capactiy);
        }

        [TestMethod]
        public void Should_OverflowTeamAssignedDuringOfficeHours_When_SessionQueueIsFull()
        {
            _sut.AddTeam(teamA);
            _sut.AddOverflowTeam(overflowTeam);

            using var context = new DateTimeProviderContext(PickTimeFromOfficeHours());
            _sut.ChangeDateTimeProviderContext(context);

            var activeAgentsCountBefore = _sut.GetActiveAgents().Count;
            for (int userId = 0; userId < 35; userId++)
                _sut.CreateChatSession(userId);
            var activeAgentsCountAfter = _sut.GetActiveAgents().Count;

            activeAgentsCountBefore.Should().Be(teamA.Count);
            activeAgentsCountAfter.Should().Be(teamA.Count + overflowTeam.Count);
        }

        [TestMethod]
        public void ShouldNot_OverflowTeamAssignedAfterOfficeHours_When_SessionQueueIsFull()
        {
            int capacity = (int)(teamB.Sum(agent => agent.Capactiy) * ChatSystem.QUEUELENGTHMULTIPLIER);
            _sut.AddTeam(teamB);
            _sut.AddOverflowTeam(overflowTeam);

            using var context = new DateTimeProviderContext(PickTimeFromAfterOfficeHours());
            _sut.ChangeDateTimeProviderContext(context);
            var activeAgentsCountBefore = _sut.GetActiveAgents().Count;
            for (int userId = 1; userId <= capacity; userId++)
                _sut.CreateChatSession(userId);
            var activeAgentsCountAfter = _sut.GetActiveAgents().Count;

            activeAgentsCountBefore.Should().Be(teamB.Count);
            activeAgentsCountAfter.Should().Be(teamB.Count);
        }

        [TestMethod]
        public void Should_AssignedOverflowTeamMustUnassign_When_OfficeHoursEnd()
        {
            int capacity = (int)(teamB.Sum(agent => agent.Capactiy) * ChatSystem.QUEUELENGTHMULTIPLIER);
            //var overflowTeam = new List<Agent>()
            //{
            //    new Agent { Seniority = Seniority.Junior, ShiftId = Shift.AFTERNOON },
            //    new Agent { Seniority = Seniority.Junior, ShiftId = Shift.AFTERNOON }
            //};
            _sut.AddTeam(teamB);
            _sut.AddOverflowTeam(overflowTeam);
            var activeAgentsCountBefore = 0;
            var activeAgentsCountAfter = 0;
            var activeAgentsCountAfterOfficeHours = 0;

            using (var context = new DateTimeProviderContext(PickTimeFromAfternoonShift()))
            {
                _sut.ChangeDateTimeProviderContext(context);
                activeAgentsCountBefore = _sut.GetActiveAgents().Count;
                for (int userId = 0; userId < 35; userId++)
                    _sut.CreateChatSession(userId);
                StartBackgroundProcess(_sut);
                WaitLittleUntilBackgroundProcessToRun();
                activeAgentsCountAfter = _sut.GetActiveAgents().Count;
            }

            using (var context = new DateTimeProviderContext(PickTime3SecsBeforeOfficeHoursEnd()))
            {
                _sut.ChangeDateTimeProviderContext(context);
                StartBackgroundProcess(_sut);
                WaitLittleUntilBackgroundProcessToRun(4);
                activeAgentsCountAfterOfficeHours = _sut.GetActiveAgents().Count;
            }

            activeAgentsCountBefore.Should().Be(teamA.Count);
            activeAgentsCountAfter.Should().Be(teamA.Count + overflowTeam.Count);
            activeAgentsCountAfterOfficeHours.Should().Be(teamA.Count);
        }

        [TestMethod]
        public void Should_AssignQueuedChatSessionToNewTeam_When_ShiftChanged()
        {
            _sut.AddTeam(teamA);
            _sut.AddTeam(teamB);
            _sut.AddTeam(teamC);

            using (var context = new DateTimeProviderContext(PickTime3SecsBeforeMorningShiftEnd()))
            {
                _sut.ChangeDateTimeProviderContext(context);
                for (int userId = 0; userId < 35; userId++)
                    _sut.CreateChatSession(userId);
                StartBackgroundProcess(_sut);
                WaitLittleUntilBackgroundProcessToRun(5);
            }

            int chatSessionsFromTeamA = 0;
            int chatSessionsFromTeamB = 0;

            foreach (var (sessionId, session) in _sut.ChatSessions)
            {
                if (session.AssignedAgent == null) continue;
                if (teamA.Any(agent => agent.Id == session.AssignedAgent.Id)) chatSessionsFromTeamA++;
                else chatSessionsFromTeamB++;
            }

            chatSessionsFromTeamA.Should().NotBe(0);
            chatSessionsFromTeamB.Should().NotBe(0);
        }

        private static DateTime PickTime3SecsBeforeMorningShiftEnd() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 13, 59, 57).ToUniversalTime();
        private static DateTime PickTime3SecsBeforeOfficeHoursEnd() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 17, 59, 57).ToUniversalTime();
        private static DateTime PickTimeFromMorningShift() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 8, 0, 0).ToUniversalTime();
        private static DateTime PickTimeFromAfternoonShift() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 15, 0, 0).ToUniversalTime();
        private static DateTime PickTimeFromOfficeHours() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 13, 0, 0).ToUniversalTime();
        private static DateTime PickTimeFromAfterOfficeHours() => new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 19, 0, 0).ToUniversalTime();
    }
}