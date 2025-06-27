using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Abstractions;
using Xunit;

namespace PinkRoosterAi.Persistify.Tests
{
    public class CachingPersistentDictionaryTests
    {
        private readonly Mock<IPersistenceProvider<string, string>> _mockProvider;
        private readonly Mock<ILogger<PersistentDictionary<string, string>>> _mockLogger;

        public CachingPersistentDictionaryTests()
        {
            _mockProvider = new Mock<IPersistenceProvider<string, string>>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<PersistentDictionary<string, string>>>();
        }

        [Fact]
        public async Task InitializeAsync_ShouldPopulateLastReadTimes()
        {
            var data = new Dictionary<string, string> { { "k1", "v1" } };

            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _mockProvider.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(data);

            var dict = new CachingPersistentDictionary<string, string>(_mockProvider.Object, TimeSpan.FromMinutes(5));

            await dict.InitializeAsync();

            var lastReadField = typeof(CachingPersistentDictionary<string, string>)
                .GetField("_lastReadAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var lastRead = (Dictionary<string, DateTime>)lastReadField.GetValue(dict)!;

            Assert.True(lastRead.ContainsKey("k1"));
            Assert.True(lastRead["k1"] <= DateTime.UtcNow);
        }

        [Fact]
        public async Task OnAccess_ShouldUpdateLastReadAndTriggerEviction()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var dict = new CachingPersistentDictionary<string, string>(_mockProvider.Object, TimeSpan.FromMinutes(1));

            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            var lastReadField = typeof(CachingPersistentDictionary<string, string>)
                .GetField("_lastReadAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            // simulate
            dict["k1"] = "v1"; // this will invoke OnAccess

            var lastRead = (Dictionary<string, DateTime>)lastReadField.GetValue(dict)!;

            Assert.True(lastRead.ContainsKey("k1"));
            Assert.True(lastRead["k1"] <= DateTime.UtcNow);
        }

        [Fact]
        public async Task OnMutation_ShouldUpdateLastReadAndLastUpdated()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var dict = new CachingPersistentDictionary<string, string>(_mockProvider.Object, TimeSpan.FromMinutes(1));
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            dict["k1"] = "v2"; // triggers OnMutation

            var lastReadField = typeof(CachingPersistentDictionary<string, string>)
                .GetField("_lastReadAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var lastUpdatedField = typeof(CachingPersistentDictionary<string, string>)
                .GetField("_lastUpdatedAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var lastRead = (Dictionary<string, DateTime>)lastReadField.GetValue(dict)!;
            var lastUpdated = (Dictionary<string, DateTime>)lastUpdatedField.GetValue(dict)!;

            Assert.True(lastRead.ContainsKey("k1"));
            Assert.True(lastUpdated.ContainsKey("k1"));
            Assert.True(lastRead["k1"] <= DateTime.UtcNow);
            Assert.True(lastUpdated["k1"] <= DateTime.UtcNow);
        }

        [Fact]
        public async Task EvictExpiredEntries_ShouldRemoveStaleKeys()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var dict = new TestableCachingPersistentDictionary(_mockProvider.Object, TimeSpan.FromMilliseconds(1));

            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            // simulate expired by sleeping
            await Task.Delay(10);

            // force eviction by accessing
            dict["k1"] = "v1"; // triggers OnAccess

            Assert.False(dict.ContainsKey("k1"));
            Assert.True(dict.RemovedKeys.Contains("k1")); // tracked by subclass
        }

        [Fact]
        public async Task ShouldNotEvictBeforeTTL()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var dict = new TestableCachingPersistentDictionary(_mockProvider.Object, TimeSpan.FromSeconds(5));

            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            // force access to refresh time but no expiration
            dict["k1"] = "v1"; // triggers OnAccess

            Assert.True(dict.ContainsKey("k1"));
            Assert.Empty(dict.RemovedKeys);
        }

        /// <summary>
        /// Subclass to monitor RemoveAndSaveAsync calls since the base fires them fire-and-forget
        /// </summary>
        private class TestableCachingPersistentDictionary : CachingPersistentDictionary<string, string>
        {
            public List<string> RemovedKeys { get; } = new();

            public TestableCachingPersistentDictionary(IPersistenceProvider<string, string> provider, TimeSpan ttl)
                : base(provider, ttl)
            {
            }

            public override async Task<bool> RemoveAndSaveAsync(string key, CancellationToken cancellationToken = default)
            {
                RemovedKeys.Add(key);
                return await Task.FromResult(true);
            }
        }
    }
}
