using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Builders;

/// <summary>
/// Non-generic builder interface for creating persistence providers.
/// </summary>
public interface IPersistenceProviderBuilder
{
    /// <summary>
    /// Builds the configured provider instance.
    /// </summary>
    IPersistenceProvider Build();
}

/// <summary>
/// Legacy generic builder interface for backward compatibility.
/// </summary>
public interface IPersistenceProviderBuilder<TValue> : IPersistenceProviderBuilder
{
    /// <summary>
    ///     Builds the configured provider instance.
    /// </summary>
    new IPersistenceProvider<TValue> Build();
}