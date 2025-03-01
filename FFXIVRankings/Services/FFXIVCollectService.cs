using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FFXIVRankings.Services;

public class FFXIVCollectService(TimeSpan cacheDuration)
{
    private const string ApiBaseUrl = "https://ffxivcollect.com/api";

    private readonly HttpClient httpClient = new();
    private readonly ConcurrentDictionary<string, (DateTime timestamp, FFXIVCollectCharacterData data)> cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<FFXIVCollectCharacterData?> GetCharacterDataAsync(string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId))
        {
            Shared.Log.Warning("Lodestone ID cannot be null or empty.");
            return null;
        }

        if (TryGetCachedData(lodestoneId, out var cachedData))
        {
            return cachedData;
        }

        return await FetchAndCacheCharacterDataAsync(lodestoneId);
    }

    private bool TryGetCachedData(string lodestoneId, out FFXIVCollectCharacterData? cachedData)
    {
        if (cache.TryGetValue(lodestoneId, out var cachedEntry) &&
            DateTime.Now - cachedEntry.timestamp < cacheDuration)
        {
            cachedData = cachedEntry.data;
            return true;
        }

        cachedData = null;
        return false;
    }

    private async Task<FFXIVCollectCharacterData?> FetchAndCacheCharacterDataAsync(string lodestoneId)
    {
        try
        {
            var jsonResponse = await FetchDataFromApiAsync(lodestoneId);
            if (jsonResponse == null) return null;

            var characterData = DeserializeCharacterData(jsonResponse);
            if (characterData != null)
            {
                cache.AddOrUpdate(lodestoneId, (DateTime.Now, characterData),
                                  (_, _) => (DateTime.Now, characterData));
            }

            return characterData;
        }
        catch (Exception e)
        {
            Shared.Log.Error($"Unexpected error for Lodestone ID {lodestoneId}: {e.Message}");
            return null;
        }
    }

    private async Task<string?> FetchDataFromApiAsync(string lodestoneId)
    {
        try
        {
            var response = await httpClient.GetAsync($"{ApiBaseUrl}/characters/{lodestoneId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Shared.Log.Warning($"No data found for Lodestone ID: {lodestoneId}");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var jsonResponse = await response.Content.ReadAsStringAsync();

            return jsonResponse;
        }
        catch (HttpRequestException e)
        {
            Shared.Log.Warning($"Network error for Lodestone ID {lodestoneId}: {e.Message}");
            return null;
        }
    }

    private FFXIVCollectCharacterData? DeserializeCharacterData(string jsonResponse)
    {
        try
        {
            return JsonSerializer.Deserialize<FFXIVCollectCharacterData>(jsonResponse, JsonOptions);
        }
        catch (JsonException e)
        {
            Shared.Log.Error($"Error parsing JSON data: {e.Message}");
            return null;
        }
    }
}

public class FFXIVCollectCharacterData
{
    public RankingData Rankings { get; set; }
    
    public class RankingData
    {
        public RankingDetail Achievements { get; set; }
        public RankingDetail Mounts { get; set; }
        public RankingDetail Minions { get; set; }

        public class RankingDetail
        {
            [JsonPropertyName("server")]
            public int? Server { get; set; }

            [JsonPropertyName("data_center")]
            public int? DataCenter { get; set; }

            [JsonPropertyName("global")]
            public int? Global { get; set; }
        }
    }
}
