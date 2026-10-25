using System;
using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class TripTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        private const ulong MissionServer = 90071992547409921UL;
        private const ulong Universe = 90071992547400001UL;

        public static DepartureNews News(string state, ulong server = 0, string id = "d1")
        {
            var text = "depart\nid=" + id + "\nmission=m1\nnom=Convoy\netat=" + state + "\ndemarrage=120\njoueurs=1\njoueursMax=4\nduree=1800\nrecompense=12000\nchoix.Mode=Hard";
            if (server != 0)
                text += "\nserveur=" + server;
            return DepartureNews.Read(WireMessage.Read(text));
        }

        private static ServerAddress Pu()
        {
            return new ServerAddress { ConnectionString = "steam://203.0.113.5:27016", Name = "Sirius PU", SteamId = Universe };
        }

        [Fact]
        public void TheDepartureToldIsReadWithItsStartTimeAndServer()
        {
            var news = News("pret", MissionServer);

            Assert.Equal("d1", news.Id);
            Assert.Equal(DepartureState.Ready, news.State);
            Assert.Equal(120, news.StartSeconds);
            Assert.Equal(MissionServer, news.Server);
            Assert.True(news.IsReady);
            Assert.Equal("Hard", news.Choices[0].Value);
        }

        [Fact]
        public void EveryStateOfSirMissionsIsKnown()
        {
            Assert.Equal(DepartureState.Asked, Departures.StateOf("demande"));
            Assert.Equal(DepartureState.WaitingForTurn, Departures.StateOf("attente-de-son-tour"));
            Assert.Equal(DepartureState.Starting, Departures.StateOf("demarrage"));
            Assert.Equal(DepartureState.Ready, Departures.StateOf("pret"));
            Assert.Equal(DepartureState.Over, Departures.StateOf("termine"));
            Assert.Equal(DepartureState.Failed, Departures.StateOf("echec"));
        }

        [Fact]
        public void WhileTheServerStartsNothingLeaves()
        {
            var trip = new Trip();

            Assert.Equal(TripAction.None, trip.Hear(News("demande"), Now));
            Assert.Equal(TripAction.None, trip.Hear(News("attente-de-son-tour"), Now));
            Assert.Equal(TripAction.None, trip.Hear(News("demarrage"), Now));
            Assert.False(trip.CanLeave);
        }

        [Fact]
        public void BoxCheckedTheGameLeavesAsSoonAsTheServerIsReady()
        {
            var trip = new Trip { AutoLeave = true };
            trip.Hear(News("demarrage"), Now);

            Assert.Equal(TripAction.Leave, trip.Hear(News("pret", MissionServer), Now));
        }

        [Fact]
        public void BackOnTheUniverseWithoutReachingTheMissionServerTheJoinIsToldFailedOnce()
        {
            var trip = new Trip { AutoLeave = true };
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu(), Now);

            // The mission server could not be reached: back on the universe.
            trip.Arrived(Universe);

            Assert.False(trip.ReachedMission);
            Assert.True(trip.TakeMissedMission());
            Assert.False(trip.TakeMissedMission());
            Assert.Null(trip.Current);
        }

        [Fact]
        public void BackFromAMissionPlayedNothingIsToldFailed()
        {
            var trip = new Trip { AutoLeave = true };
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu(), Now);

            trip.Arrived(MissionServer);
            trip.Arrived(Universe);

            Assert.True(trip.ReachedMission);
            Assert.False(trip.TakeMissedMission());
        }

        [Fact]
        public void AJoinFailedLeavesThePlayerFreeToJoinTheSameDepartureAgain()
        {
            var trip = new Trip { AutoLeave = false };
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu(), Now);
            trip.Arrived(Universe);
            trip.TakeMissedMission();

            trip.Asked("d1");

            Assert.Equal(TripAction.AskToLeave, trip.Hear(News("pret", MissionServer), Now));
            Assert.True(trip.Leave(Pu(), Now));
            Assert.False(trip.MissedMission);
        }

        [Fact]
        public void BoxUncheckedThePlayerLeavesWithAClick()
        {
            var trip = new Trip { AutoLeave = false };

            Assert.Equal(TripAction.AskToLeave, trip.Hear(News("pret", MissionServer), Now));
            Assert.True(trip.CanLeave);
            Assert.True(trip.Leave(Pu()));
            Assert.Equal(MissionServer, trip.MissionServer);
        }

        [Fact]
        public void AReadyStateWithoutServerIdentityDoesNotLeave()
        {
            var trip = new Trip();

            Assert.Equal(TripAction.None, trip.Hear(News("pret"), Now));
        }

        [Fact]
        public void TheGameNeverLeavesTwiceForTheSameDeparture()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            Assert.True(trip.Leave(Pu()));

            // Sir Missions tells again (another player joined): nothing more.
            Assert.Equal(TripAction.None, trip.Hear(News("pret", MissionServer), Now));
            Assert.False(trip.Leave(Pu()));
        }

        [Fact]
        public void JoiningAgainAfterComingBackLeavesAgain()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu());

            trip.Asked();

            Assert.Equal(TripAction.Leave, trip.Hear(News("pret", MissionServer), Now));
        }

        [Fact]
        public void OnTheMissionServerTheOrderToGoBackLeadsToTheUniverseLeft()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu());
            trip.Arrived(MissionServer);

            Assert.True(trip.OnMission);
            Assert.Equal(TripAction.GoBack, trip.ReturnOrder());
            Assert.Equal("steam://203.0.113.5:27016", trip.Back.ConnectionString);
            Assert.Equal(Universe, trip.Back.SteamId);
        }

        [Fact]
        public void TheOrderToGoBackRepeatedIsFollowedOnce()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu());
            trip.Arrived(MissionServer);

            Assert.Equal(TripAction.GoBack, trip.ReturnOrder());
            Assert.Equal(TripAction.None, trip.ReturnOrder());
        }

        [Fact]
        public void WithoutKnowingTheUniverseThePlayerIsToldToJoinItHimself()
        {
            var trip = new Trip();

            Assert.Equal(TripAction.GoBackUnknown, trip.ReturnOrder());
        }

        [Fact]
        public void BackOnTheUniverseTheTripIsOver()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu());
            trip.Arrived(MissionServer);
            trip.ReturnOrder();

            trip.Arrived(Universe);

            Assert.False(trip.OnMission);
            Assert.False(trip.Returning);
            Assert.Null(trip.Current);
        }

        [Fact]
        public void TheMissionServerIsNeverRememberedAsTheWayBack()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);

            trip.Leave(new ServerAddress { SteamId = MissionServer });

            Assert.Null(trip.Back);
        }

        [Fact]
        public void GivingUpForgetsTheDeparture()
        {
            var trip = new Trip();
            trip.Hear(News("demarrage"), Now);

            trip.GaveUp("d1");

            Assert.Null(trip.Current);
        }

        [Fact]
        public void AfterGivingUpTheNewsOfThatDepartureIsNoLongerHeard()
        {
            var trip = new Trip();
            trip.Hear(News("demarrage"), Now);
            trip.GaveUp("d1");

            // Sir Missions tells him once more at its next check.
            Assert.Equal(TripAction.None, trip.Hear(News("demarrage"), Now));
            Assert.Null(trip.Current);
            Assert.Equal(TripAction.None, trip.Hear(News("pret", MissionServer), Now));
            Assert.Null(trip.Current);
            trip.Hear(News("termine"), Now);
            Assert.Null(trip.Current);
        }

        [Fact]
        public void LaunchingAnotherMissionStillIgnoresTheDepartureGivenUp()
        {
            var trip = new Trip();
            trip.Hear(News("demarrage"), Now);
            trip.GaveUp("d1");

            trip.Asked();
            trip.Hear(News("demarrage"), Now);
            Assert.Null(trip.Current);

            trip.Hear(News("demande", 0, "d2"), Now);
            Assert.Equal("d2", trip.Current.Id);
        }

        [Fact]
        public void JoiningTheDepartureGivenUpAgainHearsItAgain()
        {
            var trip = new Trip();
            trip.Hear(News("demarrage"), Now);
            trip.GaveUp("d1");

            trip.Asked("d1");
            trip.Hear(News("demarrage"), Now);

            Assert.Equal("d1", trip.Current.Id);
        }

        [Fact]
        public void ALeaveThatNeverHappenedMayBeTriedAgain()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            Assert.True(trip.Leave(Pu(), Now));

            Assert.False(trip.LeaveStalled(Now + TimeSpan.FromSeconds(10)));
            Assert.True(trip.LeaveStalled(Now + Trip.LeaveTimeout));

            trip.LeaveFailed();
            Assert.True(trip.CanLeave);
            Assert.False(trip.LeaveStalled(Now + Trip.LeaveTimeout));
        }

        [Fact]
        public void OnTheMissionServerNothingStalls()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu(), Now);

            trip.Arrived(MissionServer);

            Assert.False(trip.LeaveStalled(Now + Trip.LeaveTimeout));
        }

        [Fact]
        public void AWayBackThatCouldNotStartIsTriedAgainAtTheNextOrder()
        {
            var trip = new Trip();
            trip.Hear(News("pret", MissionServer), Now);
            trip.Leave(Pu(), Now);
            trip.Arrived(MissionServer);

            Assert.Equal(TripAction.GoBack, trip.ReturnOrder());
            Assert.Equal(TripAction.None, trip.ReturnOrder());
            trip.ReturnFailed();
            Assert.Equal(TripAction.GoBack, trip.ReturnOrder());
        }
    }
}
