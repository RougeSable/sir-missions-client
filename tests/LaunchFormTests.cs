using SirMissionsClient;
using Xunit;

namespace SirMissionsClient.Tests
{
    public class LaunchFormTests
    {
        private static OfferedMission WithMode()
        {
            var mission = new OfferedMission { Id = "m1", Name = "Convoy" };
            var mode = new MissionChoice { Name = "Mode" };
            mode.Values.Add("Easy");
            mode.Values.Add("Hard");
            mission.Choices.Add(mode);
            return mission;
        }

        [Fact]
        public void AMissionWithAChoiceAsksForItsValueBeforeLaunching()
        {
            var form = new LaunchForm(WithMode());

            Assert.False(form.CanLaunch);
            Assert.Equal(new[] { "Mode" }, form.Missing());
            Assert.Null(form.Picked("Mode"));
        }

        [Fact]
        public void OnceEachChoiceIsPickedTheMissionLaunchesWithTheValues()
        {
            var form = new LaunchForm(WithMode());

            Assert.True(form.Pick("Mode", "Hard"));

            Assert.True(form.CanLaunch);
            var request = WireMessage.Read(form.Request(1));
            Assert.Equal("lancer", request.Verb);
            Assert.Equal("m1", request.Value("mission"));
            Assert.Equal("Hard", request.Value("choix.Mode"));
        }

        [Fact]
        public void AValueTheMissionDoesNotOfferIsRefused()
        {
            var form = new LaunchForm(WithMode());

            Assert.False(form.Pick("Mode", "Impossible"));
            Assert.False(form.Pick("Weather", "Rain"));
            Assert.False(form.CanLaunch);
        }

        [Fact]
        public void AMissionWithoutChoiceLaunchesAtOnce()
        {
            var form = new LaunchForm(new OfferedMission { Id = "m2", Name = "Survey" });

            Assert.True(form.CanLaunch);
            var request = WireMessage.Read(form.Request(1));
            Assert.Equal("m2", request.Value("mission"));
            Assert.Empty(request.Prefixed("choix."));
        }
    }
}
