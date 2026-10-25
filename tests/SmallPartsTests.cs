using System;
using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class CompanionAgreementTests
    {
        [Fact]
        public void TheBlockAsksForTheWindowWithItsIdentity()
        {
            long block;

            Assert.True(CompanionAgreement.IsOpenRequest("ouvrir\nbloc=123456789", out block));
            Assert.Equal(123456789L, block);
        }

        [Fact]
        public void AnythingElseIsNotARequest()
        {
            long block;

            Assert.False(CompanionAgreement.IsOpenRequest("ouverte", out block));
            Assert.False(CompanionAgreement.IsOpenRequest(42, out block));
            Assert.False(CompanionAgreement.IsOpenRequest(null, out block));
        }

        [Fact]
        public void TheChannelsAreThoseOfTheCompanion()
        {
            Assert.Equal(5465480001L, CompanionAgreement.RequestChannel);
            Assert.Equal(5465480002L, CompanionAgreement.AnswerChannel);
            Assert.Equal("ouverte", CompanionAgreement.Opened);
        }
    }

    public class SettingsFileTests
    {
        [Fact]
        public void LeavingAutomaticallyIsCheckedAtInstallation()
        {
            Assert.True(new ClientSettings().AutoLeave);
            Assert.True(SettingsFile.Parse(null).AutoLeave);
            Assert.True(SettingsFile.Parse("garbage\nautoLeave=maybe").AutoLeave);
        }

        [Fact]
        public void TheChoiceIsKeptForTheNextGames()
        {
            var text = SettingsFile.Write(new ClientSettings { AutoLeave = false });

            Assert.False(SettingsFile.Parse(text).AutoLeave);
        }
    }

    public class TextsTests
    {
        [Fact]
        public void DurationsAreWrittenInWords()
        {
            Assert.Equal("No time limit", Texts.Duration(0));
            Assert.Equal("30 minutes", Texts.Duration(1800));
            Assert.Equal("1 hour 30 minutes", Texts.Duration(5400));
            Assert.Equal("2 days", Texts.Duration(172800));
            Assert.Equal("45 seconds", Texts.Duration(45));
        }

        [Fact]
        public void RewardsAreWrittenInCredits()
        {
            Assert.Equal("12,000 credits", Texts.Reward(12000));
            Assert.Equal("No reward", Texts.Reward(0));
        }

        [Fact]
        public void EveryRefusalOfSirMissionsIsWorded()
        {
            foreach (var reason in new[] { "mission-inconnue", "mission-incomplete", "choix-manquant", "choix-invalide", "complete", "deja-en-mission", "depart-inconnu", "depart-ferme", "joueur-inconnu" })
                Assert.NotEqual(Texts.Refusal("anything"), Texts.Refusal(reason));
        }

        [Fact]
        public void TheWaitSaysWhetherTheGameLeavesByItself()
        {
            var ready = TripTests.News("pret", 5);

            Assert.Equal(Texts.StateReadyAuto, Texts.Waiting(ready, true, false));
            Assert.Equal(Texts.StateReadyClick, Texts.Waiting(ready, false, false));
            Assert.Equal(Texts.StateReadyLeft, Texts.Waiting(ready, false, true));
        }

        [Fact]
        public void AMissionOfferedIsDescribedWithAllItsFacts()
        {
            var mission = new OfferedMission { Name = "Convoy", Description = "Escort", MaxPlayers = 4, DurationSeconds = 1800, Reward = 500 };

            var text = Texts.Describe(mission);

            Assert.Contains("Convoy", text);
            Assert.Contains("Escort", text);
            Assert.Contains("Players at most: 4", text);
            Assert.Contains("30 minutes", text);
            Assert.Contains("500 credits", text);
        }
    }

    public class WayBackTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void IdleAtTheMenuAfterLeavingTheWayBackIsOfferedOnce()
        {
            var way = new WayBack();
            way.Arm(Now);

            Assert.False(way.Update(Now.AddSeconds(9), false, false));
            Assert.True(way.Update(Now.AddSeconds(14), false, false));
            Assert.False(way.Update(Now.AddSeconds(30), false, false));
        }

        [Fact]
        public void AWorldReachedDisarmsIt()
        {
            var way = new WayBack();
            way.Arm(Now);

            way.Update(Now.AddSeconds(20), true, false);

            Assert.False(way.Armed);
        }

        [Fact]
        public void WhileTheGameJoinsNothingIsOffered()
        {
            var way = new WayBack();
            way.Arm(Now);

            Assert.False(way.Update(Now.AddSeconds(20), false, true));
            Assert.False(way.Update(Now.AddSeconds(40), false, null));
        }
    }
}
