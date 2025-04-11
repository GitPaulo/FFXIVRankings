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
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<RankMetric, string>> playerRanksText = new();
    private readonly ConcurrentDictionary<string, bool> loadingState = new();

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
        loadingState.Clear();
    }

    public async void ProcessNamePlates(IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            try
            {
                if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;
                var playerCharacter = handler.PlayerCharacter;
                if (playerCharacter == null || !playerCharacter.IsValid()) continue;

                var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
                if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) continue;

                var playerKey = $"{playerName}@{worldName}";

                if (playerRanksText.TryGetValue(playerKey, out var rankDict) &&
                    rankDict.TryGetValue(Shared.Config.SelectedRankMetric, out var rankText))
                {
                    UpdateNamePlateText(handler, rankText, GetRankColorFromRankText(rankText));
                }
                else if (loadingState.TryGetValue(playerKey, out var isLoading) && isLoading)
                {
                    UpdateNamePlateText(handler, "Loading...", new Vector4(0.6f, 0.6f, 1.0f, 1.0f));
                }
                else
                {
                    loadingState[playerKey] = true;
                    UpdateNamePlateText(handler, "Loading...", new Vector4(0.6f, 0.6f, 1.0f, 1.0f));
                    _ = FetchAndDisplayRankAsync(handler, playerKey, playerCharacter);
                }
            }
            catch (Exception ex)
            {
                Shared.Log.Error($"Error processing nameplate: {ex}");
            }
        }
    }

    private async Task FetchAndDisplayRankAsync(
        INamePlateUpdateHandler handler, string playerKey, IPlayerCharacter playerCharacter)
    {
        var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
        if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) return;

        try
        {
            var lodestoneId = await Shared.LodestoneIdFinder.GetLodestoneIdAsync(playerName, worldName);
            if (string.IsNullOrEmpty(lodestoneId))
            {
                UpdatePlayerRank(playerKey, RankStatus.NotFound.ToString());
                UpdateNamePlateText(handler, RankStatus.NotFound.ToString(),
                                    GetRankColorFromRankText(RankStatus.NotFound.ToString()));
                return;
            }

            FFXIVCollectCharacterData? ffxivCollectData = null;
            LalachievementsCharacterData? lalachievementsData = null;

            switch (Shared.Config.SelectedAPI)
            {
                case Configuration.APISelection.FFXIVCollect:
                    ffxivCollectData = await Shared.FFXIVCollectService.GetCharacterDataAsync(lodestoneId);
                    break;
                case Configuration.APISelection.Lalachievements:
                    lalachievementsData = await Shared.LalachievementsService.GetCharacterDataAsync(lodestoneId);
                    break;
            }

            bool privateData = (Shared.Config.SelectedAPI == Configuration.APISelection.FFXIVCollect &&
                                ffxivCollectData?.Rankings == null)
                               || (Shared.Config.SelectedAPI == Configuration.APISelection.Lalachievements &&
                                   lalachievementsData?.GlobalAchievementRank == null);

            if (privateData)
            {
                UpdatePlayerRank(playerKey, RankStatus.Private.ToString());
                UpdateNamePlateText(handler, RankStatus.Private.ToString(),
                                    GetRankColorFromRankText(RankStatus.Private.ToString()));
                return;
            }

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

            if (rankValue.HasValue)
            {
                var rankText = $"{separatorChar}{rankValue.Value}";
                UpdatePlayerRank(playerKey, rankText);
                UpdateNamePlateText(handler, rankText, GetRankColor(rankValue.Value));
            }
            else
            {
                UpdatePlayerRank(playerKey, RankStatus.Private.ToString());
                UpdateNamePlateText(handler, RankStatus.Private.ToString(),
                                    GetRankColorFromRankText(RankStatus.Private.ToString()));
            }
        }
        catch (Exception ex)
        {
            Shared.Log.Error($"Failed to fetch rank for {playerKey}: {ex}");
        } finally
        {
            loadingState.TryRemove(playerKey, out _);
        }
    }

    private void UpdatePlayerRank(string playerKey, string rankText)
    {
        var rankDict = playerRanksText.GetOrAdd(playerKey, _ => new ConcurrentDictionary<RankMetric, string>());
        rankDict[Shared.Config.SelectedRankMetric] = rankText;
    }

    private void UpdateNamePlateText(INamePlateUpdateHandler handler, string text, Vector4 color)
    {
        handler.FreeCompanyTagParts.Text = text;
        if (Shared.Config.UsePercentileColours)
        {
            handler.FreeCompanyTagParts.TextWrap = CreateTextWrap(color);
        }
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

    private Vector4 GetRankColorFromRankText(string rankText)
    {
        return rankText switch
        {
            var r when r == RankStatus.Private.ToString() => rankColors.GetValueOrDefault(
                RankStatus.Private.ToString(), Vector4.Zero),
            var r when r == RankStatus.NotFound.ToString() => rankColors.GetValueOrDefault(
                RankStatus.NotFound.ToString(), Vector4.Zero),
            _ when rankText.StartsWith(separatorChar) &&
                   int.TryParse(rankText.TrimStart(separatorChar.ToCharArray()), out var rankValue) => GetRankColor(
                rankValue),
            _ => rankColors.GetValueOrDefault(RankStatus.Found.ToString(), Vector4.Zero)
        };
    }

    private Vector4 GetRankColor(int rankValue)
    {
        foreach (var threshold in rankThresholdColors)
        {
            if (rankValue > threshold.Key)
                return threshold.Value;
        }

        return rankColors.GetValueOrDefault(RankStatus.Found.ToString(), Vector4.Zero);
    }

    private (string? playerName, string? worldName) GetPlayerKeyDetails(IPlayerCharacter playerCharacter)
    {
        var playerName = playerCharacter.Name?.TextValue;
        var world = playerCharacter.HomeWorld.Value;
        if (world.Equals(null)) return (null, null);
        var worldName = world.Name.ExtractText();
        return string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)
                   ? (null, null)
                   : (playerName, worldName);
    }

    public void RefreshNameplate(INamePlateUpdateHandler handler)
    {
        var playerCharacter = handler.PlayerCharacter;
        if (playerCharacter == null || !playerCharacter.IsValid()) return;
        var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
        var playerKey = $"{playerName}@{worldName}";
        if (playerRanksText.TryGetValue(playerKey, out var rankDict) &&
            rankDict.TryGetValue(Shared.Config.SelectedRankMetric, out var rankText))
        {
            UpdateNamePlateText(handler, rankText, GetRankColorFromRankText(rankText));
        }
    }
}
