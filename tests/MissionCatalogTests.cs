using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class MissionCatalogTests
    {
        // As Sir Missions writes it (EchangesAvecLeClient.ListeDesMissions).
        public const string Answer =
            "missions\n" +
            "requete=3\n" +
            "mission\n" +
            "id=m1\n" +
            "nom=Convoy\n" +
            "description=Escort the convoy\\nto the station\n" +
            "monde=Sirius\n" +
            "joueursMax=4\n" +
            "duree=1800\n" +
            "dureeTexte=30 minutes\n" +
            "recompense=12000\n" +
            "choix=Mode\n" +
            "valeur=Easy\n" +
            "valeur=Hard\n" +
            "mission\n" +
            "id=m2\n" +
            "nom=Survey\n" +
            "description=Look around\n" +
            "monde=Sirius\n" +
            "joueursMax=2\n" +
            "duree=0\n" +
            "dureeTexte=sans limite\n" +
            "recompense=0\n" +
            "enCours\n" +
            "id=d1\n" +
            "mission=m1\n" +
            "nom=Convoy\n" +
            "etat=demarrage\n" +
            "joueurs=1\n" +
            "joueursMax=4\n" +
            "places=3\n" +
            "lanceur=Rouge\n" +
            "duree=1800\n" +
            "recompense=12000\n" +
            "choix.Mode=Hard\n" +
            "enCours\n" +
            "id=d2\n" +
            "mission=m2\n" +
            "nom=Survey\n" +
            "etat=pret\n" +
            "joueurs=2\n" +
            "joueursMax=2\n" +
            "places=0\n" +
            "lanceur=Sable\n" +
            "duree=0\n" +
            "recompense=0";

        [Fact]
        public void EveryMissionOfferedComesWithNameDescriptionPlayersDurationAndReward()
        {
            var catalog = MissionCatalog.Read(WireMessage.Read(Answer));

            Assert.Equal(2, catalog.Missions.Count);
            var convoy = catalog.Missions[0];
            Assert.Equal("m1", convoy.Id);
            Assert.Equal("Convoy", convoy.Name);
            Assert.Equal("Escort the convoy\nto the station", convoy.Description);
            Assert.Equal(4, convoy.MaxPlayers);
            Assert.Equal(1800, convoy.DurationSeconds);
            Assert.Equal(12000, convoy.Reward);
            Assert.Equal(3, catalog.Request);
        }

        [Fact]
        public void TheChoicesOfAMissionComeWithTheirValuesInOrder()
        {
            var catalog = MissionCatalog.Read(WireMessage.Read(Answer));

            var convoy = catalog.Mission("m1");
            Assert.True(convoy.HasChoices);
            Assert.Single(convoy.Choices);
            Assert.Equal("Mode", convoy.Choices[0].Name);
            Assert.Equal(new[] { "Easy", "Hard" }, convoy.Choices[0].Values);
            Assert.False(catalog.Mission("m2").HasChoices);
        }

        [Fact]
        public void MissionsInProgressComeWithTheirPlacesLeft()
        {
            var catalog = MissionCatalog.Read(WireMessage.Read(Answer));

            Assert.Equal(2, catalog.Running.Count);
            var convoy = catalog.RunningById("d1");
            Assert.Equal("m1", convoy.MissionId);
            Assert.Equal(DepartureState.Starting, convoy.State);
            Assert.Equal(1, convoy.Players);
            Assert.Equal(3, convoy.Places);
            Assert.Equal("Rouge", convoy.Launcher);
            Assert.Equal("Hard", convoy.Choices[0].Value);
            Assert.True(convoy.CanJoin);
        }

        [Fact]
        public void AFullMissionIsNeverOfferedToJoin()
        {
            var catalog = MissionCatalog.Read(WireMessage.Read(Answer));

            var survey = catalog.RunningById("d2");
            Assert.Equal(0, survey.Places);
            Assert.False(survey.CanJoin);
            Assert.Equal("Full", Texts.Places(survey.Places));
        }

        [Fact]
        public void TheDepartureAlreadyAwaitedIsRead()
        {
            var catalog = MissionCatalog.Read(WireMessage.Read("missions\nattendu=d1"));

            Assert.Equal("d1", catalog.Awaited);
            Assert.Empty(catalog.Missions);
        }

        [Fact]
        public void AnotherAnswerIsNoCatalog()
        {
            Assert.Null(MissionCatalog.Read(WireMessage.Read("refus\nraison=complete")));
        }
    }
}
