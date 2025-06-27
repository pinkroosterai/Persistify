namespace PinkRoosterAi.Persistify.Builders;

public static class PersistenceProviderBuilder
{
    /// <summary>
    /// Creates a non-generic JSON file persistence provider builder.
    /// </summary>
    public static JsonFilePersistenceProviderBuilder<object> JsonFile()
    {
        return new JsonFilePersistenceProviderBuilder<object>();
    }

    /// <summary>
    /// Creates a generic JSON file persistence provider builder.
    /// </summary>
    public static JsonFilePersistenceProviderBuilder<TValue> JsonFile<TValue>()
    {
        return new JsonFilePersistenceProviderBuilder<TValue>();
    }

    /// <summary>
    /// Creates a non-generic database persistence provider builder.
    /// </summary>
    public static DatabasePersistenceProviderBuilder<object> Database()
    {
        return new DatabasePersistenceProviderBuilder<object>();
    }

    /// <summary>
    /// Creates a generic database persistence provider builder.
    /// </summary>
    public static DatabasePersistenceProviderBuilder<TValue> Database<TValue>()
    {
        return new DatabasePersistenceProviderBuilder<TValue>();
    }
}