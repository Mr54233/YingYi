using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using SQLitePCL.pretty;
using StrmAssistant.Common;

namespace StrmAssistant.Pinyin
{
    /// <summary>
    ///     P1-2: writes pinyin initials into <c>MediaItems.SortName</c> and keeps the
    ///     original value in <c>PinyinSortNameBackup</c> so the change can be rolled
    ///     back (docs/analysis/p1-1-p1-2-拼音层方案.md §4).
    ///     Backup and update always run inside the same BEGIN IMMEDIATE transaction,
    ///     batches are 200 rows with a 300ms pause (never Task.Delay, see plan §3.2),
    ///     and the WAL is checkpointed every 5 batches plus once at the end.
    /// </summary>
    internal sealed class PinyinSortNameService
    {
        internal const int BatchSize = 200;
        internal const string BackupTable = "PinyinSortNameBackup";

        private const int BatchPauseMs = 300;
        private const int CheckpointInterval = 5;

        private const string CreateBackupTableSql =
            "CREATE TABLE IF NOT EXISTS PinyinSortNameBackup (ItemId INTEGER PRIMARY KEY, OriginalSortName TEXT) WITHOUT ROWID";

        private const string CandidateSql =
            "SELECT Id, Name, SortName, type, IsLocked, LockedFields, Path FROM MediaItems " +
            "WHERE Id > ? AND Name GLOB '*[一-龥]*' ORDER BY Id LIMIT 200";

        private readonly ILogger _logger;
        private readonly EmbyDatabase _database;
        private readonly ILibraryManager _libraryManager;

        internal PinyinSortNameService(ILogger logger, EmbyDatabase database, ILibraryManager libraryManager)
        {
            _logger = logger;
            _database = database;
            _libraryManager = libraryManager;
        }

        /// <summary>
        ///     True when a backup table from a previous run exists, i.e. sort names are
        ///     currently modified and can be rolled back.
        /// </summary>
        internal bool HasBackup
        {
            get
            {
                var count = _database.Use(db =>
                {
                    var rows = EmbyDatabase.Query(db,
                        "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=?", BackupTable);
                    return rows.Count == 0 ? 0 : (int)rows[0].GetInt64(0);
                });

                return count > 0;
            }
        }

        /// <summary>
        ///     Applies pinyin sort names to every eligible item that is not backed up yet.
        ///     Returns the number of updated rows.
        /// </summary>
        internal int ApplyPending(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!_database.IsAvailable)
            {
                _logger.Info("PinyinSortName - database unavailable, skipping");
                return 0;
            }

            var locations = GetLocationsInScope();
            var total = CountCandidates();
            if (total == 0)
            {
                _logger.Info("PinyinSortName - no pending items");
                progress?.Report(100);
                return 0;
            }

            _logger.Info("PinyinSortName - applying pinyin sort name to {0} items", total);

            var applied = 0;
            var scanned = 0;
            var lastId = 0L;
            var batchCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var rows = _database.Use(db => FetchBatch(db, lastId));
                if (rows == null || rows.Count == 0) break;

                lastId = rows[rows.Count - 1].Id;
                scanned += rows.Count;

                var batchApplied = _database.Use(db => ApplyBatch(db, rows, locations));
                if (batchApplied < 0) break;

                applied += batchApplied;

                batchCount++;
                if (batchCount % CheckpointInterval == 0)
                {
                    _database.Use(db => db.Execute("PRAGMA wal_checkpoint(TRUNCATE)"));
                }

                if (progress != null)
                {
                    var percent = Math.Min(scanned / (double)total * 100, 100);
                    progress.Report(percent);
                }

                if (rows.Count < BatchSize) break;

                Thread.Sleep(BatchPauseMs);
            }

            if (applied > 0)
            {
                _database.Use(db => db.Execute("PRAGMA wal_checkpoint(TRUNCATE)"));
            }

