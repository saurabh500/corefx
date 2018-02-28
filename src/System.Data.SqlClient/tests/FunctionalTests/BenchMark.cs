// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Data.SqlClient.Tests
{
    public class BenchMark
    {

        [Fact]
        public async void TestMars()
        {
            string connectionString = "server=tcp:ss-desktop2,1437;uid=saurabh;pwd=pwd;MultipleActiveResultSets=True;Initial Catalog=NorthWind;";
            string query = @"SELECT [c].[City]
FROM [Customers] AS [c]
ORDER BY [c].[CustomerID], [c].[Country]";
            using(SqlConnection c = new SqlConnection(connectionString))
            {
                await c.OpenAsync();
                using (SqlCommand command = c.CreateCommand())
                {
                    command.CommandText = query;
                    SqlDataReader reader = await command.ExecuteReaderAsync();
                    while(reader.Read())
                    {
                        Console.WriteLine(reader.GetValue(0));
                    }
                }
            }
        }

        private static readonly IDictionary<string, DriverBase> _drivers
            = new Dictionary<string, DriverBase>
            {
                {
                    "ado-sqlclient", new AdoDriver(SqlClientFactory.Instance) 
                }
            };

        [Fact]
        public async Task TestBenchMarkAsync()
        {
#if DEBUG
            Console.WriteLine("WARNING! Using DEBUG build.");
#endif
            

            
            var driverName = "ado-sqlclient";

            _drivers.TryGetValue(driverName, out var driver);
            
            var connectionString = "Data Source=ss-desktop2; uid=saurabh; pwd=pwd; Initial Catalog=fortune; MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=true";

            var variationName = "async";

            var variation = driver.TryGetVariation(variationName);

            
            var threadCount = DefaultThreadCount;
            var time = DefaultExecutionTimeSeconds;

            
            driver.Initialize(connectionString, threadCount);

            DateTime startTime = default, stopTime = default;

            var totalTransactions = 0;
            var results = new List<double>();

            Console.WriteLine($"Using Variateion {variationName} with connection String {connectionString}");
            IEnumerable<Task> CreateTasks()
            {
                yield return Task.Run(
                    async () =>
                    {
                        Console.Write($"Warming up for {WarmupTimeSeconds}s...");

                        await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds));

                        Console.SetCursorPosition(0, Console.CursorTop);

                        Interlocked.Exchange(ref _counter, 0);

                        startTime = DateTime.UtcNow;
                        DateTime lastDisplay = startTime;

                        while (IsRunning)
                        {
                            await Task.Delay(200);

                            DateTime now = DateTime.UtcNow;
                            var tps = (int)(_counter / (now - lastDisplay).TotalSeconds);
                            var remaining = (int)(time - (now - startTime).TotalSeconds);

                            results.Add(tps);

                            Console.Write($"{tps} tps, {remaining}s                     ");
                            Console.SetCursorPosition(0, Console.CursorTop);

                            lastDisplay = now;
                            totalTransactions += Interlocked.Exchange(ref _counter, 0);
                        }
                    });

                yield return Task.Run(
                    async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(WarmupTimeSeconds + time));

                        Interlocked.Exchange(ref _running, 0);

                        stopTime = DateTime.UtcNow;
                    });

                foreach (var task in Enumerable.Range(0, threadCount)
                    .Select(_ => Task.Factory.StartNew(variation, TaskCreationOptions.LongRunning).Unwrap()))
                {
                    yield return task;
                }
            }

            Interlocked.Exchange(ref _running, 1);

            await Task.WhenAll(CreateTasks());

            var totalTps = (int)(totalTransactions / (stopTime - startTime).TotalSeconds);

            results.Sort();
            results.RemoveAt(0);
            results.RemoveAt(results.Count - 1);

            double CalculateStdDev(ICollection<double> values)
            {
                var avg = values.Average();
                var sum = values.Sum(d => Math.Pow(d - avg, 2));

                return Math.Sqrt(sum / values.Count);
            }

            var stdDev = CalculateStdDev(results);

            Console.SetCursorPosition(0, Console.CursorTop);
            Console.WriteLine($"{driverName} {variationName} {threadCount:D2} Threads, tps: {totalTps:F2}, stddev(w/o best+worst): {stdDev:F2}");

            var desc = $"{driverName}+{variationName}+{threadCount}";

            
            (driver as IDisposable)?.Dispose();

        }

        private const int DefaultThreadCount = 64;
        private const int DefaultExecutionTimeSeconds = 10;
        private const int WarmupTimeSeconds = 3;

        public const string TestQuery = "SELECT id, message FROM fortune";

        private static int _counter;

        public static void IncrementCounter() => Interlocked.Increment(ref _counter);

        private static int _running;

        public static bool IsRunning => _running == 1;

        
    }

    public class AdoDriver : DriverBase
    {
        protected readonly DbProviderFactory _providerFactory;
        protected string _connectionString;

        public AdoDriver(DbProviderFactory providerFactory)
        {
            _providerFactory = providerFactory;
        }

        public override void Initialize(string connectionString, int _)
        {
            _connectionString = connectionString;
        }

        public override Task DoWorkSync()
        {
            while (BenchMark.IsRunning)
            {
                var results = new List<Fortune>();

                using (var connection = _providerFactory.CreateConnection())
                {
                    connection.ConnectionString = _connectionString;
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = BenchMark.TestQuery;
                        command.Prepare();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(
                                    new Fortune
                                    {
                                        Id = reader.GetInt32(0),
                                        Message = reader.GetString(1)
                                    });
                            }
                        }
                    }
                }

                CheckResults(results);

                BenchMark.IncrementCounter();
            }

            return Task.CompletedTask;
        }

        public override Task DoWorkSyncCaching()
        {
            using (var connection = _providerFactory.CreateConnection())
            {
                connection.ConnectionString = _connectionString;
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = BenchMark.TestQuery;
                    command.Prepare();

                    while (BenchMark.IsRunning)
                    {
                        var results = new List<Fortune>();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                results.Add(
                                    new Fortune
                                    {
                                        Id = reader.GetInt32(0),
                                        Message = reader.GetString(1)
                                    });
                            }
                        }

                        CheckResults(results);

                        BenchMark.IncrementCounter();
                    }
                }
            }

            return Task.CompletedTask;
        }

        public override async Task DoWorkAsync()
        {
            while (BenchMark.IsRunning)
            {
                var results = new List<Fortune>();

                using (var connection = _providerFactory.CreateConnection())
                {
                    connection.ConnectionString = _connectionString;

                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = BenchMark.TestQuery;
                        command.Prepare();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(
                                    new Fortune
                                    {
                                        Id = reader.GetInt32(0),
                                        Message = reader.GetString(1)
                                    });
                            }
                        }
                    }
                }

                CheckResults(results);

                BenchMark.IncrementCounter();
            }
        }

        public override async Task DoWorkAsyncCaching()
        {
            using (var connection = _providerFactory.CreateConnection())
            {
                connection.ConnectionString = _connectionString;

                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = BenchMark.TestQuery;
                    command.Prepare();

                    while (BenchMark.IsRunning)
                    {
                        var results = new List<Fortune>();

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(
                                    new Fortune
                                    {
                                        Id = reader.GetInt32(0),
                                        Message = reader.GetString(1)
                                    });
                            }
                        }

                        CheckResults(results);

                        BenchMark.IncrementCounter();
                    }
                }
            }
        }
    }

    public abstract class DriverBase
    {
        public static Func<Task> NotSupportedVariation = () => null;

        protected static void CheckResults(ICollection<Fortune> results)
        {
            if (results.Count != 12)
            {
                throw new InvalidOperationException($"Unexpected number of results! Expected 12 got {results.Count}");
            }
        }

        public virtual Func<Task> TryGetVariation(string variationName)
        {
            switch (variationName)
            {
                case Variation.Sync:
                    return DoWorkSync;
                case Variation.SyncCaching:
                    return DoWorkSyncCaching;
                case Variation.Async:
                    return DoWorkAsync;
                case Variation.AsyncCaching:
                    return DoWorkAsyncCaching;
            }

            return default;
        }

        public abstract void Initialize(string connectionString, int threadCount);

        public virtual Task DoWorkSync()
        {
            throw VariationNotSupported(Variation.Sync);
        }

        public virtual Task DoWorkSyncCaching()
        {
            throw VariationNotSupported(Variation.SyncCaching);
        }

        public virtual Task DoWorkAsync()
        {
            throw VariationNotSupported(Variation.Async);
        }

        public virtual Task DoWorkAsyncCaching()
        {
            throw VariationNotSupported(Variation.AsyncCaching);
        }

        private static Exception VariationNotSupported(string variationName)
            => new NotSupportedException($"Variation {variationName} not supported on driver.");
    }

    public class Fortune
    {
        public int Id { get; set; }
        public string Message { get; set; }
    }

    public static class Variation
    {
        public const string Sync = "sync";
        public const string SyncCaching = "sync-caching";
        public const string Async = "async";
        public const string AsyncCaching = "async-caching";

        public static IEnumerable<string> Names
        {
            get
            {
                yield return Sync;
                yield return SyncCaching;
                yield return Async;
                yield return AsyncCaching;
            }
        }
    }
}
