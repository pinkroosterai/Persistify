using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Builders;

public interface IPersistenceProviderBuilder<TValue>
{
    /// <summary>
    ///     Builds the configured provider instance.
    /// </summary>
    IPersistenceProvider<TValue> Build();
}