            progress?.Report(100);
            _logger.Info("PinyinSortName - sort name applied to {0} items ({1} scanned)", applied, scanned);
            return applied;
        }

        /// <summary>
        ///     Re-applies the pinyin sort name for one item (event queue path).
        ///     Emby resets SortName to the plain name on every metadata refresh, so this
        ///     is what keeps the sort title alive at runtime. The very first original
        ///     value is preserved by INSERT OR IGNORE.
        /// </summary>
        internal bool ApplyItem(long internalId)
        {
            var done = _database.Use(db =>
            {
                db.Execute(CreateBackupTableSql);

                var rows = EmbyDatabase.Query(db,
                    "SELECT Id, Name, SortName, type, IsLocked, LockedFields, Path FROM MediaItems WHERE Id = ?",
                    internalId);
                if (rows.Count == 0) return false;

                if (!ReadCandidate(rows[0], out var candidate)) return false;

                var locations = GetLocationsInScope();
                if (!IsEligible(candidate, locations, out var sortName)) return false;

                return ApplyBatch(db, new List<CandidateRow> { candidate }, locations) > 0;
            });

            return done;
        }

        /// <summary>
        ///     Restores every backed up sort name and then drops the backup table,
        ///     returning the library to the exact state before the feature was enabled.
        /// </summary>
        internal int Rollback(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (!HasBackup)
            {
                _logger.Info("PinyinSortName - no backup table, nothing to roll back");
                progress?.Report(100);
                return 0;
            }

            var total = _database.Use(db =>
            {
                var rows = EmbyDatabase.Query(db, $"SELECT count(*) FROM {BackupTable}");
                return rows.Count == 0 ? 0 : (int)rows[0].GetInt64(0);
            });

            _logger.Info("PinyinSortName - rolling back {0} sort names", total);

            var restored = 0;
            var offset = 0;
            var batchCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var rows = _database.Use(db => EmbyDatabase.Query(db,
                    $"SELECT ItemId, OriginalSortName FROM {BackupTable} ORDER BY ItemId LIMIT {BatchSize} OFFSET ?",
                    (long)offset));

                if (rows == null || rows.Count == 0) break;

                var batchRestored = _database.Use(db => RestoreBatch(db, rows));
                if (batchRestored < 0) break;

                restored += batchRestored;
                offset += rows.Count;

                batchCount++;
                if (batchCount % CheckpointInterval == 0)
                {
                    _database.Use(db => db.Execute("PRAGMA wal_checkpoint(TRUNCATE)"));
                }

                if (progress != null && total > 0)
                {
                    progress.Report(Math.Min(offset / (double)total * 100, 100));
                }

                if (rows.Count < BatchSize) break;

                Thread.Sleep(BatchPauseMs);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Info("PinyinSortName - rollback cancelled after {0} items, backup table kept", restored);
                return restored;
            }

            _database.Use(db =>
            {
                db.Execute("PRAGMA wal_checkpoint(TRUNCATE)");
                db.Execute($"DROP TABLE IF EXISTS {BackupTable}");
                db.Execute("PRAGMA wal_checkpoint(TRUNCATE)");
            });

            progress?.Report(100);
            _logger.Info("PinyinSortName - rollback finished: {0}/{1} sort names restored, backup table dropped",
                restored, total);
            return restored;
        }

        private int CountCandidates()
        {
            return _database.Use(db =>
            {
                var exists = EmbyDatabase.Query(db,
                    "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=?", BackupTable);
                var hasBackup = exists.Count > 0 && exists[0].GetInt64(0) > 0;

                var sql = hasBackup
                    ? "SELECT count(*) FROM MediaItems mi WHERE mi.Name GLOB '*[一-龥]*' " +
                      $"AND NOT EXISTS (SELECT 1 FROM {BackupTable} b WHERE b.ItemId = mi.Id)"
                    : "SELECT count(*) FROM MediaItems mi WHERE mi.Name GLOB '*[一-龥]*'";

                var rows = EmbyDatabase.Query(db, sql);
                return rows.Count == 0 ? 0 : (int)rows[0].GetInt64(0);
            });
        }

        private List<CandidateRow> FetchBatch(IDatabaseConnection db, long lastId)
        {
            var rows = EmbyDatabase.Query(db, CandidateSql, lastId);
            var candidates = new List<CandidateRow>(rows.Count);
            foreach (var row in rows)
            {
                if (ReadCandidate(row, out var candidate)) candidates.Add(candidate);
            }

            return candidates;
        }

        private static bool ReadCandidate(EmbyDatabase.Row row, out CandidateRow candidate)
        {
            candidate = default;
            var id = row.GetInt64(0);
            var name = row.GetString(1);
            if (string.IsNullOrEmpty(name)) return false;

            candidate = new CandidateRow(
                id,
                name,
                row.GetString(2),
                row.GetInt64(3),
                row.GetInt64(4) != 0,
                row.GetString(5),
                row.GetString(6));
            return true;
        }

        private static bool IsEligible(CandidateRow candidate, List<string> locations, out string sortName)
        {
            sortName = null;

            // Seasons and episodes inherit the sort title of their series, and the
            // internal folder rows (root / media folders / library folders) are never
            // shown as sortable titles. Plan §4.1.
            var type = candidate.Type;
            if (type == 1 || type == 2 || type == 3 || type == 7 || type == 8) return false;

            if (candidate.IsLocked) return false;
            if (!string.IsNullOrEmpty(candidate.LockedFields) &&
                candidate.LockedFields.IndexOf("SortName", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            if (locations.Count > 0 && !string.IsNullOrEmpty(candidate.Path) &&
                !locations.Any(l => candidate.Path.StartsWith(l, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            sortName = PinyinConverter.BuildSortName(candidate.Name);
            return !string.IsNullOrEmpty(sortName);
        }

        /// <summary>Returns the number of updated rows, or -1 when the transaction failed.</summary>
        private int ApplyBatch(IDatabaseConnection db, List<CandidateRow> rows, List<string> locations)
        {
            db.Execute("BEGIN IMMEDIATE");
            try
            {
                db.Execute(CreateBackupTableSql);

                var updated = 0;
                foreach (var row in rows)
                {
                    if (!IsEligible(row, locations, out var sortName)) continue;
                    if (string.Equals(sortName, row.SortName, StringComparison.Ordinal)) continue;

                    // Statements cannot be re-stepped after DONE without a Reset, and
                    // IStatement has no Reset, so both are rebuilt per row.
                    using (var backup = db.PrepareStatement(
                               $"INSERT OR IGNORE INTO {BackupTable} (ItemId, OriginalSortName) VALUES (?, ?)"))
                    {
                        backup.BindParameters[0].Bind(row.Id);
                        if (row.SortName == null) backup.BindParameters[1].BindNull();
                        else backup.BindParameters[1].Bind(row.SortName);
                        backup.MoveNext();
                    }

                    using (var update = db.PrepareStatement("UPDATE MediaItems SET SortName = ? WHERE Id = ?"))
                    {
                        update.BindParameters[0].Bind(sortName);
                        update.BindParameters[1].Bind(row.Id);
                        update.MoveNext();
                    }

                    updated++;
                }

                db.Execute("COMMIT");
                return updated;
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

                _logger.Error("PinyinSortName - batch update failed: {0}", ex.Message);
                return -1;
            }
        }

        /// <summary>Returns the number of restored rows, or -1 when the transaction failed.</summary>
        private int RestoreBatch(IDatabaseConnection db, IReadOnlyList<EmbyDatabase.Row> rows)
        {
            db.Execute("BEGIN IMMEDIATE");
            try
            {
                var restored = 0;
                foreach (var row in rows)
                {
                    var itemId = row.GetInt64(0);
                    var original = row.GetString(1);

                    using (var update = db.PrepareStatement("UPDATE MediaItems SET SortName = ? WHERE Id = ?"))
                    {
                        if (original == null) update.BindParameters[0].BindNull();
                        else update.BindParameters[0].Bind(original);
                        update.BindParameters[1].Bind(itemId);
                        update.MoveNext();
                    }

                    restored++;
                }

                db.Execute("COMMIT");
                return restored;
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

                _logger.Error("PinyinSortName - batch restore failed: {0}", ex.Message);
                return -1;
            }
        }

        private List<string> GetLocationsInScope()
        {
            var locations = new List<string>();

            try
            {
                var scope = Plugin.Instance?.GetPluginOptions().PinyinEnhanceOptions.LibraryScope;
                var ids = (scope ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var library in _libraryManager.GetVirtualFolders())
                {
                    if (LibraryApi.ExcludedCollectionTypes.Contains(library.CollectionType)) continue;
                    if (ids.Length > 0 && !ids.Contains(library.Id)) continue;

                    foreach (var location in library.Locations ?? Array.Empty<string>())
                    {
                        if (string.IsNullOrEmpty(location)) continue;
                        locations.Add(location.EndsWith(Path.DirectorySeparatorChar.ToString())
                            ? location
                            : location + Path.DirectorySeparatorChar);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug("PinyinSortName - failed to resolve library scope: {0}", ex.Message);
            }

            return locations;
        }

        private readonly struct CandidateRow
        {
            internal CandidateRow(long id, string name, string sortName, long type, bool isLocked,
                string lockedFields, string path)
            {
                Id = id;
                Name = name;
                SortName = sortName;
                Type = type;
                IsLocked = isLocked;
                LockedFields = lockedFields;
                Path = path;
            }

            internal long Id { get; }

            internal string Name { get; }

            internal string SortName { get; }

            internal long Type { get; }

            internal bool IsLocked { get; }

            internal string LockedFields { get; }

            internal string Path { get; }
        }
    }
}
