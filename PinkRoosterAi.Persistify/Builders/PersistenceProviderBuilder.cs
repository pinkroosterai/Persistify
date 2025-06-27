namespace PinkRoosterAi.Persistify.Builders;

public static class PersistenceProviderBuilder
{
    public static JsonFilePersistenceProviderBuilder<TValue> JsonFile<TValue>()
    {
        return new JsonFilePersistenceProviderBuilder<TValue>();
    }

    public static DatabasePersistenceProviderBuilder<TValue> Database<TValue>()
    {
        return new DatabasePersistenceProviderBuilder<TValue>();
    }
}