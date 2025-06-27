namespace PinkRoosterAi.Persistify.Samples;

using System;
using System.Threading.Tasks;
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Builders;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Persistify Sample Console Application");

        // 1. JSON-Backed Persistence
        Console.WriteLine("\n--- JSON-Backed Persistence ---");
        var jsonProvider = PersistenceProviderBuilder.JsonFile<string, int>()
            .WithFilePath("persistify_data.json")
            .WithBatch(batchSize: 3, batchInterval: TimeSpan.FromSeconds(5))
            .Build();

        var jsonDict = new PersistentDictionary<string, int>(jsonProvider);
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
        Console.WriteLine("\n--- Database-Backed Persistence ---");
        var dbProvider = PersistenceProviderBuilder.Database<string, int>()
            .WithConnectionString("Data Source=sample.db;")
            .WithTableName("SampleTable")
            .WithColumns("Key", "Value")
            .WithBatch(batchSize: 2, batchInterval: TimeSpan.FromSeconds(3))
            .Build();

        var dbDict = new PersistentDictionary<string, int>(dbProvider);
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
        Console.WriteLine("\n--- Automatic Retry & Error Events ---");
        var badJsonProvider = PersistenceProviderBuilder.JsonFile<string, int>()
            .WithFilePath("?:/invalid_path.json")
            .ThrowOnFailure(false)
            .Build();

        var badDict = new PersistentDictionary<string, int>(badJsonProvider);
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
        Console.WriteLine("\n--- Batched Commits ---");
        var batchProvider = PersistenceProviderBuilder.JsonFile<string, int>()
            .WithFilePath("batch_data.json")
            .WithBatch(batchSize: 3, batchInterval: TimeSpan.FromSeconds(5))
            .Build();

        var batchDict = new PersistentDictionary<string, int>(batchProvider);
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
        Console.WriteLine("\n--- In-Memory Caching with TTL Eviction ---");
        var cacheProvider = PersistenceProviderBuilder.JsonFile<string, int>()
            .WithFilePath("cache_data.json")
            .Build();

        var cacheDict = new CachingPersistentDictionary<string, int>(cacheProvider, TimeSpan.FromSeconds(10));
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
