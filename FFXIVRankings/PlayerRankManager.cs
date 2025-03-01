using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVRankings.Services;

namespace FFXIVRankings;

public class PlayerRankManager(
    string separatorChar,
    Dictionary<string, Vector4> rankColors,
    Dictionary<int, Vector4> rankThresholdColors)
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<RankMetric, string>> playerRanksText =
        new();

    public enum RankMetric
    {
        Achievements,
        Mounts,
        Minions
    }

    public enum RankStatus
    {
        Found,
        NotFound,
        Private,
    }

    public void RefreshCache()
    {
        playerRanksText.Clear();
        
        // TODO: Force redraw? Things will only update when re-drawn.
    }

    public async void ProcessNamePlates(IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        try
        {
            foreach (var handler in handlers)
            {
                try
                {
                    // Skip non-player nameplates
                    if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;

                    // Skip invalid player characters
                    var playerCharacter = handler.PlayerCharacter;
                    if (playerCharacter == null || !playerCharacter.IsValid()) continue;

                    // Skip NPCs
                    var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
                    if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) continue;

                    var playerKey = $"{playerName}@{worldName}";
                    if (playerRanksText.TryGetValue(playerKey, out var rankDict) &&
                        rankDict.TryGetValue(Shared.Config.SelectedRankMetric, out var rankText))
                    {
                        UpdateNamePlateText(handler, rankText, GetRankColorFromRankText(rankText));
                    }
                    else
                    {
                        await FetchAndDisplayRankAsync(handler, playerKey, playerCharacter);
                    }
                }
                catch (Exception ex)
                {
                    Shared.Log.Error($"Error processing nameplate for handler: {ex}");
                }
            }
        }
        catch (Exception e)
        {
            Shared.Log.Error($"Error processing nameplates: {e}");
        }
    }


    private async Task FetchAndDisplayRankAsync(
        INamePlateUpdateHandler handler, string playerKey, IPlayerCharacter playerCharacter)
    {
        var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName))
        {
            Shared.Log.Error($"Failed to resolve player key details for {playerCharacter.Name?.TextValue}.");
            return;
        }

        string? lodestoneId;
        try
        {
            lodestoneId = await Shared.LodestoneIdFinder.GetLodestoneIdAsync(playerName, worldName);
        }
        catch (Exception ex)
        {
            Shared.Log.Error($"Failed to retrieve Lodestone ID for {playerName}@{worldName}: {ex}");
            return;
        }

        // Not Found
        if (string.IsNullOrEmpty(lodestoneId))
        {
            Shared.Log.Warning($"Marking {playerName}@{worldName} as NotFound.");
            UpdatePlayerRank(playerKey, RankStatus.NotFound.ToString());
            return;
        }

        // Determine the API to use
        FFXIVCollectCharacterData? ffxivCollectData = null;
        LalachievementsCharacterData? lalachievementsData = null;

        try
        {
            switch (Shared.Config.SelectedAPI)
            {
                case Configuration.APISelection.FFXIVCollect:
                    ffxivCollectData = await Shared.FFXIVCollectService.GetCharacterDataAsync(lodestoneId);
                    break;

                case Configuration.APISelection.Lalachievements:
                    lalachievementsData = await Shared.LalachievementsService.GetCharacterDataAsync(lodestoneId);
                    break;
            }
        }
        catch (Exception ex)
        {
            Shared.Log.Error($"Failed to fetch character data for Lodestone ID {lodestoneId}: {ex}");
            return;
        }

        // Private/Unavailable data handling
        if ((Shared.Config.SelectedAPI == Configuration.APISelection.FFXIVCollect &&
             ffxivCollectData?.Rankings == null) ||
            (Shared.Config.SelectedAPI == Configuration.APISelection.Lalachievements &&
             lalachievementsData?.GlobalAchievementRank == null))
        {
            UpdatePlayerRank(playerKey, RankStatus.Private.ToString());
            UpdateNamePlateText(handler, RankStatus.Private.ToString(),
                                rankColors.GetValueOrDefault(RankStatus.Private.ToString(),
                                                             new Vector4(1.0f, 0.0f, 0.0f, 1.0f))); // Red
            return;
        }

        string rankText;
        int? rankValue = Shared.Config.SelectedAPI switch
        {
            Configuration.APISelection.FFXIVCollect => Shared.Config.SelectedRankMetric switch
            {
                RankMetric.Achievements => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                               ? ffxivCollectData?.Rankings?.Achievements?.Global
                                               : ffxivCollectData?.Rankings?.Achievements?.Server,

                RankMetric.Mounts => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                         ? ffxivCollectData?.Rankings?.Mounts?.Global
                                         : ffxivCollectData?.Rankings?.Mounts?.Server,

                RankMetric.Minions => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                          ? ffxivCollectData?.Rankings?.Minions?.Global
                                          : ffxivCollectData?.Rankings?.Minions?.Server,

                _ => null
            },

            Configuration.APISelection.Lalachievements => Shared.Config.SelectedRankMetric switch
            {
                RankMetric.Achievements => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                               ? lalachievementsData?.GlobalAchievementRank
                                               : lalachievementsData?.AchievementRank,

                RankMetric.Mounts => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                         ? lalachievementsData?.GlobalMountRank
                                         : lalachievementsData?.MountRank,

                RankMetric.Minions => Shared.Config.SelectedRankingType == Configuration.RankingType.Global
                                          ? lalachievementsData?.GlobalMinionRank
                                          : lalachievementsData?.MinionRank,

                _ => null
            },

            _ => null
        };

        // Found or Unknown
        if (rankValue.HasValue)
        {
            rankText = $"{separatorChar}{rankValue.Value}";
            UpdatePlayerRank(playerKey, rankText);
            UpdateNamePlateText(handler, rankText, GetRankColor(rankValue.Value));
        }
        else
        {
            rankText = RankStatus.Private.ToString();
            UpdatePlayerRank(playerKey, rankText);
            UpdateNamePlateText(handler, rankText,
                                rankColors.GetValueOrDefault(RankStatus.Private.ToString(), Vector4.Zero));
        }
    }

    private void UpdatePlayerRank(string playerKey, string rankText)
    {
        var rankDict = playerRanksText.GetOrAdd(playerKey, _ => new ConcurrentDictionary<RankMetric, string>());
        rankDict[Shared.Config.SelectedRankMetric] = rankText;
    }

    private (Dalamud.Game.Text.SeStringHandling.SeString, Dalamud.Game.Text.SeStringHandling.SeString) CreateTextWrap(
        Vector4 color)
    {
        var left = new Lumina.Text.SeStringBuilder();
        var right = new Lumina.Text.SeStringBuilder();

        left.PushColorRgba(color);
        right.PopColor();

        return ((Dalamud.Game.Text.SeStringHandling.SeString)left.ToSeString(),
                   (Dalamud.Game.Text.SeStringHandling.SeString)right.ToSeString());
    }

    private void UpdateNamePlateText(INamePlateUpdateHandler handler, string text, Vector4 color)
    {
        string rankText = $"{text}";
        handler.FreeCompanyTagParts.Text = rankText;

        if (Shared.Config.UsePercentileColours)
        {
            handler.FreeCompanyTagParts.TextWrap = CreateTextWrap(color);
        }
    }

    private Vector4 GetRankColorFromRankText(string rankText)
    {
        return rankText switch
        {
            var r when r == RankStatus.Private.ToString() => rankColors.GetValueOrDefault(
                RankStatus.Private.ToString(), Vector4.Zero),
            var r when r == RankStatus.NotFound.ToString() => rankColors.GetValueOrDefault(
                RankStatus.NotFound.ToString(), Vector4.Zero),
            _ when rankText.StartsWith(separatorChar) &&
                   int.TryParse(rankText.TrimStart(separatorChar.ToCharArray()), out int rankValue) =>
                GetRankColor(rankValue),
            _ => rankColors.GetValueOrDefault(RankStatus.Found.ToString(), Vector4.Zero)
        };
    }

    private Vector4 GetRankColor(int rankValue)
    {
        foreach (var threshold in rankThresholdColors)
        {
            if (rankValue > threshold.Key)
            {
                return threshold.Value;
            }
        }

        return rankColors.GetValueOrDefault(RankStatus.Found.ToString(), Vector4.Zero);
    }

    private (string? playerName, string? worldName) GetPlayerKeyDetails(IPlayerCharacter playerCharacter)
    {
        var playerName = playerCharacter.Name?.TextValue;
        var world = playerCharacter.HomeWorld.Value;

        // Is world null?
        if (world.Equals(null))
        {
            Shared.Log.Warning("World is null for player character.");
            return (null, null);
        }

        var worldName = world.Name.ExtractText();

        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName))
        {
            Shared.Log.Warning("Player name or world name is null or empty.");
            return (null, null);
        }

        return (playerName, worldName);
    }
}
