using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVCollectRankings.Windows;
using NetStone;

namespace FFXIVCollectRankings;

internal class Shared
{
    public static Configuration Config { get; set; } = null!;
    public static ConfigWindow ConfigWindow { get; set; } = null!;
    public static FFXIVCollectService FFXIVCollectService { get; set; } = null!;
    public static LodestoneClient LodestoneClient { get; set; } = null!;
    public static LodestoneIdFinder LodestoneIdFinder { get; set; } = null!;
    public static PlayerRankManager PlayerRankManager { get; set; } = null!;
    
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlateGui { get; private set; } = null!; 
}
