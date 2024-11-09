using Dalamud.Configuration;
using System;

namespace FFXIVCollectRankings;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;

    // Toggles whether to use percentile colors for ranks
    public bool UsePercentileColours { get; set; } = true;

    // Enum to define available rank metrics
    public Plugin.RankMetric SelectedRankMetric { get; set; } = Plugin.RankMetric.Achievements; // Default to Achievements

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
