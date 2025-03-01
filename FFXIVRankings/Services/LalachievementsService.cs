using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FFXIVRankings.Services;

public class LalachievementsService
{
    private readonly HttpClient httpClient = new();
    const string APIBaseURL = "https://lalachievements.com/api";

    public async Task<LalachievementsCharacterData?> GetCharacterDataAsync(string lodestoneId)
    {
        if (string.IsNullOrWhiteSpace(lodestoneId))
        {
            Shared.Log.Error("Lodestone ID cannot be null or empty.");
            throw new ArgumentException("Lodestone ID cannot be null or empty", nameof(lodestoneId));
        }

        var url = $"{APIBaseURL}/charrealtime/{lodestoneId}";

        try
        {
            using var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var characterData = JsonSerializer.Deserialize<LalachievementsCharacterData>(responseBody);

            if (characterData == null)
            {
                Shared.Log.Warning($"Failed to deserialize Lalachievements data for Lodestone ID {lodestoneId}");
            }

            return characterData;
        }
        catch (HttpRequestException e)
        {
            Shared.Log.Error($"Error fetching Lalachievements data: {e.Message}");
            return null;
        }
        catch (JsonException e)
        {
            Shared.Log.Error($"Failed to parse Lalachievements API response: {e.Message}");
            return null;
        }
        catch (Exception e)
        {
            Shared.Log.Error($"Unexpected error in GetCharacterDataAsync: {e}");
            return null;
        }
    }
}

public class LalachievementsCharacterData
{
    [JsonPropertyName("achievementRank")]
    public string? AchievementRankString { get; set; }

    [JsonIgnore]
    public int? AchievementRank => TryParseInt(AchievementRankString);

    [JsonPropertyName("globalAchievementRank")]
    public string? GlobalAchievementRankString { get; set; }

    [JsonIgnore]
    public int? GlobalAchievementRank => TryParseInt(GlobalAchievementRankString);

    [JsonPropertyName("mountRank")]
    public string? MountRankString { get; set; }

    [JsonIgnore]
    public int? MountRank => TryParseInt(MountRankString);

    [JsonPropertyName("globalMountRank")]
    public string? GlobalMountRankString { get; set; }

    [JsonIgnore]
    public int? GlobalMountRank => TryParseInt(GlobalMountRankString);

    [JsonPropertyName("minionRank")]
    public string? MinionRankString { get; set; }

    [JsonIgnore]
    public int? MinionRank => TryParseInt(MinionRankString);

    [JsonPropertyName("globalMinionRank")]
    public string? GlobalMinionRankString { get; set; }

    [JsonIgnore]
    public int? GlobalMinionRank => TryParseInt(GlobalMinionRankString);

    private static int? TryParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }
}
