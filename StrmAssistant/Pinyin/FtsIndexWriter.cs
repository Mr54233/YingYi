using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MediaBrowser.Model.Logging;
using SQLitePCL.pretty;

namespace StrmAssistant.Pinyin
{
    /// <summary>
    ///     P1-1 full and incremental injection into the Emby FTS virtual table
    ///     (docs/analysis/p1-1-p1-2-拼音层方案.md §3).
    ///     Works strictly in batches: 200 rows per BEGIN IMMEDIATE transaction,
    ///     Thread.Sleep(300ms) between batches (never Task.Delay — see plan §3.2),
    ///     WAL checkpoint every 5 batches.
    /// </summary>
    internal sealed class FtsIndexWriter
    {
        internal const int BatchSize = 200;
        private const int BatchPauseMs = 300;
        private const int CheckpointInterval = 5;

        private readonly ILogger _logger;
        private readonly EmbyDatabase _database;
        private readonly string _cursorFilePath;

        private string _ftsTable;

        internal FtsIndexWriter(ILogger logger, EmbyDatabase database, string cursorFilePath)
        {
            _logger = logger;
            _database = database;
            _cursorFilePath = cursorFilePath;
        }

        internal static string Normalize(string name)
        {
            return name?.Replace("'", string.Empty).Replace(".", string.Empty);
        }

        /// <summary>
        ///     Resolves the actual FTS table name (fts_search9 today, future-proof for
        ///     fts_search10+). Returns null when no fts_search% table exists yet —
        ///     Emby creates it during library migration, catch-up will find it later.
        /// </summary>
        internal string ResolveFtsTable(bool force = false)
        {
            if (_ftsTable != null && !force) return _ftsTable;

            _ftsTable = _database.DetectFtsTable();
            if (_ftsTable == null)
            {
                _logger.Info("PinyinSearch - no fts_search table found, skipping injection");
            }

            return _ftsTable;
        }

        /// <summary>
        ///     Full/incremental injection pass. Returns the number of rows injected.
        ///     The pending query itself is cheap (c0=Name comparison), so calling this
        ///     when nothing is pending costs one indexed-ish scan of the joined view.
        /// </summary>
        internal int InjectPending(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var ftsTable = ResolveFtsTable();
            if (ftsTable == null) return 0;

            var total = CountPending(ftsTable);
            if (total == 0)
            {
                _logger.Info("PinyinSearch - no pending items");
                SaveCursor(0);
                progress?.Report(100);
                return 0;
            }

            _logger.Info("PinyinSearch - injecting pinyin tokens into {0} rows", total);

            var injected = 0;
            var lastId = LoadCursor();
            var batchCount = 0;
            var wrapped = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                var rows = _database.Use(db => FetchPendingBatch(db, ftsTable, lastId));
                if (rows == null || rows.Count == 0)
                {
                    // Emby rewrites the FTS document (plain name) on metadata saves,
                    // so rows BELOW the cursor can become pending again. Wrap around
                    // once per pass instead of stalling above the cursor forever.
                    if (lastId > 0 && !wrapped && CountPending(ftsTable) > 0)
                    {
                        wrapped = true;
                        lastId = 0;
                        SaveCursor(0);
                        continue;
                    }

                    break;
                }

                var batchInjected = _database.Use(db => InjectBatch(db, ftsTable, rows));
                if (batchInjected < 0) break;

                injected += batchInjected;
                lastId = rows[rows.Count - 1].Id;
                SaveCursor(lastId);

                batchCount++;
                if (batchCount % CheckpointInterval == 0)
                {
                    _database.Use(db => db.Execute("PRAGMA wal_checkpoint(TRUNCATE)"));
                }

                if (progress != null)
                {
                    var percent = Math.Min(injected / (double)total * 100, 100);
                    progress.Report(percent);
                }

                if (rows.Count < BatchSize) break;

                Thread.Sleep(BatchPauseMs);
            }

            if (injected > 0)
            {
                _database.Use(db => db.Execute("PRAGMA wal_checkpoint(TRUNCATE)"));
            }

            if (injected >= total)
            {
                SaveCursor(0);
                progress?.Report(100);
            }

            _logger.Info("PinyinSearch - injection finished: {0}/{1} rows", injected, total);
            return injected;
        }

