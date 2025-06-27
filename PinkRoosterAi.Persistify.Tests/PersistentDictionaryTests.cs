using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PinkRoosterAi.Persistify;
using PinkRoosterAi.Persistify.Abstractions;
using Xunit;

namespace PinkRoosterAi.Persistify.Tests
{
    public class PersistentDictionaryTests
    {
        private readonly Mock<IPersistenceProvider<string>> _mockProvider;
        private readonly Mock<ILogger<PersistentDictionary<string>>> _mockLogger;

        public PersistentDictionaryTests()
        {
            _mockProvider = new Mock<IPersistenceProvider<string>>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<PersistentDictionary<string>>>();
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadExistingData()
        {
            // arrange
            var existingData = new Dictionary<string, string> { { "k1", "v1" } };
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockProvider.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingData);

            var dict = new PersistentDictionary<string>(_mockProvider.Object);

            // act
            await dict.InitializeAsync();

            // assert
            Assert.True(dict.IsInitialized);
            Assert.True(dict.ContainsKey("k1"));
            Assert.Equal("v1", dict["k1"]);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLeaveEmpty_WhenNoPersistence()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(_mockProvider.Object);

            await dict.InitializeAsync();

            Assert.True(dict.IsInitialized);
            Assert.Empty(dict);
        }

        [Fact]
        public async Task AddAndSaveAsync_ShouldAddValue()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            var dict = new PersistentDictionary<string>(_mockProvider.Object);
            await dict.InitializeAsync();

            // act
            await dict.AddAndSaveAsync("key", "value");

            // assert
            Assert.Equal("value", dict["key"]);
        }

        [Fact]
        public async Task RemoveAndSaveAsync_ShouldRemoveValue()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(_mockProvider.Object);
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("key", "value");

            // act
            var removed = await dict.RemoveAndSaveAsync("key");

            // assert
            Assert.True(removed);
            Assert.False(dict.ContainsKey("key"));
        }

        [Fact]
        public async Task TryAddAndSaveAsync_ShouldNotAddDuplicate()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(_mockProvider.Object);
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("key", "value");

            var added = await dict.TryAddAndSaveAsync("key", "value2");

            Assert.False(added);
            Assert.Equal("value", dict["key"]);
        }

        [Fact]
        public async Task TryRemoveAndSaveAsync_ShouldHandleNonExistentKey()
        {
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(_mockProvider.Object);
            await dict.InitializeAsync();

            var removed = await dict.TryRemoveAndSaveAsync("missing");

            Assert.False(removed);
        }

        [Fact]
        public async Task FlushAsync_ShouldPersistData()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockProvider.Setup(p => p.SaveAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, _mockLogger.Object);
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            // act
            await dict.FlushAsync();

            // assert
            _mockProvider.Verify(p => p.SaveAsync(It.Is<Dictionary<string, string>>(d => d.ContainsKey("k1")), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Dispose_ShouldFlushAndDisposeProvider()
        {
            var disposableProvider = new Mock<IPersistenceProvider<string>>(MockBehavior.Strict);
            disposableProvider.As<IDisposable>().Setup(d => d.Dispose());
            disposableProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            disposableProvider.Setup(p => p.SaveAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(disposableProvider.Object, _mockLogger.Object);
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            dict.Dispose();

            disposableProvider.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
        }

        [Fact]
        public async Task FlushAsync_ShouldThrow_WhenSaveFails()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockProvider.Setup(p => p.SaveAsync(It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("save failure"));

            var dict = new PersistentDictionary<string>(_mockProvider.Object, _mockLogger.Object);
            await dict.InitializeAsync();
            await dict.AddAndSaveAsync("k1", "v1");

            // act & assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => dict.FlushAsync());
        }
    }
}
