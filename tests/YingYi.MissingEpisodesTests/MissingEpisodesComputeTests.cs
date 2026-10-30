using StrmAssistant.Web.Dto;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace StrmAssistant.Tests
{
    /// <summary>
    ///     P3-4 MissingEpisodes MVP：只覆盖纯计算部分。端点行为（开关 404 门、
    ///     IProviderManager.GetAllEpisodes 调用、失败安全）依赖 Emby 宿主面，
    ///     由 tools/scripts/verify-apis.py 的沙盒冒烟兜底。
    /// </summary>
    public class MissingEpisodesComputeTests
    {
        [Fact]
        public void ComputeMissing_ReturnsGapNumbers()
        {
            var missing = MissingEpisodesCalculator.ComputeMissing(1,
                new[] { 1, 2, 3, 4, 5 },
                new[] { 1, 2, 5 });

            Assert.Equal(new[] { 3, 4 }, missing.Select(m => m.EpisodeNumber).ToArray());
            Assert.All(missing, m => Assert.Equal(1, m.SeasonNumber));
        }

        [Fact]
        public void ComputeMissing_EmptyExpected_YieldsEmpty()
        {
            var missing = MissingEpisodesCalculator.ComputeMissing(2, Enumerable.Empty<int>(), new[] { 1, 2 });

            Assert.Empty(missing);
        }

        [Fact]
        public void ComputeMissing_NullInputs_DoNotThrow()
        {
            var missing = MissingEpisodesCalculator.ComputeMissing(1, null, null);

            Assert.Empty(missing);
        }

        [Fact]
        public void ComputeMissing_DeduplicatesAndIgnoresNonPositive()
        {
            var missing = MissingEpisodesCalculator.ComputeMissing(1,
                new List<int> { 2, 2, 0, -1, 3 },
                new List<int> { 3 });

            Assert.Equal(new[] { 2 }, missing.Select(m => m.EpisodeNumber).ToArray());
        }

        [Fact]
        public void Response_Defaults_AreReadOnlyAndFailSafe()
        {
            var response = new MissingEpisodesResponse();

            Assert.True(response.IsReadOnly);
            Assert.True(response.IsBestEffort);
            Assert.Equal("none", response.Source);
            Assert.Empty(response.Episodes);
        }
    }
}
