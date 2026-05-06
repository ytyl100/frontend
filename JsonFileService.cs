using CarPartsInventory.API.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarPartsInventory.API.Services
{
    public class JsonFileService<T> : IJsonFileService<T>
    {
        private readonly string _filePath;
        private readonly ILogger<JsonFileService<T>> _logger;
        private readonly JsonSerializerOptions _options;

        private List<T>? _cache;
        private DateTime _cacheTimestamp;

        public JsonFileService(IOptions<JsonStorageOptions> storageOptions, ILogger<JsonFileService<T>> logger)
        {
            // Prefer DataPath (configurable) but fall back to BasePath for compatibility.
            var configuredPath = storageOptions.Value.DataPath ?? storageOptions.Value.BasePath ?? @"API\Data";
            var basePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
            _filePath = Path.Combine(basePath, $"{typeof(T).Name.ToLowerInvariant()}s.json");
            _logger = logger;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            _logger.LogInformation("🔎 JsonFileService for {Type} will use file: {Path}", typeof(T).Name, _filePath);
        }
        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<T>();

                var lastWrite = File.GetLastWriteTimeUtc(_filePath);
                if (_cache != null && lastWrite <= _cacheTimestamp)
                    return _cache;

                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<T>();

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                List<T>? result = null;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    result = root.Deserialize<List<T>>(_options);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            result = prop.Value.Deserialize<List<T>>(_options);
                            break;
                        }
                    }
                }

                _cache = result ?? new List<T>();
                _cacheTimestamp = lastWrite;

                _logger.LogInformation("📂 Loaded {Count} items from {Path}", _cache.Count, _filePath);
                return _cache;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading data from {Path}", _filePath);
                throw;
            }
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            var items = await GetAllAsync();
            foreach (var item in items)
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp != null && string.Equals(idProp.GetValue(item)?.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return default;
        }

        public async Task<T> CreateAsync(T item)
        {
            var items = new List<T>(await GetAllAsync()) { item };
            await SaveAllAsync(items);
            return item;
        }

        public async Task<T?> UpdateAsync(string id, T item)
        {
            var items = new List<T>(await GetAllAsync());
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null) return default;

            var index = items.FindIndex(x => string.Equals(idProp.GetValue(x)?.ToString(), id, StringComparison.OrdinalIgnoreCase));
            if (index == -1) return default;

            items[index] = item;
            await SaveAllAsync(items);
            return item;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var items = new List<T>(await GetAllAsync());
            var idProp = typeof(T).GetProperty("Id");
            if (idProp == null) return false;

            var removed = items.RemoveAll(x => string.Equals(idProp.GetValue(x)?.ToString(), id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                await SaveAllAsync(items);
            return removed;
        }

        public async Task ReplaceAllAsync(List<T> items)
        {
            await SaveAllAsync(items);
            _logger.LogInformation("✅ Replaced all data in {Path}, total {Count} items", _filePath, items.Count);
        }

        private async Task SaveAllAsync(List<T> items)
        {
            var json = JsonSerializer.Serialize(items, _options);
            await File.WriteAllTextAsync(_filePath, json);
        }
    }
}