using System;
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

namespace FFXIVCollectRankings;

public sealed class Plugin : IDalamudPlugin
{
    public Configuration Configuration { get; init; }
    public string Name => "FFXIV Collect Rankings";
    private const string CommandName = "/fcr";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static INamePlateGui NamePlateGui { get; private set; } = null!;

    [PluginService]
    public static IObjectTable Objects { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    private static IChatGui Chat { get; set; } = null!;

    private readonly WindowSystem windowSystem = new("FFXIVCollectRankings");
    private readonly ConfigWindow configWindow;

    // Services
    private readonly FFXIVCollectService ffxivCollectService;
    private readonly LodestoneIdFinder lodestoneIdFinder;

    // Lookup for player ranks
    private readonly Dictionary<string, string> playerRanks = new();

    // Should show
    private bool isRankDisplayEnabled = true;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize Windows
        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        // Command
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the FFXIV Collect Rankings Config."
        });

        // Services initialization
        ffxivCollectService = new FFXIVCollectService(TimeSpan.FromMinutes(10), PluginLog);
        lodestoneIdFinder = new LodestoneIdFinder(PluginLog);

        // UI Hooks
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        NamePlateGui.OnNamePlateUpdate += NamePlateGui_OnNamePlateUpdate;

        PluginLog.Information("Plugin loaded and nameplate update hooked.");
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        NamePlateGui.OnNamePlateUpdate -= NamePlateGui_OnNamePlateUpdate;

        PluginLog.Information("Plugin disposed and nameplate hook removed.");
    }

    private void OnCommand(string command, string args)
    {
        // Toggle the rank display state
        isRankDisplayEnabled = !isRankDisplayEnabled;
        var message = $"Rank display is now {(isRankDisplayEnabled ? "enabled" : "disabled")}";
        PluginLog.Information(message);
        Chat.Print(message);
    }

    private void NamePlateGui_OnNamePlateUpdate(
        INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!isRankDisplayEnabled)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;

            var playerCharacter = handler.PlayerCharacter;
            if (playerCharacter == null || !playerCharacter.IsValid()) continue;

            var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) continue;

            string playerKey = $"{playerName}@{worldName}";

            // Check if we already have the rank in our lookup
            if (playerRanks.TryGetValue(playerKey, out var rankText))
            {
                // If found, update the nameplate text directly
                UpdateNamePlateText(handler, rankText);
            }
            else
            {
                // Fetch rank and update the lookup
                FetchAndDisplayRankAsync(handler, playerKey, playerCharacter);
            }
        }
    }

    private (string? playerName, string? worldName) GetPlayerKeyDetails(IPlayerCharacter playerCharacter)
    {
        return (playerCharacter.Name.TextValue, playerCharacter.HomeWorld?.GameData?.Name);
    }

    private async void FetchAndDisplayRankAsync(
        INamePlateUpdateHandler handler, string playerKey, IPlayerCharacter playerCharacter)
    {
        var (playerName, worldName) = GetPlayerKeyDetails(playerCharacter);
        string? lodestoneId = await lodestoneIdFinder.GetLodestoneIdAsync(playerName, worldName);

        if (string.IsNullOrEmpty(lodestoneId))
        {
            PluginLog.Warning(
                $"No Lodestone ID found for {playerName} on {worldName}. Marking as processed and returning debug information.");
            PluginLog.Debug(
                $"{playerCharacter.Name.TextValue}@{playerCharacter.HomeWorld?.GameData?.Name} - {playerCharacter.Address}");
            playerRanks[playerKey] = "Not Found";
            return;
        }

        var characterData = await ffxivCollectService.GetCharacterDataAsync(lodestoneId);
        string rankText = characterData?.Rankings?.Achievements?.Global != null
                              ? $"Rank: {characterData.Rankings.Achievements.Global}"
                              : "Private"; // Return the rank text

        // Update the lookup and the nameplate text
        playerRanks[playerKey] = rankText;
        UpdateNamePlateText(handler, rankText);
    }

    private void UpdateNamePlateText(INamePlateUpdateHandler handler, string text)
    {
        handler.NameParts.Text = text;
        handler.NameParts.TextWrap = CreateTextWrap(new Vector4(1.0f, 0.8f, 0.3f, 1.0f)); // Adjust color as desired
        PluginLog.Debug($"Updated nameplate text to: {text}");
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

    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => configWindow.Toggle();
}
