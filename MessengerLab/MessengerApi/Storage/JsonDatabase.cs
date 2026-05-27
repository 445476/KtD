using System.Text.Json;
using MessengerApi.Models;

namespace MessengerApi.Storage;

public class JsonDatabase
{
    private readonly string _dbPath = "database.json";
    private readonly object _lock = new();

    public DatabaseSchema ReadDb()
    {
        lock (_lock)
        {
            if (!File.Exists(_dbPath))
            {
                var defaultDb = new DatabaseSchema();
                WriteDb(defaultDb);
                return defaultDb;
            }

            var json = File.ReadAllText(_dbPath);
            return JsonSerializer.Deserialize<DatabaseSchema>(json) ?? new DatabaseSchema();
        }
    }

    public void WriteDb(DatabaseSchema schema)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dbPath, json);
        }
    }
}