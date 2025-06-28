using System.Text.Json;
using PinkRoosterAi.Persistify.Abstractions;
using PinkRoosterAi.Persistify.Options;
using PinkRoosterAi.Persistify.Providers;

namespace PinkRoosterAi.Persistify.Builders;

public class JsonFilePersistenceProviderBuilder<TValue>
    : BasePersistenceProviderBuilder<TValue, JsonFilePersistenceOptions, JsonFilePersistenceProviderBuilder<TValue>>
{
    private string? _filePath;
    private JsonSerializerOptions? _serializerOptions;

    public JsonFilePersistenceProviderBuilder<TValue> WithFilePath(string path)
    {
        _filePath = path;
        return this;
    }

    public JsonFilePersistenceProviderBuilder<TValue> WithSerializerOptions(JsonSerializerOptions opts)
    {
        _serializerOptions = opts;
        return this;
    }

    protected override void ValidateSpecificOptions()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            throw new InvalidOperationException("FilePath must be set for JsonFilePersistenceProvider.");
        }

        try
        {
            string fullPath = Path.GetFullPath(_filePath!);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FilePath is not a valid path for JsonFilePersistenceProvider.", ex);
        }
    }

    protected override IPersistenceProvider CreateNonGenericProvider(JsonFilePersistenceOptions options)
    {
        return new JsonFilePersistenceProvider(options);
    }

    protected override void PopulateSpecificOptions(JsonFilePersistenceOptions options)
    {
        options.FilePath = _filePath!;
        options.SerializerOptions = _serializerOptions;
    }
}