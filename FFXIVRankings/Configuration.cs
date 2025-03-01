using Dalamud.Configuration;
using System;

namespace FFXIVRankings;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;

    public bool UsePercentileColours { get; set; } = true;

    public PlayerRankManager.RankMetric SelectedRankMetric { get; set; } = PlayerRankManager.RankMetric.Achievements;

    // New: Option to select Global or Server rankings
    public RankingType SelectedRankingType { get; set; } = RankingType.Global;
    
    public APISelection SelectedAPI { get; set; } = APISelection.FFXIVCollect;

    public enum APISelection
    {
        FFXIVCollect,
        Lalachievements
    }

    public enum RankingType
    {
        Global,
        Server
    }

    public void Save()
    { 
        Shared.PluginInterface.SavePluginConfig(this);
    }
}
