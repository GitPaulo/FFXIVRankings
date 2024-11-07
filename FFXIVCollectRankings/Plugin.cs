using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVCollectRankings.Windows;

namespace FFXIVCollectRankings;

public sealed class Plugin : IDalamudPlugin
{
    public Configuration Configuration { get; init; }

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ITextureProvider TextureProvider { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    private static INamePlateGui NamePlateGui { get; set; } = null!;

    private const string CommandName = "/fcr";

    // Window
    private readonly WindowSystem windowSystem = new("FFXIVCollectRankings");
    private readonly ConfigWindow configWindow;
    private readonly MainWindow mainWindow;

    // Services
    private readonly FFXIVCollectService ffxivCollectService;
    private readonly LodestoneIdFinder lodestoneIdFinder;

    // Processed players cache
    private readonly HashSet<string> processedPlayers = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // Initialize Windows
        configWindow = new ConfigWindow(this);
        mainWindow = new MainWindow(this, "Resources\\goat.png");
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(mainWindow);

        // Command
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        // Services initialization
        ffxivCollectService = new FFXIVCollectService(TimeSpan.FromMinutes(10), PluginLog);
        lodestoneIdFinder = new LodestoneIdFinder(PluginLog);

        // UI Hooks
        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        NamePlateGui.OnNamePlateUpdate += OnNamePlateUpdate;
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        mainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        NamePlateGui.OnNamePlateUpdate -= OnNamePlateUpdate;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            if (handler.NamePlateKind != NamePlateKind.PlayerCharacter) continue;

            var playerCharacter = handler.PlayerCharacter;
            if (playerCharacter == null || !playerCharacter.IsValid()) continue;

            var (playerName, worldName) = GetPlayerDetails(playerCharacter);
            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(worldName)) continue;

            var playerIdentifier = $"{playerName}@{worldName}";

            if (processedPlayers.Contains(playerIdentifier)) continue;
            processedPlayers.Add(playerIdentifier);

            FetchAndDisplayRankAsync(handler, playerName, worldName);
        }

        processedPlayers.Clear(); // Clear after each update cycle to allow reprocessing
    }

    private (string? playerName, string? worldName) GetPlayerDetails(IPlayerCharacter? playerCharacter)
    {
        return playerCharacter == null
                   ? (null, null)
                   : (playerCharacter.Name.TextValue, playerCharacter.HomeWorld?.GameData?.Name);
    }

    private async void FetchAndDisplayRankAsync(INamePlateUpdateHandler handler, string playerName, string worldName)
    {
        string? lodestoneId = await lodestoneIdFinder.GetLodestoneIdAsync(playerName, worldName);
        if (string.IsNullOrEmpty(lodestoneId))
        {
            PluginLog.Warning($"No Lodestone ID found for {playerName} on {worldName}. Marking as processed.");
            UpdateNamePlateText(handler, "Not Found");
            return;
        }

        var characterData = await ffxivCollectService.GetCharacterDataAsync(lodestoneId);

        if (characterData == null)
        {
            PluginLog.Warning(
                $"Character data not found for {playerName} on {worldName} (Lodestone ID: {lodestoneId}).");
            UpdateNamePlateText(handler, "Not Found");
            return;
        }

        string rankText = characterData.Rankings?.Achievements?.Global != null
                              ? $"Rank: {characterData.Rankings.Achievements.Global}"
                              : "Private";

        UpdateNamePlateText(handler, rankText);

        PluginLog.Information(
            $"Updated rank for {playerName} on {worldName} (Lodestone ID: {lodestoneId}): {rankText}");
        PluginLog.Debug($"FetchAndDisplayRankAsync completed for {playerName} on {worldName}.");
    }

    private void UpdateNamePlateText(INamePlateUpdateHandler handler, string rankText)
    {
        var originalText = handler.NameParts.Text;
        var nameWithRank = $"{rankText}\n{originalText}";

        handler.NameParts.Text = nameWithRank;
        handler.NameParts.TextWrap = CreateTextWrap(new Vector4(1.0f, 0.8f, 0.3f, 1.0f)); // Adjust color as desired
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
    public void ToggleMainUI() => mainWindow.Toggle();
}
