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
        private const string TestDictionaryName = "test";

        public PersistentDictionaryTests()
        {
            _mockProvider = new Mock<IPersistenceProvider<string>>(MockBehavior.Strict);
            _mockLogger = new Mock<ILogger<PersistentDictionary<string>>>();
            
            // Setup common Options property
            var mockOptions = new Mock<IPersistenceOptions>();
            mockOptions.Setup(o => o.BatchInterval).Returns(TimeSpan.Zero);
            mockOptions.Setup(o => o.BatchSize).Returns(1);
            mockOptions.Setup(o => o.MaxRetryAttempts).Returns(3);
            mockOptions.Setup(o => o.RetryDelay).Returns(TimeSpan.FromMilliseconds(100));
            mockOptions.Setup(o => o.ThrowOnPersistenceFailure).Returns(false);
            _mockProvider.Setup(p => p.Options).Returns(mockOptions.Object);
        }

        [Fact]
        public async Task InitializeAsync_ShouldLoadExistingData()
        {
            // arrange
            var existingData = new Dictionary<string, string> { { "k1", "v1" } };
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockProvider.Setup(p => p.LoadAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingData);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName);

            // act
            await dict.InitializeAsync();

            // assert
            Assert.Equal("v1", dict["k1"]);
            _mockProvider.Verify();
        }

        [Fact]
        public async Task InitializeAsync_WhenNoExistingData_ShouldStartEmpty()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName);

            // act
            await dict.InitializeAsync();

            // assert
            Assert.Empty(dict);
            _mockProvider.Verify();
        }

        [Fact]
        public async Task AddAndSaveAsync_ShouldAddItemAndSave()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockProvider.Setup(p => p.SaveAsync(TestDictionaryName, It.Is<Dictionary<string, string>>(d => d.ContainsKey("k1") && d["k1"] == "v1"), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName);
            await dict.InitializeAsync();

            // act
            await dict.AddAndSaveAsync("k1", "v1");

            // assert
            Assert.Equal("v1", dict["k1"]);
            _mockProvider.Verify();
        }

        [Fact]
        public async Task RemoveAndSaveAsync_ShouldRemoveItemAndSave()
        {
            // arrange
            var existingData = new Dictionary<string, string> { { "k1", "v1" } };
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockProvider.Setup(p => p.LoadAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingData);
            _mockProvider.Setup(p => p.SaveAsync(TestDictionaryName, It.Is<Dictionary<string, string>>(d => !d.ContainsKey("k1")), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName);
            await dict.InitializeAsync();

            // act
            await dict.RemoveAndSaveAsync("k1");

            // assert
            Assert.False(dict.ContainsKey("k1"));
            _mockProvider.Verify();
        }

        [Fact]
        public async Task ClearAndSaveAsync_ShouldClearAllItemsAndSave()
        {
            // arrange
            var existingData = new Dictionary<string, string> { { "k1", "v1" }, { "k2", "v2" } };
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _mockProvider.Setup(p => p.LoadAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingData);
            _mockProvider.Setup(p => p.SaveAsync(TestDictionaryName, It.Is<Dictionary<string, string>>(d => d.Count == 0), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName);
            await dict.InitializeAsync();

            // act
            await dict.ClearAndSaveAsync();

            // assert
            Assert.Empty(dict);
            _mockProvider.Verify();
        }

        [Fact]
        public async Task FlushAsync_ShouldSavePendingChanges()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _mockProvider.Setup(p => p.SaveAsync(TestDictionaryName, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName, _mockLogger.Object);
            await dict.InitializeAsync();

            dict["k1"] = "v1"; // Direct assignment should be batched

            // act
            await dict.FlushAsync();

            // assert
            _mockProvider.Verify(p => p.SaveAsync(TestDictionaryName, It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task Dispose_ShouldDisposeProvider()
        {
            // arrange
            var disposableProvider = new Mock<IPersistenceProvider<string>>();
            disposableProvider.As<IDisposable>();
            disposableProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var dict = new PersistentDictionary<string>(disposableProvider.Object, TestDictionaryName, _mockLogger.Object);
            await dict.InitializeAsync();

            // act
            dict.Dispose();

            // assert - no exception should be thrown
        }

        [Fact]
        public async Task Constructor_WithLogger_ShouldAcceptLogger()
        {
            // arrange
            _mockProvider.Setup(p => p.ExistsAsync(TestDictionaryName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // act
            var dict = new PersistentDictionary<string>(_mockProvider.Object, TestDictionaryName, _mockLogger.Object);
            await dict.InitializeAsync();

            // assert
            Assert.NotNull(dict);
            Assert.Equal(TestDictionaryName, dict.DictionaryName);
        }
    }
}