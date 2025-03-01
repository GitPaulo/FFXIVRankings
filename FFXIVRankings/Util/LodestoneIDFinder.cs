using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NetStone.Model.Parseables.Search.Character;
using NetStone.Search.Character;

namespace FFXIVRankings
{
    public class LodestoneIdFinder
    {
        // Dictionary to track ongoing requests for specific character-server pairs
        private static readonly ConcurrentDictionary<string, Task<string?>> ActiveRequests = new();

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
                    Shared.Log.Warning($"No Lodestone character search results found for {characterName} on {serverName}.");
                    return null;
                }

                var lodestoneId = searchResults.Results.FirstOrDefault()?.Id;
                if (lodestoneId == null)
                {
                    Shared.Log.Warning($"No Lodestone ID found for {characterName} on {serverName}.");
                }

                return lodestoneId;
            }
            catch (HttpRequestException httpEx)
            {
                Shared.Log.Error(
                    $"Network error while fetching Lodestone ID for {characterName} on {serverName}: {httpEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Shared.Log.Error(
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

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await Shared.LodestoneClient.SearchCharacter(searchQuery);
                stopwatch.Stop();

                return result;
            }
            catch (HttpRequestException httpEx)
            {
                Shared.Log.Error($"Network error during character search: {httpEx.Message}");
                return null;
            }
        }
    }
}
