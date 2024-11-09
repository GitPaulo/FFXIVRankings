using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVCollectRankings.Windows;
using NetStone;

namespace FFXIVCollectRankings;

/**
 * TODO:
 * - Found/NotFound/Private states need to be handled better. If a network error happens NotFound is set, but it should be retried.
 * - Rate limiting measures should be implemented sometime
 * - More configuration options (global datacenter world rankings, etc.)
 */

public sealed class Plugin : IDalamudPlugin
{
    public Configuration Configuration { get; init; }
    public string Name => "FFXIV Collect Rankings";
    private const string CommandFCR = "/fcr";
    private bool isRankDisplayEnabled = true;

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static INamePlateGui NamePlateGui { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    private static IChatGui Chat { get; set; } = null!;

    private readonly string rankSeparatorChar = "♯";
    private readonly WindowSystem windowSystem = new("FFXIVCollectRankings");
    private readonly ConfigWindow configWindow;
    private readonly LodestoneClient lodestoneClient;
    private readonly FFXIVCollectService ffxivCollectService;
    private readonly LodestoneIdFinder lodestoneIdFinder;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<RankMetric, string>> playerRanksText = new();
    private readonly Dictionary<string, Vector4> rankColors = new()
    {
        { RankStatus.Found.ToString(), new Vector4(1.0f, 1.0f, 1.0f, 1.0f) },      // White
        { RankStatus.Private.ToString(), new Vector4(1.0f, 0.0f, 0.0f, 1.0f) },    // Red
        { RankStatus.NotFound.ToString(), new Vector4(1.0f, 0.647f, 0.0f, 1.0f) }, // Orange
    };
    private readonly Dictionary<int, Vector4> rankThresholdColors = new()
    {
        { 1000000, new Vector4(0.9f, 0.9f, 0.9f, 1.0f) }, // > 1 million = White
        { 500000, new Vector4(0.7f, 0.7f, 0.7f, 1.0f) },  // > 500,000 = Grey
        { 250000, new Vector4(0.9f, 0.9f, 0.9f, 1.0f) },  // > 250,000 = White
        { 100000, new Vector4(1.0f, 0.85f, 0.9f, 1.0f) }, // > 100,000 = Pink
        { 10000, new Vector4(0.3f, 0.3f, 1.0f, 1.0f) },   // > 10,000 = Blue
        { 1000, new Vector4(0.7f, 0.3f, 0.7f, 1.0f) },    // > 1,000 = Purple
        { 100, new Vector4(0.0f, 0.0f, 0.0f, 1.0f) },     // 100 = Black (dark grey)
    };

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

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandFCR, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle rank display on nameplates."
        });

        ffxivCollectService = new FFXIVCollectService(TimeSpan.FromMinutes(10), PluginLog);
        lodestoneClient = LodestoneClient.GetClientAsync().GetAwaiter().GetResult();
        lodestoneIdFinder = new LodestoneIdFinder(PluginLog, lodestoneClient);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        NamePlateGui.OnNamePlateUpdate += NamePlateGui_OnNamePlateUpdate;
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();

        CommandManager.RemoveHandler(CommandFCR);
        NamePlateGui.OnNamePlateUpdate -= NamePlateGui_OnNamePlateUpdate;

        PluginLog.Information("Plugin disposed and nameplate hook removed.");
    }

    private void OnCommand(string command, string args)
    {
        isRankDisplayEnabled = !isRankDisplayEnabled;
        var message = $"Rank display is now {(isRankDisplayEnabled ? "enabled" : "disabled")}";
        PluginLog.Information(message);
        Chat.Print(message);
    }

    private void NamePlateGui_OnNamePlateUpdate(
        INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!isRankDisplayEnabled) return;

        foreach (var handler in handlers)
        {
            if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;

            var playerCharacter = handler.PlayerCharacter;
            if (playerCharacter == null || !playerCharacter.IsValid()) continue;

            var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) continue;

            string playerKey = $"{playerName}@{worldName}";

            // Check if the rank and color are already in our lookup
            if (playerRanksText.TryGetValue(playerKey, out var rankDict) &&
                rankDict.TryGetValue(Configuration.SelectedRankMetric, out var rankText))
            {
                UpdateNamePlateText(handler, rankText, GetRankColorFromRankText(rankText));
            }
            else
            {
                FetchAndDisplayRankAsync(handler, playerKey, playerCharacter);
            }
        }
    }

    private async void FetchAndDisplayRankAsync(
        INamePlateUpdateHandler handler, string playerKey, IPlayerCharacter playerCharacter)
    {
        var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
        string? lodestoneId = await lodestoneIdFinder.GetLodestoneIdAsync(playerName, worldName);

        if (string.IsNullOrEmpty(lodestoneId)) // This is problematic
        {
            PluginLog.Debug($"No Lodestone ID found for {playerName} on {worldName}. Marking as processed.");
            UpdatePlayerRank(playerKey, RankStatus.NotFound.ToString());
            return;
        }

        var characterData = await ffxivCollectService.GetCharacterDataAsync(lodestoneId);
        if (characterData?.Rankings == null) // Likely not found/private
        {
            UpdatePlayerRank(playerKey, RankStatus.Private.ToString());
            UpdateNamePlateText(handler, RankStatus.Private.ToString(), rankColors[RankStatus.Private.ToString()]);
            return;
        }

        string rankText;
        int? rankValue = Configuration.SelectedRankMetric switch
        {
            RankMetric.Achievements => characterData?.Rankings?.Achievements?.Global,
            RankMetric.Mounts => characterData?.Rankings?.Mounts?.Global,
            RankMetric.Minions => characterData?.Rankings?.Minions?.Global,
            _ => null
        };

        if (rankValue.HasValue)
        {
            rankText = $"{rankSeparatorChar}{rankValue.Value}"; // Use the separator character
            UpdatePlayerRank(playerKey, rankText);              // Update lookup
            UpdateNamePlateText(handler, rankText, GetRankColor(rankValue.Value));
        }
        else
        {
            rankText = RankStatus.Private.ToString();
            UpdatePlayerRank(playerKey, rankText);
            UpdateNamePlateText(handler, rankText, rankColors[RankStatus.Private.ToString()]);
        }
    }

    private void UpdatePlayerRank(string playerKey, string rankText)
    {
        // Update the rank for the selected metric in a thread-safe way
        var rankDict = playerRanksText.GetOrAdd(playerKey, _ => new ConcurrentDictionary<RankMetric, string>());
        rankDict[Configuration.SelectedRankMetric] = rankText;
    }


    private void UpdateNamePlateText(INamePlateUpdateHandler handler, string text, Vector4 color)
    {
        handler.NameParts.Text = $" {text}"; // Add a little margin-left
        if (Configuration.UsePercentileColours)
        {
            handler.NameParts.TextWrap = CreateTextWrap(color);
        }
    }

    private Vector4 GetRankColorFromRankText(string rankText)
    {
        // Determine color based on rank text
        return rankText switch
        {
            var r when r == RankStatus.Private.ToString() => rankColors[RankStatus.Private.ToString()],
            var r when r == RankStatus.NotFound.ToString() => rankColors[RankStatus.NotFound.ToString()],
            _ when rankText.StartsWith(rankSeparatorChar) &&
                   int.TryParse(rankText.TrimStart(rankSeparatorChar.ToCharArray()), out int rankValue) =>
                GetRankColor(rankValue),                 // Ensure it starts with the separator
            _ => rankColors[RankStatus.Found.ToString()] // Default to found color
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

        return rankColors[RankStatus.Found.ToString()]; // Default to found color
    }

    private (string? playerName, string? worldName) GetPlayerKeyDetails(IPlayerCharacter playerCharacter)
    {
        return (playerCharacter.Name.TextValue, playerCharacter.HomeWorld?.GameData?.Name!);
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

    public void RefreshCache()
    {
        playerRanksText.Clear();
        PluginLog.Debug("Player ranks cache cleared.");
    }

    // UI handlers
    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => configWindow.Toggle();
}
