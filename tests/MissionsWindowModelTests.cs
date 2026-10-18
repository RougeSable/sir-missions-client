using System;
using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class MissionsWindowModelTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

        private static string Receive(MissionsWindowModel model, string text, DateTime now)
        {
            string outgoing;
            model.Receive(WireMessage.Read(text), now, out outgoing);
            return outgoing;
        }

        private static MissionsWindowModel Opened(Trip trip = null)
        {
            var model = new MissionsWindowModel(trip ?? new Trip());
            var asked = WireMessage.Read(model.Open(Now));
            Receive(model, MissionCatalogTests.Answer.Replace("requete=3", "requete=" + asked.Value("requete")), Now);
            return model;
        }

        [Fact]
        public void OpeningAsksSirMissionsForItsMissions()
        {
            var model = new MissionsWindowModel(new Trip());

            var request = WireMessage.Read(model.Open(Now));

            Assert.Equal("missions", request.Verb);
            Assert.Equal(WindowView.Loading, model.View(Now));
        }

        [Fact]
        public void WithoutAnswerTheWindowSaysNoMissionIsOfferedHere()
        {
            var model = new MissionsWindowModel(new Trip());
            model.Open(Now);

            Assert.Equal(WindowView.NoAnswer, model.View(Now + MissionsWindowModel.AnswerDelay));
        }

        [Fact]
        public void TheAnswerShowsTheMissionsOfferedAndThoseInProgress()
        {
            var model = Opened();

            Assert.Equal(WindowView.List, model.View(Now));
            Assert.Equal(2, model.Catalog.Missions.Count);
            Assert.Equal(2, model.Catalog.Running.Count);
            Assert.Equal("m1", model.SelectedMission);
            Assert.Equal("d1", model.SelectedRunning);
        }

        [Fact]
        public void AnOlderAnswerThanTheLastQuestionIsIgnored()
        {
            var model = new MissionsWindowModel(new Trip());
            model.Open(Now);
            model.Refresh(Now);

            Receive(model, MissionCatalogTests.Answer.Replace("requete=3", "requete=1"), Now);

            Assert.Null(model.Catalog);
        }

        [Fact]
        public void TheMissionWithAChoiceCannotBeLaunchedUntilItsValueIsPicked()
        {
            var model = Opened();
            model.SelectMission("m1");

            Assert.False(model.CanLaunch);
            Assert.Null(model.Launch(Now));

            model.Form.Pick("Mode", "Easy");

            Assert.True(model.CanLaunch);
            var request = WireMessage.Read(model.Launch(Now));
            Assert.Equal("Easy", request.Value("choix.Mode"));
        }

        [Fact]
        public void TheMissionWithoutChoiceLaunchesWithAClick()
        {
            var model = Opened();
            model.SelectMission("m2");

            Assert.True(model.CanLaunch);
            Assert.Equal("lancer", WireMessage.Read(model.Launch(Now)).Verb);
            Assert.True(model.Pending);
        }

        [Fact]
        public void AMissionInProgressIsJoinedWithoutChoosingAnything()
        {
            var model = Opened();
            model.SelectRunning("d1");

            Assert.True(model.CanJoin);
            var request = WireMessage.Read(model.Join(Now));
            Assert.Equal("rejoindre", request.Verb);
            Assert.Equal("d1", request.Value("depart"));
            Assert.Empty(request.Prefixed("choix."));
        }

        [Fact]
        public void AFullMissionInProgressCannotBeJoined()
        {
            var model = Opened();
            model.SelectRunning("d2");

            Assert.False(model.CanJoin);
            Assert.Null(model.Join(Now));
        }

        [Fact]
        public void ALaunchedMissionTurnsTheWindowIntoTheWait()
        {
            var trip = new Trip();
            var model = Opened(trip);
            model.SelectMission("m2");
            model.Launch(Now);

            var text = "depart\nid=d9\nmission=m2\nnom=Survey\netat=demande\ndemarrage=120\njoueurs=1\njoueursMax=2\nduree=0\nrecompense=0";
            trip.Hear(DepartureNews.Read(WireMessage.Read(text)), Now);
            Receive(model, text, Now);

            Assert.Equal(WindowView.Waiting, model.View(Now));
            Assert.False(model.Pending);
            var wait = Texts.Describe(trip.Current, trip.Since, Now.AddSeconds(35), trip.AutoLeave, false);
            Assert.Contains("Announced start time: about 2 minutes", wait);
            Assert.Contains("Waiting for 0:35", wait);
        }

        [Fact]
        public void WhenTheLimitOfMissionServersIsReachedTheLauncherIsToldHisMissionWaitsItsTurn()
        {
            var trip = new Trip();
            trip.Hear(TripTests.News("attente-de-son-tour"), Now);

            var wait = Texts.Describe(trip.Current, trip.Since, Now, trip.AutoLeave, false);

            Assert.Contains("waits for its turn", wait);
            Assert.Contains("Announced start time", wait);
        }

        [Fact]
        public void TheWindowOpenedWhileWaitingAsksForTheDepartureAwaited()
        {
            var model = new MissionsWindowModel(new Trip());
            model.Open(Now);

            var outgoing = Receive(model, "missions\nattendu=d1", Now);

            var request = WireMessage.Read(outgoing);
            Assert.Equal("rejoindre", request.Verb);
            Assert.Equal("d1", request.Value("depart"));
        }

        [Fact]
        public void ARefusalIsWordedForThePlayer()
        {
            var model = Opened();
            model.SelectRunning("d1");
            model.Join(Now);

            Receive(model, "refus\nraison=complete", Now);

            Assert.False(model.Pending);
            Assert.True(model.StatusIsError);
            Assert.Equal("This mission is full.", model.Status);
        }

        [Fact]
        public void ARequestWithoutAnswerFreesTheButtonsAgain()
        {
            var model = Opened();
            model.SelectMission("m2");
            model.Launch(Now);

            Assert.True(model.Tick(Now + MissionsWindowModel.AnswerDelay, WindowView.List));
            Assert.False(model.Pending);
            Assert.True(model.CanLaunch);
        }

        [Fact]
        public void GivingUpGoesBackToTheList()
        {
            var trip = new Trip();
            var model = Opened(trip);
            trip.Hear(TripTests.News("demarrage"), Now);

            Assert.True(model.CanGiveUp);
            Assert.Equal("quitter", WireMessage.Read(model.GiveUp(Now)).Verb);
            var outgoing = Receive(model, "quitte\ndepart=d1", Now);

            Assert.Null(trip.Current);
            Assert.Equal("missions", WireMessage.Read(outgoing).Verb);
        }

        [Fact]
        public void AnEndedDepartureLeavesTheWaitForTheList()
        {
            var trip = new Trip();
            var model = Opened(trip);
            trip.Hear(TripTests.News("echec"), Now);

            Assert.False(model.CanGiveUp);
            Assert.True(model.CanGoBackToList);
            Assert.Equal("missions", WireMessage.Read(model.BackToList(Now)).Verb);
            Assert.Null(trip.Current);
        }

        // Sir Missions tells the one who gave up once more, then at its next
        // check: the window stays on the list, and he may launch another one.
        [Fact]
        public void AfterGivingUpTheWindowNeverGoesBackToThatWait()
        {
            var trip = new Trip();
            var model = Opened(trip);
            trip.Hear(TripTests.News("demarrage"), Now);
            model.GiveUp(Now);
            var asked = WireMessage.Read(Receive(model, "quitte\ndepart=d1", Now));

            const string again = "depart\nid=d1\nmission=m1\nnom=Convoy\netat=demarrage\ndemarrage=120";
            trip.Hear(TripTests.News("demarrage"), Now);
            Assert.Null(Receive(model, again, Now));
            Receive(model, MissionCatalogTests.Answer.Replace("requete=3", "requete=" + asked.Value("requete")), Now);

            Assert.Null(trip.Current);
            Assert.Equal(WindowView.List, model.View(Now));
            Assert.False(model.Pending);
            model.SelectMission("m2");
            Assert.True(model.CanLaunch);
        }

        [Fact]
        public void TheDepartureGivenUpIsNotJoinedAgainByItself()
        {
            var trip = new Trip();
            trip.GaveUp("d1");
            var model = new MissionsWindowModel(trip);
            model.Open(Now);

            Assert.Null(Receive(model, "missions\nattendu=d1", Now));
        }

        [Fact]
        public void JoiningTheDepartureGivenUpWaitsForItAgain()
        {
            var trip = new Trip();
            var model = Opened(trip);
            trip.GaveUp("d1");

            model.SelectRunning("d1");
            Assert.Equal("rejoindre", WireMessage.Read(model.Join(Now)).Verb);
            trip.Hear(TripTests.News("demarrage"), Now);

            Assert.Equal(WindowView.Waiting, model.View(Now));
        }
    }
}
