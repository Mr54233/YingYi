using StrmAssistant.ScheduledTask;
using Xunit;

namespace YingYi.UpdatePluginTaskSuppress.Tests
{
    /// <summary>
    ///     P3-5 acceptance for the suppression match rule. The full task branch
    ///     (Found new plugin version → Update suppressed) additionally needs a live
    ///     GitHub release above 1.0.0.0, which the release freeze forbids; that part
    ///     stays a post-first-release verification item.
    /// </summary>
    public class UpdatePluginTaskSuppressTests
    {
        private const string Self = "YingYi.dll";

        [Fact]
        public void Match_By_Assembly_Name()
        {
            Assert.Equal("YingYi.dll", UpdateSuppressRule.FindSuppressHit(Self, Self, "v9.9.9"));
            Assert.Equal("yingyi.dll", UpdateSuppressRule.FindSuppressHit("yingyi.dll", Self, "v9.9.9"));
        }

        [Fact]
        public void Match_By_Remote_Tag()
        {
            Assert.Equal("v9.9.9", UpdateSuppressRule.FindSuppressHit("v9.9.9", Self, "v9.9.9"));
        }

        [Fact]
        public void Match_By_Prefix()
        {
            Assert.Equal("Ying", UpdateSuppressRule.FindSuppressHit("Ying", Self, "v9.9.9"));
        }

        [Fact]
        public void Multiple_Entries_With_Separators()
        {
            Assert.Equal("Ying", UpdateSuppressRule.FindSuppressHit("SomeOther.dll; Ying", Self, "v1.0.0.0"));
            Assert.Equal("Ying", UpdateSuppressRule.FindSuppressHit("A.dll ; Ying ; B.dll", Self, null));
        }

        [Fact]
        public void No_Match_Or_Empty_List()
        {
            Assert.Null(UpdateSuppressRule.FindSuppressHit(null, Self, "v9.9.9"));
            Assert.Null(UpdateSuppressRule.FindSuppressHit("", Self, "v9.9.9"));
            Assert.Null(UpdateSuppressRule.FindSuppressHit("  ", Self, "v9.9.9"));
            Assert.Null(UpdateSuppressRule.FindSuppressHit("Other.dll, v8.8.8", Self, "v9.9.9"));
        }

        [Fact]
        public void Null_Tag_Only_Matches_Filename_Or_Prefix()
        {
            Assert.Equal(Self, UpdateSuppressRule.FindSuppressHit(Self, Self, null));
            Assert.Null(UpdateSuppressRule.FindSuppressHit("v9.9.9", Self, null));
        }
    }
}
