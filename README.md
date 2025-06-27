<div align="center">
  <img src="img/logo_transparent.png" alt="PinkRoosterAi.Persistify Logo" width="300">
</div>

# PinkRoosterAi.Persistify

**A thread-safe, extensible persistent dictionary for .NET**  
Async, batched, and retryable key-value storage with support for JSON files and databases.

---

## Features

- **PersistentDictionary<TValue>**
  - Thread-safe, string-keyed dictionary with async persistence
  - Batch commit and flush support
  - Retry logic with exponential backoff (Polly)
  - Event hooks for persistence errors
  - JSON file and database (SQLite/PostgreSQL) backends

- **CachingPersistentDictionary<TValue>**
  - Inherits all features of PersistentDictionary
  - Tracks last access and update times
  - Evicts stale entries from memory and persistence
  - Configurable TTL

- **Flexible Persistence Providers**
  - JSON file: atomic file replacement
  - Database: ServiceStack.OrmLite support

- **Builder Pattern**
  - Chainable configuration for providers

- **Batching & Retry**
  - Control batch size, timing, and error handling

- **Metadata Support**
  - Retrieve last-updated timestamps (if supported by provider)

---

## Usage

### Creating a Persistent Dictionary

```csharp
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Builders;

var provider = PersistenceProviderBuilder.JsonFile<int>()
    .WithFilePath("data.json")
    .WithBatch(batchSize: 10, batchInterval: TimeSpan.FromSeconds(5))
    .Build();

var dict = new PersistentDictionary<int>(provider);
await dict.InitializeAsync();

await dict.AddAndSaveAsync("foo", 42);
await dict.RemoveAndSaveAsync("foo");
```

### Using CachingPersistentDictionary

```csharp
using PinkRoosterAi.Persistify;

var cachingDict = new CachingPersistentDictionary<int>(provider, TimeSpan.FromMinutes(10));
await cachingDict.InitializeAsync();

await cachingDict.AddAndSaveAsync("key1", 42);
// "key1" will be evicted after 10 minutes of no access
```

### Database Persistence

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

## Dependencies

- [.NET 9.0](https://dotnet.microsoft.com/)
- [Polly](https://github.com/App-vNext/Polly)
- [ServiceStack.OrmLite](https://github.com/ServiceStack/ServiceStack.OrmLite)
- [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)

---

## Components

- `PersistentDictionary<TValue>` — async, batch, and retry persistence with string keys
- `CachingPersistentDictionary<TValue>` — TTL-based eviction for stale keys
- `JsonFilePersistenceProvider<TValue>` — JSON-backed storage
- `DatabasePersistenceProvider<TValue>` — SQL-based storage (SQLite/PostgreSQL)
- `IPersistenceProvider<TValue>` — pluggable persistence abstraction
- `IPersistenceOptions` — configure batching, retries, and more
- `IPersistenceMetadataProvider` — last-updated timestamps (if supported)
- Builder classes for easy configuration

---

## Notes

- All mutation methods are async — always `await` them
- Always call `InitializeAsync()` before using the dictionary
- No synchronous mutation methods
- Custom providers can be implemented via `IPersistenceProvider<TValue>`

---

## License

This project is licensed under the [MIT License](https://opensource.org/licenses/MIT).

---

## Feedback

Contributions and suggestions are welcome! Open an issue or send a PR.

