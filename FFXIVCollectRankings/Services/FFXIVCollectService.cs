using Dalamud.Plugin.Services;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FFXIVCollectRankings
{
    public class FFXIVCollectService
    {
        private const string ApiBaseUrl = "https://ffxivcollect.com/api/characters/";

        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly ConcurrentDictionary<string, (DateTime timestamp, CharacterData data)> cache;
        private readonly TimeSpan cacheDuration;
        private readonly IPluginLog pluginLog;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public FFXIVCollectService(TimeSpan cacheDuration, IPluginLog pluginLog)
        {
            this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
            this.cacheDuration = cacheDuration;
            cache = new ConcurrentDictionary<string, (DateTime, CharacterData)>();
        }

        public async Task<CharacterData?> GetCharacterDataAsync(string lodestoneId)
        {
            if (string.IsNullOrWhiteSpace(lodestoneId))
            {
                pluginLog.Warning("Lodestone ID cannot be null or empty.");
                return null;
            }

            if (TryGetCachedData(lodestoneId, out var cachedData))
            {
                return cachedData;
            }

            return await FetchAndCacheCharacterDataAsync(lodestoneId);
        }

        private bool TryGetCachedData(string lodestoneId, out CharacterData? cachedData)
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

        private async Task<CharacterData?> FetchAndCacheCharacterDataAsync(string lodestoneId)
        {
            try
            {
                var jsonResponse = await FetchDataFromApiAsync(lodestoneId);
                if (jsonResponse == null) return null;

                var characterData = DeserializeCharacterData(jsonResponse);
                if (characterData != null)
                {
                    cache.AddOrUpdate(lodestoneId, (DateTime.Now, characterData), (key, oldValue) => (DateTime.Now, characterData));
                }

                return characterData;
            }
            catch (Exception e)
            {
                pluginLog.Error($"Unexpected error for Lodestone ID {lodestoneId}: {e.Message}");
                return null;
            }
        }

        private async Task<string?> FetchDataFromApiAsync(string lodestoneId)
        {
            try
            {
                var response = await HttpClient.GetAsync($"{ApiBaseUrl}{lodestoneId}");
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    pluginLog.Debug($"No FFXIVCollect Character data found Lodestone ID: {lodestoneId}"); // This is okay. The character may not exist on FFXIVCollect/private.
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                pluginLog.Debug($"Raw JSON response for Lodestone ID {lodestoneId}: {jsonResponse}");
                return jsonResponse;
            }
            catch (HttpRequestException e)
            {
                pluginLog.Warning($"Network error fetching data for Lodestone ID {lodestoneId}: {e.Message}");
                return null;
            }
        }

        private CharacterData? DeserializeCharacterData(string jsonResponse)
        {
            try
            {
                return JsonSerializer.Deserialize<CharacterData>(jsonResponse, JsonOptions);
            }
            catch (JsonException e)
            {
                pluginLog.Error($"Error parsing JSON data: {e.Message}");
                return null;
            }
        }
    }

    /**
     * Eh, I tried my best hehe
     */
    public class CharacterData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Server { get; set; }
        public string DataCenter { get; set; }
        public string Portrait { get; set; }
        public string Avatar { get; set; }
        public DateTime LastParsed { get; set; }
        public bool Verified { get; set; }

        public AchievementData Achievements { get; set; }
        public MountData Mounts { get; set; }
        public MinionData Minions { get; set; }
        public RankingData Rankings { get; set; }

        public OrchestrionData Orchestrions { get; set; }
        public SpellData Spells { get; set; }
        public EmoteData Emotes { get; set; }
        public BardingData Bardings { get; set; }
        public HairstyleData Hairstyles { get; set; }
        public ArmoireData Armoires { get; set; }
        public FashionData Fashions { get; set; }
        public RecordData Records { get; set; }
        public SurveyRecordData SurveyRecords { get; set; }
        public CardData Cards { get; set; }
        public NpcData Npcs { get; set; }
        public RelicData Relics { get; set; }
        public LeveData Leves { get; set; }

        public class AchievementData
        {
            public int Count { get; set; }
            public int Total { get; set; }
            public int Points { get; set; }
            public int PointsTotal { get; set; }
            public int RankedPoints { get; set; }
            public int RankedPointsTotal { get; set; }
            public DateTime RankedTime { get; set; }
            public bool Public { get; set; }
        }

        public class MountData
        {
            public int Count { get; set; }
            public int Total { get; set; }
            public int RankedCount { get; set; }
            public int RankedTotal { get; set; }
            public bool Public { get; set; }
        }

        public class MinionData
        {
            public int Count { get; set; }
            public int Total { get; set; }
            public int RankedCount { get; set; }
            public int RankedTotal { get; set; }
            public bool Public { get; set; }
        }

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

        public class OrchestrionData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class SpellData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class EmoteData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class BardingData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class HairstyleData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class ArmoireData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class FashionData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class RecordData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class SurveyRecordData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class CardData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class NpcData
        {
            public int Count { get; set; }
            public int Total { get; set; }
        }

        public class RelicData
        {
            public WeaponData Weapons { get; set; }
            public UltimateData Ultimate { get; set; }
            public ArmorData Armor { get; set; }
            public ToolData Tools { get; set; }

            public class WeaponData
            {
                public int Count { get; set; }
                public int Total { get; set; }
            }

            public class UltimateData
            {
                public int Count { get; set; }
                public int Total { get; set; }
            }

            public class ArmorData
            {
                public int Count { get; set; }
                public int Total { get; set; }
            }

            public class ToolData
            {
                public int Count { get; set; }
                public int Total { get; set; }
            }
        }

        public class LeveData
        {
            public LeveCategory Battlecraft { get; set; }
            public LeveCategory Tradecraft { get; set; }
            public LeveCategory Fieldcraft { get; set; }

            public class LeveCategory
            {
                public int Count { get; set; }
                public int Total { get; set; }
            }
        }
    }
}
