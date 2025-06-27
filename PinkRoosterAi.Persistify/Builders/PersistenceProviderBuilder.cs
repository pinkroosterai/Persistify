namespace PinkRoosterAi.Persistify.Builders
{
  public static class PersistenceProviderBuilder
  {
    public static JsonFilePersistenceProviderBuilder<TKey, TValue> JsonFile<TKey, TValue>()
        where TKey : notnull
    {
      return new JsonFilePersistenceProviderBuilder<TKey, TValue>();
    }

    public static DatabasePersistenceProviderBuilder<TKey, TValue> Database<TKey, TValue>()
        where TKey : notnull
    {
      return new DatabasePersistenceProviderBuilder<TKey, TValue>();
    }
  }
}
