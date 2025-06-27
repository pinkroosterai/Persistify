using PinkRoosterAi.Persistify.Abstractions;

namespace PinkRoosterAi.Persistify.Builders
{
  public interface IPersistenceProviderBuilder<TKey, TValue>
      where TKey : notnull
  {
    /// <summary>
    ///   Builds the configured provider instance.
    /// </summary>
    IPersistenceProvider<TKey, TValue> Build();
  }
}
