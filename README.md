<div align="center">
  <img src="img/logo_transparent.png" alt="PinkRoosterAi.Persistify Logo" width="300">
</div>

# PinkRoosterAi.Persistify

A thread-safe, asynchronous persistent dictionary for .NET with pluggable storage backends.

## Storage Providers

- JSON File (`JsonFilePersistenceProvider<TValue>`)
  - Configure via `PersistenceProviderBuilder.JsonFile<TValue>()`
  - Chainable configuration:
    - `.WithFilePath(path)`
    - `.WithSerializerOptions(JsonSerializerOptions)`
    - `.WithRetry(maxAttempts, delay)`
    - `.ThrowOnFailure()`
    - `.WithBatch(size, interval)`
- Database (`DatabasePersistenceProvider<TValue>`)
  - Configure via `PersistenceProviderBuilder.Database<TValue>()`
  - Chainable configuration:
    - `.WithConnectionString(connectionString)`
    - `.WithRetry(maxAttempts, delay)`
    - `.ThrowOnFailure()`
    - `.WithBatch(size, interval)`

## Dictionary Types

- `PersistentDictionary<TValue>`
  - Thread-safe `IDictionary<string, TValue>`
  - Async methods:
    - `InitializeAsync()`
    - `AddAndSaveAsync(key, value)`
    - `RemoveAndSaveAsync(key)`
    - `TryAddAndSaveAsync(key, value)`
    - `ClearAndSaveAsync()`
    - `FlushAsync()`
    - `ReloadAsync()`
- `CachingPersistentDictionary<TValue>`
  - In-memory TTL cache on top of `PersistentDictionary<TValue>`
  - Evicts entries after TTL of no access/update
  - Create via `provider.CreateCachingDictionary(name, ttl)`

## Quick Start

```csharp
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Builders;

var provider = PersistenceProviderBuilder.JsonFile<int>()
    .WithFilePath("data")
    .WithBatch(10, TimeSpan.FromSeconds(5))
    .Build();

var dict = provider.CreateDictionary("mydict");
await dict.InitializeAsync();
await dict.AddAndSaveAsync("key1", 42);
await dict.FlushAsync();
```

```csharp
var dbProvider = PersistenceProviderBuilder.Database<string>()
    .WithConnectionString("Data Source=app.db;")
    .WithRetry(5, TimeSpan.FromMilliseconds(200))
    .Build();

var cacheDict = dbProvider.CreateCachingDictionary("cache", TimeSpan.FromMinutes(15));
await cacheDict.InitializeAsync();
```

## Requirements

- .NET 9.0 or later
- [Polly](https://github.com/App-vNext/Polly)
- [ServiceStack.OrmLite](https://github.com/ServiceStack/ServiceStack.OrmLite)
- [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions)

## License

MIT License. See [LICENSE](LICENSE).

