using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using SQLitePCL.pretty;

namespace StrmAssistant.Pinyin
{
    /// <summary>
    ///     Thin wrapper around the Emby-managed library.db connection.
    ///     The connection is obtained by reflecting
    ///     <c>SqliteItemRepository.ConnectionManager</c> (public getter on
    ///     <c>BaseSqliteRepository</c>, identical on 4.9.5.0 and 4.10.2.0) and
    ///     calling <c>CreateConnection(false, CancellationToken.None)</c>.
    ///     On hosts where the reflection chain is unavailable (e.g. 4.8.11.0 uses
    ///     a single private connection) the class degrades to IsAvailable=false
    ///     and every pinyin feature turns itself off with a single log line,
    ///     following the Common/ChapterQueryCompatibility.cs soft-failure pattern.
    /// </summary>
    internal sealed class EmbyDatabase
    {
        private const string FtsTableProbeSql =
            "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'fts_search%' " +
            "AND instr(substr(name, 10), '_') = 0 ORDER BY name DESC";

        private readonly ILogger _logger;
        private readonly object _sync = new object();

        private bool _resolved;
        private object _connectionManager;
        private System.Reflection.MethodInfo _createConnection;

        internal EmbyDatabase(ILogger logger)
        {
            _logger = logger;
        }

        internal bool IsAvailable
        {
            get
            {
                EnsureResolved();
                return _connectionManager != null && _createConnection != null;
            }
        }

        private void EnsureResolved()
        {
            if (_resolved) return;

            lock (_sync)
            {
                if (_resolved) return;

                try
                {
                    var repository = Plugin.Instance?.ItemRepository;
                    if (repository == null)
                    {
                        _logger.Warn("PinyinDatabase - IItemRepository unavailable, pinyin features disabled");
                    }
                    else
                    {
                        var type = repository.GetType();
                        var property = FindProperty(type, "ConnectionManager");
                        if (property == null)
                        {
                            _logger.Warn(
                                "PinyinDatabase - ConnectionManager not found on {0}, pinyin features disabled",
                                type.FullName);
                        }
                        else
                        {
                            var manager = property.GetValue(repository, null);
                            if (manager == null)
                            {
                                _logger.Warn("PinyinDatabase - ConnectionManager is null, pinyin features disabled");
                            }
                            else
                            {
                                var create = FindCreateConnection(manager.GetType());
                                if (create == null)
                                {
                                    _logger.Warn(
                                        "PinyinDatabase - CreateConnection(bool, CancellationToken) not found on {0}, pinyin features disabled",
                                        manager.GetType().FullName);
                                }
                                else
                                {
                                    _connectionManager = manager;
                                    _createConnection = create;
                                    _logger.Info("PinyinDatabase - ConnectionManager resolved on {0}", type.FullName);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn("PinyinDatabase - reflection failed ({0}), pinyin features disabled", ex.Message);
                    _connectionManager = null;
                    _createConnection = null;
                }

                _resolved = true;
            }
        }

        private static System.Reflection.PropertyInfo FindProperty(Type type, string name)
        {
            return type.GetProperty(name, System.Reflection.BindingFlags.Instance |
                                         System.Reflection.BindingFlags.Public |
                                         System.Reflection.BindingFlags.NonPublic);
        }

        private static System.Reflection.MethodInfo FindCreateConnection(Type managerType)
        {
            foreach (var method in managerType.GetMethods(System.Reflection.BindingFlags.Instance |
                                                          System.Reflection.BindingFlags.Public |
                                                          System.Reflection.BindingFlags.NonPublic))
            {
                if (method.Name != "CreateConnection") continue;

                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(bool) &&
                    parameters[1].ParameterType == typeof(CancellationToken))
                {
                    return method;
                }
            }

            return null;
        }

        /// <summary>
        ///     Runs <paramref name="action"/> against the Emby-managed library.db with a
        ///     writable connection. Returns false when the connection could not be created.
        /// </summary>
        internal bool Use(Action<IDatabaseConnection> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            EnsureResolved();
            if (_connectionManager == null || _createConnection == null) return false;

            var connection = OpenConnection();
            if (connection == null) return false;

            try
            {
                action(connection);
                return true;
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // ignore disposal failures on the host-managed connection wrapper
                }
            }
        }

        /// <summary>
        ///     Runs <paramref name="action"/> and returns its result, or
        ///     <c>null</c> when the connection could not be created.
        /// </summary>
        internal T Use<T>(Func<IDatabaseConnection, T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            EnsureResolved();
            if (_connectionManager == null || _createConnection == null) return default;

            var connection = OpenConnection();
            if (connection == null) return default;

            try
            {
                return action(connection);
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // ignore disposal failures on the host-managed connection wrapper
                }
            }
        }

        private IDatabaseConnection OpenConnection()
        {
            try
            {
                return (IDatabaseConnection)_createConnection.Invoke(_connectionManager,
                    new object[] { false, CancellationToken.None });
            }
            catch (Exception ex)
            {
                _logger.Info("PinyinDatabase - CreateConnection failed ({0})", ex.InnerException?.Message ?? ex.Message);
                return null;
            }
        }

        internal string DetectFtsTable()
        {
            return Use(db =>
            {
                using (var statement = db.PrepareStatement(FtsTableProbeSql))
                {
                    while (statement.MoveNext())
                    {
                        var name = statement.Current.GetString(0);
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }

                return null;
            });
        }

        internal static IReadOnlyList<Row> Query(IDatabaseConnection db, string sql, params object[] args)
        {
            var rows = new List<Row>();
            using (var statement = db.PrepareStatement(sql))
            {
                Bind(statement, args);
                var columnCount = statement.Columns.Count;
                while (statement.MoveNext())
                {
                    var result = statement.Current;
                    var fields = new object[columnCount];
                    for (var i = 0; i < columnCount; i++)
                    {
                        fields[i] = ReadValue(result, i);
                    }

                    rows.Add(new Row(fields));
                }
            }

            return rows;
        }

        internal static int Execute(IDatabaseConnection db, string sql, params object[] args)
        {
            using (var statement = db.PrepareStatement(sql))
            {
                Bind(statement, args);
                statement.MoveNext();
                return db.Changes;
            }
        }

        private static object ReadValue(IResultSet result, int index)
        {
            switch (result.GetSQLiteType(index))
            {
                case SQLiteType.Integer:
                    return result.GetInt64(index);
                case SQLiteType.Float:
                    return result.GetDouble(index);
                case SQLiteType.Text:
                    return result.GetString(index);
                case SQLiteType.Blob:
                    return result.GetBlob(index).ToArray();
                default:
                    return null;
            }
        }

        private static void Bind(IStatement statement, object[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var value = args[i];
                var binder = statement.BindParameters[i];
                if (value == null)
                {
                    binder.BindNull();
                }
                else
                {
                    switch (value)
                    {
                        case long v:
                            binder.Bind(v);
                            break;
                        case int v:
                            binder.Bind((long)v);
                            break;
                        case string v:
                            binder.Bind(v);
                            break;
                        case double v:
                            binder.Bind(v);
                            break;
                        default:
                            binder.Bind(value.ToString());
                            break;
                    }
                }
            }
        }

        internal readonly struct Row
        {
            private readonly object[] _fields;

            internal Row(object[] fields)
            {
                _fields = fields;
            }

            internal long GetInt64(int index)
            {
                return Convert.ToInt64(_fields[index]);
            }

            internal string GetString(int index)
            {
                return (string)_fields[index];
            }

            internal bool IsNull(int index)
            {
                return _fields[index] == null;
            }
        }
    }
}
