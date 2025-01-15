using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FFXIVCollectRankings.Windows;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base("FFXIV Collect Rankings Configuratio###ConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(300, 150);
        SizeCondition = ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (Shared.Config.IsConfigWindowMovable)
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
        bool usePercentileColours = Shared.Config.UsePercentileColours; // Read the current value
        if (ImGui.Checkbox("Use Percentile Colors", ref usePercentileColours))
        {
            Shared.Config.UsePercentileColours = usePercentileColours; // Update the configuration
            Shared.Config.Save(); // Save the updated configuration
        }

        // Dropdown for Rank Metric selection
        ImGui.Text("Select Rank Metric:");
        if (ImGui.BeginCombo("##RankMetric", Shared.Config.SelectedRankMetric.ToString()))
        {
            foreach (PlayerRankManager.RankMetric metric in Enum.GetValues(typeof(PlayerRankManager.RankMetric)))
            {
                bool isSelected = Shared.Config.SelectedRankMetric == metric;
                if (ImGui.Selectable(metric.ToString(), isSelected))
                {
                    Shared.Config.SelectedRankMetric = metric;
                    Shared.Config.Save();
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
            Shared.PlayerRankManager.RefreshCache(); // Call the refresh method in the plugin
        }
    }
}
