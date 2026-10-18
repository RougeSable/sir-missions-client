using System.Collections.Generic;
using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class WireTests
    {
        [Fact]
        public void ProtectedTextsReadBackAsWritten()
        {
            var text = new WireWriter("depart").Line("nom", "Convoy = risky\nnight \\ run").Line("joueurs", 3).Text();

            var message = WireMessage.Read(text);

            Assert.Equal("depart", message.Verb);
            Assert.Equal("Convoy = risky\nnight \\ run", message.Value("nom"));
            Assert.Equal("3", message.Value("joueurs"));
        }

        [Fact]
        public void EmptyOrBlankTextIsNoMessage()
        {
            Assert.Null(WireMessage.Read(null));
            Assert.Null(WireMessage.Read(""));
            Assert.Null(WireMessage.Read("\nnom=x"));
        }

        [Fact]
        public void LaunchCarriesTheMissionAndEachChoicePicked()
        {
            var text = Requests.Launch("edc037160d17", new[] { new KeyValuePair<string, string>("Mode", "Hard") }, 4);

            var message = WireMessage.Read(text);

            Assert.Equal(Protocol.Launch, message.Verb);
            Assert.Equal("lancer", message.Verb);
            Assert.Equal("edc037160d17", message.Value("mission"));
            Assert.Equal("4", message.Value("requete"));
            var choices = message.Prefixed("choix.");
            Assert.Single(choices);
            Assert.Equal("Mode", choices[0].Key);
            Assert.Equal("Hard", choices[0].Value);
        }

        [Fact]
        public void JoinCarriesTheDepartureAndNoChoice()
        {
            var message = WireMessage.Read(Requests.Join("d-42", 7));

            Assert.Equal("rejoindre", message.Verb);
            Assert.Equal("d-42", message.Value("depart"));
            Assert.Empty(message.Prefixed("choix."));
        }

        [Fact]
        public void GiveUpAndMissionsUseTheWordsOfSirMissions()
        {
            Assert.Equal("quitter", WireMessage.Read(Requests.GiveUp("d-1", 1)).Verb);
            Assert.Equal("missions", WireMessage.Read(Requests.Missions(1)).Verb);
            Assert.Equal(47231, Protocol.RequestChannel);
            Assert.Equal(47232, Protocol.AnswerChannel);
        }

        [Fact]
        public void SteamIdentitiesReadInFull()
        {
            Assert.Equal(90071992547409921UL, WireMessage.SteamId("90071992547409921"));
            Assert.Equal(0UL, WireMessage.SteamId("not a number"));
        }
    }
}
