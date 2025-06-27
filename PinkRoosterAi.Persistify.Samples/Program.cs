namespace PinkRoosterAi.Persistify.Samples;

using System;
using System.Threading.Tasks;
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Builders;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Persistify Sample Console Application");

        // 1. JSON-Backed Persistence
        // What it shows: Creating a persistent dictionary backed by a JSON file, with batched writes and automatic flushing.
        // Why it's cool: Simplifies local data storage with human-readable files, supports batching for performance, and ensures data durability with minimal code.
        Console.WriteLine("\n--- JSON-Backed Persistence ---");
        var jsonProvider = PersistenceProviderBuilder.JsonFile<int>()
            .WithFilePath("./persistify_data")
            .WithBatch(batchSize: 3, batchInterval: TimeSpan.FromSeconds(5))
            .Build();

        var jsonDict = jsonProvider.CreateDictionary("sample-dict");
        await jsonDict.InitializeAsync();

        Console.WriteLine("Adding values to JSON dictionary...");
        await jsonDict.AddAndSaveAsync("apple", 1);
        await jsonDict.AddAndSaveAsync("banana", 2);
        jsonDict["cherry"] = 3; // buffered mutation
        jsonDict["date"] = 4;   // buffered mutation

        Console.WriteLine("Flushing JSON dictionary...");
        await jsonDict.FlushAsync();

        Console.WriteLine("JSON dictionary contents:");
        foreach (var kvp in jsonDict)
        {
            Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
        }

        // 2. Database-Backed Persistence
        // What it shows: Using a database (via SQL) as the persistence backend, with support for upsert and delete operations.
        // Why it's cool: Enables scalable, reliable storage for large datasets, with transactional integrity and flexible schema control, suitable for production environments.
        Console.WriteLine("\n--- Database-Backed Persistence ---");
        var dbProvider = PersistenceProviderBuilder.Database<int>()
            .WithConnectionString("Data Source=sample.db;")
            .WithBatch(batchSize: 2, batchInterval: TimeSpan.FromSeconds(3))
            .Build();

        var dbDict = dbProvider.CreateDictionary("SampleTable");
        await dbDict.InitializeAsync();

        Console.WriteLine("Adding values to DB dictionary...");
        await dbDict.AddAndSaveAsync("alpha", 10);
        await dbDict.AddAndSaveAsync("beta", 20);
        await dbDict.TryAddAndSaveAsync("gamma", 30);
        await dbDict.RemoveAndSaveAsync("beta");

        Console.WriteLine("Flushing DB dictionary...");
        await dbDict.FlushAsync();

        Console.WriteLine("DB dictionary contents:");
        foreach (var kvp in dbDict)
        {
            Console.WriteLine($"  {kvp.Key} = {kvp.Value}");
        }

        // 3. Automatic Retry & Error Events
        // What it shows: Handling transient failures with Polly retries and event-driven error reporting.
        // Why it's cool: Adds robustness to persistence operations, allowing graceful recovery from intermittent issues and centralized error handling.
        Console.WriteLine("\n--- Automatic Retry & Error Events ---");
        var badJsonProvider = PersistenceProviderBuilder.JsonFile<int>()
            .WithFilePath("?:/invalid_path.json")
            .ThrowOnFailure(false)
            .Build();

        var badDict = badJsonProvider.CreateDictionary("bad-dict");
        badDict.PersistenceError += (sender, e) =>
        {
            Console.WriteLine($"Persistence error on operation {e.Operation}, attempt {e.RetryAttempt}, fatal: {e.IsFatal}");
            Console.WriteLine($"Exception: {e.Exception.Message}");
        };

        try
        {
            await badDict.InitializeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught exception during initialization: {ex.Message}");
        }

        // 4. Batched Commits
        // What it shows: Buffering multiple mutations and flushing them together either when batch size is reached or after a timeout.
        // Why it's cool: Significantly improves performance by reducing I/O operations, ideal for high-throughput scenarios.
        Console.WriteLine("\n--- Batched Commits ---");
        var batchProvider = PersistenceProviderBuilder.JsonFile<int>()
            .WithFilePath("./batch_data")
            .WithBatch(batchSize: 3, batchInterval: TimeSpan.FromSeconds(5))
            .Build();

        var batchDict = batchProvider.CreateDictionary("batch-dict");
        await batchDict.InitializeAsync();

        Console.WriteLine("Performing quick mutations...");
        batchDict["one"] = 1;
        batchDict["two"] = 2;
        batchDict["three"] = 3; // Should trigger auto-flush due to batch size

        Console.WriteLine("Waiting 6 seconds to trigger timer flush...");
        await Task.Delay(TimeSpan.FromSeconds(6));

        Console.WriteLine("Performing fewer mutations than batch size...");
        batchDict["four"] = 4;
        batchDict["five"] = 5;

        Console.WriteLine("Waiting 6 seconds to trigger timer flush...");
        await Task.Delay(TimeSpan.FromSeconds(6));

        // 5. In-Memory Caching with TTL Eviction
        // What it shows: Caching data in memory with a TTL, automatically evicting stale entries.
        // Why it's cool: Combines fast in-memory access with persistence, ensuring data freshness and reducing load on storage backends.
        Console.WriteLine("\n--- In-Memory Caching with TTL Eviction ---");
        var cacheProvider = PersistenceProviderBuilder.JsonFile<int>()
            .WithFilePath("./cache_data")
            .Build();

        var cacheDict = cacheProvider.CreateCachingDictionary("cache-dict", TimeSpan.FromSeconds(10));
        await cacheDict.InitializeAsync();

        Console.WriteLine("Adding and accessing keys...");
        await cacheDict.AddAndSaveAsync("cached1", 100);
        await cacheDict.AddAndSaveAsync("cached2", 200);
        var val1 = cacheDict["cached1"];
        var val2 = cacheDict["cached2"];
        Console.WriteLine($"cached1 = {val1}, cached2 = {val2}");

        Console.WriteLine("Waiting 12 seconds to expire cache entries...");
        await Task.Delay(TimeSpan.FromSeconds(12));

        Console.WriteLine("Accessing dictionary after TTL expiration...");
        try
        {
            var valExpired = cacheDict["cached1"];
            Console.WriteLine($"cached1 after TTL = {valExpired}");
        }
        catch (KeyNotFoundException)
        {
            Console.WriteLine("cached1 was evicted due to TTL expiration.");
        }

        Console.WriteLine("Sample application complete.");
    }
}
