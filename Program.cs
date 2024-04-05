using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public class ExchangeRateList
{
    public Dictionary<string, decimal> Rates { get; set; } = new Dictionary<string, decimal>();
    public DateTime Timestamp { get; set; }
}

public interface IStorage
{
    Task<ExchangeRateList> GetValueAsync();
    Task SetValueAsync(ExchangeRateList value);
    bool CanWrite { get; }
    TimeSpan ExpirationInterval { get; }
}

public class MemoryStorage : IStorage
{
    private ExchangeRateList _cache;
    private DateTime _lastUpdated;
    public TimeSpan ExpirationInterval { get; }

    public MemoryStorage(TimeSpan expirationInterval)
    {
        ExpirationInterval = expirationInterval;
    }

    public bool CanWrite => true;

    public Task<ExchangeRateList> GetValueAsync()
    {
        if (_cache != null && (DateTime.UtcNow - _lastUpdated) <= ExpirationInterval)
        {
            return Task.FromResult(_cache);
        }
        return Task.FromResult<ExchangeRateList>(null);
    }

    public Task SetValueAsync(ExchangeRateList value)
    {
        _cache = value;
        _lastUpdated = DateTime.UtcNow;
        return Task.CompletedTask;
    }
}

public class FileSystemStorage : IStorage
{
    private readonly string _filePath;
    public TimeSpan ExpirationInterval { get; }

    public FileSystemStorage(string filePath, TimeSpan expirationInterval)
    {
        _filePath = filePath;
        ExpirationInterval = expirationInterval;
    }

    public bool CanWrite => true;

    public async Task<ExchangeRateList> GetValueAsync()
    {
        if (File.Exists(_filePath))
        {
            var fileInfo = new FileInfo(_filePath);
            if ((DateTime.UtcNow - fileInfo.LastWriteTimeUtc) <= ExpirationInterval)
            {
                var content = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<ExchangeRateList>(content);
            }
        }
        return null;
    }

    public async Task SetValueAsync(ExchangeRateList value)
    {
        var content = JsonSerializer.Serialize(value);
        await File.WriteAllTextAsync(_filePath, content);
    }
}

public class WebServiceStorage : IStorage
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;

    public TimeSpan ExpirationInterval => TimeSpan.MaxValue; // Not applicable for read-only storage
    public bool CanWrite => false;

    public WebServiceStorage(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<ExchangeRateList> GetValueAsync()
    {
        var url = $"https://openexchangerates.org/api/latest.json?app_id={_apiKey}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<OpenExchangeRatesResponse>(content, options);
            if (data != null)
            {
                return new ExchangeRateList
                {
                    Rates = data.Rates,
                    Timestamp = DateTime.UnixEpoch.AddSeconds(data.Timestamp)
                };
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"An error occurred connecting to Open Exchange Rates API: {ex.Message}");
        }
        return null;
    }

    public Task SetValueAsync(ExchangeRateList value)
    {
        throw new NotImplementedException("This storage is read-only.");
    }

    private class OpenExchangeRatesResponse
    {
        public int Timestamp { get; set; }
        public Dictionary<string, decimal> Rates { get; set; }
    }
}
public class ChainResource<T> where T : class
{
    private readonly List<IStorage> _storages;

    public ChainResource(IEnumerable<IStorage> storages)
    {
        _storages = new List<IStorage>(storages);
    }

    public async Task<T> GetValueAsync()
    {
        T result = null;
        foreach (var storage in _storages)
        {
            var value = await storage.GetValueAsync();
            if (value != null)
            {
                // If found, propagate the value up the chain to all writable storages before it
                var index = _storages.IndexOf(storage);
                for (int i = 0; i < index; i++)
                {
                    if (_storages[i].CanWrite)
                    {
                        await _storages[i].SetValueAsync(value);
                    }
                }
                result = value as T;
                break;
            }
        }
        return result;
    }
}
class Program
{
    static async Task Main(string[] args)
    {
        var webServiceStorage = new WebServiceStorage("9b20961d611645749fd22169408be7c2");
        var chainResource = new ChainResource<ExchangeRateList>(new IStorage[] {
            new MemoryStorage(TimeSpan.FromHours(1)),
            new FileSystemStorage("exchangeRates.json", TimeSpan.FromHours(4)),
            webServiceStorage 
        });

        var exchangeRates = await chainResource.GetValueAsync();
        foreach (var rate in exchangeRates.Rates)
        {
            Console.WriteLine($"{rate.Key}: {rate.Value}");
        }
    }
}