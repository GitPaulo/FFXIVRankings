using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using NetStone;
using NetStone.Model.Parseables.Search.Character;
using NetStone.Search.Character;

namespace FFXIVCollectRankings
{
    public class LodestoneIdFinder
    {
        private readonly LodestoneClient lodestoneClient;
        private readonly IPluginLog pluginLog;

        // Dictionary to track ongoing requests for specific character-server pairs
        private static readonly ConcurrentDictionary<string, Task<string?>> ActiveRequests = new();

        public LodestoneIdFinder(IPluginLog pluginLog, LodestoneClient lodestoneClient)
        {
            this.pluginLog = pluginLog ?? throw new ArgumentNullException(nameof(pluginLog));
            this.lodestoneClient = lodestoneClient ?? throw new ArgumentNullException(nameof(lodestoneClient));
        }

        public static async Task<LodestoneIdFinder> CreateAsync(IPluginLog pluginLog)
        {
            var client = await LodestoneClient.GetClientAsync();
            return new LodestoneIdFinder(pluginLog, client);
        }

        public async Task<string?> GetLodestoneIdAsync(string characterName, string serverName)
        {

            // Use a unique cache key for each character-server pair
            string cacheKey = $"{characterName}@{serverName}";

            // Ensure only one request per character-server pair is active at a time
            var requestTask = ActiveRequests.GetOrAdd(cacheKey, _ => FetchLodestoneIdAsync(characterName, serverName));

            try
            {
                return await requestTask;
            } finally
            {
                // Remove the task from the active requests once it completes
                ActiveRequests.TryRemove(cacheKey, out _);
            }
        }

        private async Task<string?> FetchLodestoneIdAsync(string characterName, string serverName)
        {
            try
            {
                var searchResults = await SearchCharacterOnLodestone(characterName, serverName);

                if (searchResults == null || !searchResults.Results.Any())
                {
                    pluginLog.Warning($"No Lodestone character search results found for {characterName} on {serverName}.");
                    return null;
                }

                var lodestoneId = searchResults.Results.FirstOrDefault()?.Id;
                if (lodestoneId == null)
                {
                    pluginLog.Warning($"No Lodestone ID found for {characterName} on {serverName}.");
                }

                return lodestoneId;
            }
            catch (HttpRequestException httpEx)
            {
                pluginLog.Error(
                    $"Network error while fetching Lodestone ID for {characterName} on {serverName}: {httpEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                pluginLog.Error(
                    $"Unexpected error while fetching Lodestone ID for {characterName} on {serverName}: {ex.Message}");
                return null;
            }
        }

        private async Task<CharacterSearchPage?> SearchCharacterOnLodestone(string characterName, string serverName)
        {
            var searchQuery = new CharacterSearchQuery
            {
                World = serverName,
                CharacterName = characterName
            };

            pluginLog.Debug($"Starting search for character '{characterName}' on server '{serverName}'.");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await lodestoneClient.SearchCharacter(searchQuery);
                stopwatch.Stop();

                pluginLog.Debug(
                    $"Search completed in {stopwatch.ElapsedMilliseconds}ms for {characterName} on {serverName}.");
                return result;
            }
            catch (HttpRequestException httpEx)
            {
                pluginLog.Error($"Network error during character search: {httpEx.Message}");
                return null;
            }
        }
    }
}
