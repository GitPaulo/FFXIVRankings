using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVCollectRankings.Windows;
using NetStone;

namespace FFXIVCollectRankings;

public sealed class Plugin : IDalamudPlugin
{
    private const string Name = "FFXIV Collect Rankings";
    private const string CommandFCR = "/fcr";

    private readonly WindowSystem windowSystem = new(Name);

    private bool isRankDisplayEnabled = true;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Shared>();

        Shared.Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        InitWindows();
        InitCommands();
        InitServices();
        InitHooks();
        
        Shared.Log.Information($"Loaded {Shared.PluginInterface.Manifest.Name}");
    }

    public void Dispose()
    {
        windowSystem.RemoveAllWindows();
        Shared.ConfigWindow.Dispose();
        Shared.CommandManager.RemoveHandler(CommandFCR);
        Shared.NamePlateGui.OnNamePlateUpdate -= NamePlateGui_OnNamePlateUpdate;
    }

    private void InitWindows()
    {
        Shared.ConfigWindow = new ConfigWindow();
        windowSystem.AddWindow(Shared.ConfigWindow);
    }

    private void InitCommands()
    {
        Shared.CommandManager.AddHandler(CommandFCR, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle rank display on nameplates."
        });
    }

    private void InitServices()
    {
        Shared.LodestoneClient = LodestoneClient.GetClientAsync().GetAwaiter().GetResult();
        Shared.FFXIVCollectService = new FFXIVCollectService(TimeSpan.FromMinutes(10));
        Shared.LodestoneIdFinder = new LodestoneIdFinder();
        Shared.PlayerRankManager = new PlayerRankManager(
            "=",
            // Colors for rank statuses
            new Dictionary<string, Vector4>
            {
                { 
                    PlayerRankManager.RankStatus.Found.ToString(), 
                    new Vector4(0.85f, 0.85f, 0.85f, 1.0f) // Light gray for found ranks (soft and neutral)
                },
                { 
                    PlayerRankManager.RankStatus.Private.ToString(), 
                    new Vector4(1.0f, 0.4f, 0.4f, 1.0f) // Soft red for private (indicates restricted data without being harsh)
                },
                { 
                    PlayerRankManager.RankStatus.NotFound.ToString(), 
                    new Vector4(1.0f, 0.8f, 0.6f, 1.0f) // Soft orange for not found (gentle warning tone)
                }
            },
            // Colors for rank thresholds
            new Dictionary<int, Vector4>
            {
                { 
                    1000000, 
                    new Vector4(0.85f, 0.85f, 0.85f, 1.0f) // Light gray for > 1 million (neutral tone for high ranks)
                },
                { 
                    500000, 
                    new Vector4(0.75f, 0.75f, 0.75f, 1.0f) // Slightly darker gray for > 500k
                },
                { 
                    250000, 
                    new Vector4(0.6f, 1.0f, 0.6f, 1.0f) // Soft green for > 250k
                },
                { 
                    100000, 
                    new Vector4(0.65f, 0.8f, 1.0f, 1.0f) // Light blue for > 100k
                },
                { 
                    50000, 
                    new Vector4(1.0f, 1.0f, 0.6f, 1.0f) // Soft yellow for > 50k
                },
                { 
                    10000, 
                    new Vector4(1.0f, 0.8f, 0.4f, 1.0f) // Light orange for > 10k
                },
                { 
                    1000, 
                    new Vector4(1.0f, 1.0f, 0.0f, 1.0f) // Bright yellow for > 1k
                },
                { 
                    100, 
                    new Vector4(1.0f, 0.84f, 0.0f, 1.0f) // Gold for top 100
                }
            });
    }

    private void InitHooks()
    {
        Shared.PluginInterface.UiBuilder.Draw += DrawUI;
        Shared.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        Shared.NamePlateGui.OnNamePlateUpdate += NamePlateGui_OnNamePlateUpdate;
    }

    private void OnCommand(string command, string args)
    {
        isRankDisplayEnabled = !isRankDisplayEnabled;
        var message = $"Rank display is now {(isRankDisplayEnabled ? "enabled" : "disabled")}";
        Shared.Chat.Print(message);
    }

    private void NamePlateGui_OnNamePlateUpdate(
        INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (isRankDisplayEnabled)
        {
            Shared.PlayerRankManager.ProcessNamePlates(handlers);
        }
    }

    // UI handlers
    private void DrawUI() => windowSystem.Draw();
    public void ToggleConfigUI() => Shared.ConfigWindow.Toggle();
}
