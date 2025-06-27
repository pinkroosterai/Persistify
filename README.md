<div align="center">
  <img src="img/logo_transparent.png" alt="PinkRoosterAi.Persistify Logo" width="300">
</div>

# PinkRoosterAi.Persistify

**Your unstoppable, thread-safe, and extensible persistent dictionary for .NET** — here to save your data from oblivion!
🦸‍♂️✨

PinkRoosterAi.Persistify makes data persistence a breeze with async, batched, and retryable key-value storage. Support
for both JSON files and databases? You bet! 🗃️

---

## 🚀 Features

* **`PersistentDictionary<TValue>`** 🗝️
  A rock-solid, thread-safe dictionary that *automagically* saves your data with string keys.

    * Async-first init & updates
    * Batch commit & flush support
    * Retry logic with exponential backoff (powered by Polly 🐦)
    * Event hooks for handling persistence errors
    * JSON file *and* database backends supported (SQLite/PostgreSQL)

* **`CachingPersistentDictionary<TValue>`** ⏰
  Like `PersistentDictionary`, but with a built-in timer for stale data cleanup!

    * Tracks last access & update times
    * Evicts stale entries from memory *and* the persistence layer
    * Configurable TTL to suit your needs

* **🧩 Flexible Persistence Providers**

    * **JSON File**: Save to a JSON file with atomic file replacement
    * **Database**: Save to relational tables using ServiceStack.OrmLite

* **🛠️ Builder Pattern**
  Easily set up your persistence provider with clean, chainable builder methods.

* **📦 Batching & Retry**

    * Control batch size, timing, and error handling
    * Fine-tune retry behavior to suit your workload

* **🕰️ Metadata Support**

    * Get the last-updated timestamps for your keys (if the provider supports it)

---

## 📝 Usage

### 🍰 Creating a Persistent Dictionary

```csharp
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Builders;

// JSON file provider
var provider = PersistenceProviderBuilder.JsonFile<int>()
    .WithFilePath("data.json")
    .WithBatch(batchSize: 10, batchInterval: TimeSpan.FromSeconds(5))
    .Build();

var dict = new PersistentDictionary<int>(provider);
await dict.InitializeAsync();

// Add and persist
await dict.AddAndSaveAsync("foo", 42);

// Remove and persist
await dict.RemoveAndSaveAsync("foo");
```

---

### ⏳ Using `CachingPersistentDictionary`

```csharp
using PinkRoosterAi.Persistify;

// 10-minute TTL for automatic cleanup
var cachingDict = new CachingPersistentDictionary<int>(provider, TimeSpan.FromMinutes(10));
await cachingDict.InitializeAsync();

// Add something, let the TTL do its magic
await cachingDict.AddAndSaveAsync("key1", 42);

// After 10 minutes of no access, "key1" is evicted from memory & persistence
```

---

### 🗄️ For Database Persistence

```csharp
using PinkRoosterAi.Persistify.Builders;

var dbProvider = PersistenceProviderBuilder.Database<int>()
    .WithConnectionString("Data Source=mydb.sqlite;Version=3;")
    .WithTableName("MyTable")
    .WithColumns("Key", "Value")
    .WithBatch(batchSize: 20, batchInterval: TimeSpan.FromSeconds(10))
    .Build();

var dbDict = new PersistentDictionary<int>(dbProvider);
await dbDict.InitializeAsync();
```

---

## 🧩 Dependencies

* [.NET 9.0](https://dotnet.microsoft.com/) 🦾
* [Polly](https://github.com/App-vNext/Polly) (for retries)
* [ServiceStack.OrmLite](https://github.com/ServiceStack/ServiceStack.OrmLite) (for DB persistence)
* [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)

---

## 🔍 Components

* `PersistentDictionary<TValue>` — async, batch, and retry persistence with string keys
* `CachingPersistentDictionary<TValue>` — TTL-based eviction for stale keys
* `JsonFilePersistenceProvider<TValue>` — JSON-backed storage
* `DatabasePersistenceProvider<TValue>` — SQL-based storage (SQLite/PostgreSQL)
* `IPersistenceProvider<TValue>` — your pluggable persistence abstraction
* `IPersistenceOptions` — tune batching, retries, and more
* `IPersistenceMetadataProvider` — timestamps if you want them
* *Builder* classes — because configuration should be a delight ✨

---

## 🔄 Simplified API (v2.0+)

Starting with version 2.0, PinkRoosterAi.Persistify has been simplified to use **string keys only**. This change:

* **Reduces complexity** — No more generic `TKey` parameter confusion
* **Improves usability** — String keys are the most common use case
* **Maintains performance** — All the same great features, simpler API

### Migration from v1.x

**Before (v1.x):**
```csharp
var provider = PersistenceProviderBuilder.JsonFile<string, int>().Build();
var dict = new PersistentDictionary<string, int>(provider);
```

**After (v2.0+):**
```csharp
var provider = PersistenceProviderBuilder.JsonFile<int>().Build();
var dict = new PersistentDictionary<int>(provider);
```

Simply remove the `TKey` generic parameter — keys are now always strings! 🎉

### 🏭 New Factory Methods

Providers now include convenient factory methods for creating dictionaries:

```csharp
// Create dictionaries directly from providers
var provider = PersistenceProviderBuilder.JsonFile<int>().WithFilePath("data.json").Build();

var dict = provider.CreateDictionary();
var cachingDict = provider.CreateCachingDictionary(TimeSpan.FromMinutes(10));
```

---

## ⚠️ Notes

* All mutation methods are async — don’t forget to `await` them!
* Always call `InitializeAsync()` before using the dictionary
* There are no synchronous mutation methods (by design!)
* Need to customize? Implement your own `IPersistenceProvider<TValue>`. Boom. 💥

---

## 📄 License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT). 🪪 Feel free to use, fork, and
modify as you see fit!

---

## 👋 Feedback welcome!

If you have ideas or suggestions to make Persistify even more awesome, open an issue or send a PR! Contributions are
always loved. 💖

