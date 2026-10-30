using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Douban.Dto;
using StrmAssistant.Options;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StrmAssistant.Douban
{
    /// <summary>
    ///     Persists Douban detail JSON on disk so the cache-build task and the manual
    ///     scrape endpoint can share payloads without re-hitting the API.
    ///     The cache root reuses the (group B owned) MetadataBuildOptions.MetadataCacheRootFolder
    ///     option: when that option is empty or the folder is missing, caching is a no-op
    ///     and callers must still work correctly (write-through cache, not a gate).
    ///     Pure local IO: no network, no exceptions escaping from Save/TryLoad.
    /// </summary>
    public class DoubanCacheService
    {
        private const string DefaultRootFolderName = "DoubanCache";

        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        public DoubanCacheService(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        ///     Returns the effective cache root: the shared MetadataCacheRootFolder option
        ///     when set and existing, else a plugin-local default folder (always usable).
        ///     Never null.
        /// </summary>
        public string GetCacheRoot()
        {
            string configured = null;
            try
            {
                configured = Plugin.Instance?.GetPluginOptions()?.MetadataBuildOptions
                    ?.MetadataCacheRootFolder;
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - Failed to read cache root option: {0}", e.Message);
            }

            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            {
                return Path.Combine(configured, DefaultRootFolderName);
            }

            // 配置了根目录但目录还不存在时自动建目录，而不是静默回退：
            // 实测（4.10.2.0，D8 验收）用户把缓存根指到一个尚未创建的路径时，
            // 回退到插件配置目录会让文件出现在意料之外的位置（cache/douban 永远为空）。
            if (!string.IsNullOrWhiteSpace(configured))
            {
                try
                {
                    Directory.CreateDirectory(configured);
                    return Path.Combine(configured, DefaultRootFolderName);
                }
                catch (Exception e)
                {
                    _logger?.Warn("Douban - Cannot create configured cache root '{0}': {1}", configured, e.Message);
                }
            }

            var pluginConfigPath = Plugin.Instance?.ApplicationPaths?.PluginConfigurationsPath;
            if (string.IsNullOrWhiteSpace(pluginConfigPath))
            {
                return null;
            }

            return Path.Combine(pluginConfigPath, "YingYi", DefaultRootFolderName);
        }

        public void SaveDetail(DoubanDetailResponse detail, string subjectId)
        {
            if (detail == null || string.IsNullOrWhiteSpace(subjectId))
            {
                return;
            }

            try
            {
                var root = GetCacheRoot();
                if (root == null)
                {
                    return;
                }

                Directory.CreateDirectory(root);
                _jsonSerializer.SerializeToFile(detail, BuildCachePath(root, subjectId));
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - Failed to write cache for subject {0}: {1}", subjectId, e.Message);
            }
        }

        public DoubanDetailResponse TryLoadDetail(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return null;
            }

            try
            {
                var root = GetCacheRoot();
                if (root == null)
                {
                    return null;
                }

                var path = BuildCachePath(root, subjectId);
                if (!File.Exists(path))
                {
                    return null;
                }

                return _jsonSerializer.DeserializeFromFile<DoubanDetailResponse>(path);
            }
            catch (Exception e)
            {
                _logger?.Debug("Douban - Failed to read cache for subject {0}: {1}", subjectId, e.Message);
                return null;
            }
        }

        private static string BuildCachePath(string root, string subjectId)
        {
            // "douban-{id}" is already filesystem-safe (digits); the sha1 prefix keeps the
            // shape uniform if ids ever contain unexpected characters.
            var safeName = "douban-" + SanitizeFileName(subjectId);
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(safeName));
                var prefix = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty);
                return Path.Combine(root, prefix + "-" + safeName + ".json");
            }
        }

        private static string SanitizeFileName(string value)
        {
            var sb = new StringBuilder(value.Trim().Length);
            foreach (var c in value.Trim())
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return sb.Length == 0 ? "unknown" : sb.ToString();
        }
    }
}
