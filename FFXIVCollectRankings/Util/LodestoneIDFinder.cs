using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using NetStone;
using NetStone.Search.Character;

namespace FFXIVCollectRankings
{
    public class LodestoneIdFinder
    {
        private readonly LodestoneClient lodestoneClient;
        private readonly IPluginLog pluginLog;

        public LodestoneIdFinder(IPluginLog pluginLog, LodestoneClient? lodestoneClient = null)
        {
            this.pluginLog = pluginLog;
            this.lodestoneClient = lodestoneClient ?? LodestoneClient.GetClientAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Finds the Lodestone ID of a character by name and server.
        /// </summary>
        /// <param name="characterName">The character's name.</param>
        /// <param name="serverName">The character's server.</param>
        /// <returns>The Lodestone ID if found; otherwise, null.</returns>
        public async Task<string?> GetLodestoneIdAsync(string characterName, string serverName)
        {
            if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(serverName))
            {
                pluginLog.Warning("Character name or server name is empty.");
                return null;
            }

            try
            {
                var searchQuery = new CharacterSearchQuery
                {
                    World = serverName,
                    CharacterName = characterName
                };

                var searchResults = await lodestoneClient.SearchCharacter(searchQuery);
                var lodestoneId = searchResults?.Results.FirstOrDefault()?.Id;

                if (lodestoneId == null)
                {
                    pluginLog.Warning($"No Lodestone ID found for {characterName} on {serverName}.");
                }

                return lodestoneId;
            }
            catch (HttpRequestException httpEx)
            {
                pluginLog.Error($"Network error while fetching Lodestone ID for {characterName} on {serverName}: {httpEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                pluginLog.Error($"Unexpected error fetching Lodestone ID for {characterName} on {serverName}: {ex.Message}");
                return null;
            }
        }
    }
}
