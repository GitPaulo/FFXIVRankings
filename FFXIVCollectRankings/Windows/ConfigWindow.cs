using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FFXIVCollectRankings.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private readonly Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("FFXIV Collect Rankings Configuratio###ConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 150);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
        this.plugin = plugin; // Store reference to the plugin for refresh action
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        // Toggle for Percentile Colors
        bool usePercentileColours = Configuration.UsePercentileColours; // Read the current value
        if (ImGui.Checkbox("Use Percentile Colors", ref usePercentileColours))
        {
            Configuration.UsePercentileColours = usePercentileColours; // Update the configuration
            Configuration.Save(); // Save the updated configuration
        }

        // Dropdown for Rank Metric selection
        ImGui.Text("Select Rank Metric:");
        if (ImGui.BeginCombo("##RankMetric", Configuration.SelectedRankMetric.ToString()))
        {
            foreach (Plugin.RankMetric metric in Enum.GetValues(typeof(Plugin.RankMetric)))
            {
                bool isSelected = Configuration.SelectedRankMetric == metric;
                if (ImGui.Selectable(metric.ToString(), isSelected))
                {
                    Configuration.SelectedRankMetric = metric;
                    Configuration.Save();
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        // Button to refresh the cache
        if (ImGui.Button("Refresh Cache"))
        {
            plugin.RefreshCache(); // Call the refresh method in the plugin
        }
    }
}