        /// <summary>
        ///     Re-injects a single item by Emby item id (event queue path).
        ///     Emby's SaveItems overwrites the FTS row with the plain name, so this is
        ///     the final line of defence for items added/updated/refreshed at runtime.
        /// </summary>
        internal bool InjectItem(long internalId)
        {
            var ftsTable = ResolveFtsTable();
            if (ftsTable == null) return false;

            var done = _database.Use(db =>
            {
                var rows = QueryItem(db, ftsTable, internalId);
                if (rows.Count == 0) return 0;
                return InjectBatch(db, ftsTable, rows);
            });

            return done > 0;
        }

        /// <summary>
        ///     Pro: MediaItems.type values for the item types the pinyin index covers.
        ///     Measured on Emby 4.10 library.db (Movie=5, Series=6, Season=7, Episode=8);
        ///     BoxSet=16 is inferred from the 4.10 SqliteItemRepository decompilation
        ///     (Type=16 joins ItemLinks2), no BoxSet existed in the sandbox library.
        /// </summary>
        private static readonly Dictionary<string, int> ItemTypeToMediaItemsType =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "Movie", 5 },
                { "Series", 6 },
                { "Season", 7 },
                { "Episode", 8 },
                { "BoxSet", 16 }
            };

        /// <summary>
        ///     Pro: " AND mi.type IN (...)" clause for the SearchItemScope option.
        ///     Empty string when the option is unset/invalid/whitelist-everything,
        ///     which keeps the historical behaviour (all item types).
        /// </summary>
        private static string BuildTypeFilter()
        {
            var scope = Plugin.Instance.GetPluginOptions().PinyinEnhanceOptions.SearchItemScope;
            if (string.IsNullOrWhiteSpace(scope)) return string.Empty;

            var types = new List<int>();
            foreach (var name in scope.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ItemTypeToMediaItemsType.TryGetValue(name.Trim(), out var type) && !types.Contains(type))
                {
                    types.Add(type);
                }
            }

            // Whitelist-empty after parsing means an invalid value was stored; treat
            // as "all" rather than filtering out everything.
            var typeList = string.Join(",", types);
            return types.Count == 0 ? string.Empty : " AND mi.type IN (" + typeList + ")";
        }

        private int CountPending(string ftsTable)
        {
            return _database.Use(db =>
            {
                using (var statement = db.PrepareStatement(
                    $"SELECT count(*) FROM {ftsTable}_content c JOIN MediaItems mi ON c.id = mi.Id " +
                    "WHERE (c.c0 = mi.Name OR c.c0 = replace(replace(mi.Name, char(39), ''), '.', '')) " +
                    $"AND mi.Name GLOB '*[一-龥]*'{BuildTypeFilter()}"))
                {
                    statement.MoveNext();
                    return (int)statement.Current.GetInt64(0);
                }
            });
        }

        private List<PendingRow> FetchPendingBatch(IDatabaseConnection db, string ftsTable, long lastId)
        {
            var rows = new List<PendingRow>(BatchSize);
            using (var statement = db.PrepareStatement(
                $"SELECT c.id, mi.Name FROM {ftsTable}_content c JOIN MediaItems mi ON c.id = mi.Id " +
                "WHERE (c.c0 = mi.Name OR c.c0 = replace(replace(mi.Name, char(39), ''), '.', '')) " +
                $"AND mi.Name GLOB '*[一-龥]*'{BuildTypeFilter()} AND c.id > ? ORDER BY c.id LIMIT " + BatchSize))
            {
                statement.BindParameters[0].Bind(lastId);
                while (statement.MoveNext())
                {
                    var result = statement.Current;
                    if (result.IsDBNull(1)) continue;
                    rows.Add(new PendingRow(result.GetInt64(0), result.GetString(1)));
                }
            }

            return rows;
        }

        private List<PendingRow> QueryItem(IDatabaseConnection db, string ftsTable, long internalId)
        {
            var rows = new List<PendingRow>(1);
            using (var statement = db.PrepareStatement(
                $"SELECT c.id, mi.Name FROM {ftsTable}_content c JOIN MediaItems mi ON c.id = mi.Id " +
                "WHERE c.id = ? AND (c.c0 = mi.Name OR c.c0 = replace(replace(mi.Name, char(39), ''), '.', '')) " +
                $"AND mi.Name GLOB '*[一-龥]*'{BuildTypeFilter()}"))
            {
                statement.BindParameters[0].Bind(internalId);
                while (statement.MoveNext())
                {
                    var result = statement.Current;
                    if (result.IsDBNull(1)) continue;
                    rows.Add(new PendingRow(result.GetInt64(0), result.GetString(1)));
                }
            }

            return rows;
        }

        /// <summary>Returns the injected row count, or -1 when the transaction failed.</summary>
        private int InjectBatch(IDatabaseConnection db, string ftsTable, List<PendingRow> rows)
        {
            db.Execute("BEGIN IMMEDIATE");
            try
            {
                var injected = 0;
                foreach (var row in rows)
                {
                    var tokens = PinyinConverter.BuildSearchTokens(row.Name);
                    if (tokens == null) continue;

                    // A statement that reached DONE (MoveNext() == false) must be Reset
                    // before it can be stepped again, and IStatement exposes no Reset.
                    // Both statements are therefore rebuilt per row.
                    string c1 = null, c2 = null, c3 = null;
                    var hasColumns = false;
                    using (var readColumns = db.PrepareStatement(
                               $"SELECT c1, c2, c3 FROM {ftsTable}_content WHERE id = ?"))
                    {
                        readColumns.BindParameters[0].Bind(row.Id);
                        while (readColumns.MoveNext())
                        {
                            var result = readColumns.Current;
                            c1 = result.IsDBNull(0) ? null : result.GetString(0);
                            c2 = result.IsDBNull(1) ? null : result.GetString(1);
                            c3 = result.IsDBNull(2) ? null : result.GetString(2);
                            hasColumns = true;
                        }
                    }

                    if (!hasColumns) continue;

                    using (var replace = db.PrepareStatement(
                               $"INSERT OR REPLACE INTO {ftsTable} (rowid, Name, OriginalTitle, SeriesName, Album) " +
                               "VALUES (?, ?, ?, ?, ?)"))
                    {
                        replace.BindParameters[0].Bind(row.Id);
                        replace.BindParameters[1].Bind(tokens);
                        if (c1 == null) replace.BindParameters[2].BindNull();
                        else replace.BindParameters[2].Bind(c1);
                        if (c2 == null) replace.BindParameters[3].BindNull();
                        else replace.BindParameters[3].Bind(c2);
                        if (c3 == null) replace.BindParameters[4].BindNull();
                        else replace.BindParameters[4].Bind(c3);
                        replace.MoveNext();
                    }

                    injected++;
                }

                db.Execute("COMMIT");
                return injected;
            }
            catch (Exception ex)
            {
                try
                {
                    db.Execute("ROLLBACK");
                }
                catch
                {
                    // ignore rollback failure, connection returns to the pool
                }

                _logger.Error("PinyinSearch - batch injection failed: {0}", ex.Message);
                return -1;
            }
        }

        private long LoadCursor()
        {
            try
            {
                if (!File.Exists(_cursorFilePath)) return 0;
                var text = File.ReadAllText(_cursorFilePath);
                var json = CoreApiDeserialize(text);
                return json;
            }
            catch
            {
                return 0;
            }
        }

        private static long CoreApiDeserialize(string text)
        {
            // minimal {"lastId":123} reader without pulling a JSON dependency in
            var start = text != null ? text.IndexOf("\"lastId\"", StringComparison.Ordinal) : -1;
            if (start < 0) return 0;
            var colon = text.IndexOf(':', start);
            if (colon < 0) return 0;
            var valueStart = colon + 1;
            while (valueStart < text.Length && (char.IsWhiteSpace(text[valueStart]))) valueStart++;
            var valueEnd = valueStart;
            while (valueEnd < text.Length && (char.IsDigit(text[valueEnd]) || text[valueEnd] == '-')) valueEnd++;
            return valueEnd > valueStart && long.TryParse(text.Substring(valueStart, valueEnd - valueStart), out var v)
                ? v
                : 0;
        }

        private void SaveCursor(long lastId)
        {
            try
            {
                var directory = Path.GetDirectoryName(_cursorFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tmp = _cursorFilePath + ".tmp";
                File.WriteAllText(tmp, "{\"lastId\":" + lastId + "}");
                if (File.Exists(_cursorFilePath))
                {
                    File.Delete(_cursorFilePath);
                }

                File.Move(tmp, _cursorFilePath);
            }
            catch (Exception ex)
            {
                _logger.Debug("PinyinSearch - failed to persist cursor: {0}", ex.Message);
            }
        }

        private readonly struct PendingRow
        {
            internal PendingRow(long id, string name)
            {
                Id = id;
                Name = name;
            }

            internal long Id { get; }

            internal string Name { get; }
        }
    }
}
