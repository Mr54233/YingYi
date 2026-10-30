using MediaBrowser.Common;
using MediaBrowser.Model.Serialization;
using StrmAssistant.Options;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StrmAssistant.Metadata
{
    /// <summary>
    /// 组B：本地元数据缓存服务（仅本地 IO，不发网络请求）。
    /// 缓存文件命名：key 的 SHA-1 前 16 位十六进制 + ".json"。
    /// 序列化通过 IApplicationHost.Resolve&lt;IJsonSerializer&gt;() 获取，
    /// 全路径取值均包 try/catch：拿不到序列化器或 IO 失败时降级为 false/null 并写 Debug 日志。
    /// 说明：当前 Emby 元数据抓取管线尚未接线到本服务（接线需要 Harmony，红线禁止），
    /// 本服务先行就位，供设置页验证与后续接线使用。
    /// </summary>
    public static class MetadataCacheService
    {
        private const string FileExtension = ".json";

        public static bool EnsureCacheRoot()
        {
            var options = Plugin.Instance.GetPluginOptions().MetadataBuildOptions;
            if (options == null ||
                options.MetadataCacheMode == Options.MetadataBuildOptions.MetadataCacheOption.None.ToString() ||
                string.IsNullOrWhiteSpace(options.MetadataCacheRootFolder))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(options.MetadataCacheRootFolder))
                {
                    Directory.CreateDirectory(options.MetadataCacheRootFolder);
                    Plugin.Instance.Logger.Info("MetadataCacheService: created cache root {0}", options.MetadataCacheRootFolder);
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("MetadataCacheService: failed to ensure cache root {0}: {1}", options.MetadataCacheRootFolder, ex.Message);
                return false;
            }
        }

        public static bool SaveCache(string key, object payload)
        {
            if (string.IsNullOrWhiteSpace(key) || payload == null || !EnsureCacheRoot())
            {
                return false;
            }

            try
            {
                var serializer = ResolveSerializer();
                if (serializer == null)
                {
                    return false;
                }

                var path = GetCacheFilePath(key);
                serializer.SerializeToFile(payload, path);
                Plugin.Instance.Logger.Debug("MetadataCacheService: saved cache {0}", path);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("MetadataCacheService: failed to save cache for key {0}: {1}", key, ex.Message);
                return false;
            }
        }

        public static T TryLoadCache<T>(string key) where T : class
        {
            if (string.IsNullOrWhiteSpace(key) || !EnsureCacheRoot())
            {
                return null;
            }

            try
            {
                var serializer = ResolveSerializer();
                if (serializer == null)
                {
                    return null;
                }

                var path = GetCacheFilePath(key);
                if (!File.Exists(path))
                {
                    return null;
                }

                var result = serializer.DeserializeFromFile<T>(path);
                Plugin.Instance.Logger.Debug("MetadataCacheService: loaded cache {0}", path);
                return result;
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Warn("MetadataCacheService: failed to load cache for key {0}: {1}", key, ex.Message);
                return null;
            }
        }

        private static IJsonSerializer ResolveSerializer()
        {
            try
            {
                // 4.8.0.80 的 IApplicationHost.Resolve<T>() 零参返回实例；
                // 跨版本签名差异统一用 try/catch 降级，不按版本号硬编码。
                var serializer = Plugin.Instance.ApplicationHost.Resolve<IJsonSerializer>();
                if (serializer == null)
                {
                    Plugin.Instance.Logger.Debug("MetadataCacheService: IJsonSerializer not resolvable, cache disabled");
                }

                return serializer;
            }
            catch (Exception ex)
            {
                Plugin.Instance.Logger.Debug("MetadataCacheService: failed to resolve IJsonSerializer: {0}", ex.Message);
                return null;
            }
        }

        private static string GetCacheFilePath(string key)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
                var name = new StringBuilder(16);
                for (var i = 0; i < 8; i++)
                {
                    name.Append(hash[i].ToString("x2"));
                }

                var root = Plugin.Instance.GetPluginOptions().MetadataBuildOptions.MetadataCacheRootFolder;
                return Path.Combine(root, name.ToString() + FileExtension);
            }
        }
    }
}
