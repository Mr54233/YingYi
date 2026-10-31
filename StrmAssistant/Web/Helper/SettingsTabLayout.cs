using System;
using System.Collections.Generic;
using System.Linq;

namespace StrmAssistant.Web.Helper
{
    /// <summary>
    ///     静态登记影医设置 Tab 结构（无行为），供文档与后续分 Tab 实现共用。
    ///     目标分组：5 个 Tab，避免单页 14 组堆叠（用户反馈 2026-10-31）。
    /// </summary>
    internal static class SettingsTabLayout
    {
        public static readonly (string TabName, string[] Groups)[] Tabs =
        {
            ("基础", new[] { "GeneralOptions", "MediaInfoExtractOptions", "PinyinEnhanceOptions" }),
            ("元数据", new[] { "MetadataEnhanceOptions", "MetadataRefreshOptions", "MetadataBuildOptions", "MetadataScrapeOptions" }),
            ("片头片尾", new[] { "IntroSkipOptions" }),
            ("体验与网络", new[] { "ExperienceEnhanceOptions", "UIFunctionOptions", "ServerOptions" }),
            ("关于", new[] { "AboutOptions" })
        };
    }
}
