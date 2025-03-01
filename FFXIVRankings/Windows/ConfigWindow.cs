using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace FFXIVRankings.Windows;

public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow() : base("FFXIVRankings Configuration###ConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(240, 230);
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
        ImGui.Text("Rank Metric:");
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
        
        ImGui.Text("Ranking Type:");
        if (ImGui.BeginCombo("##RankingType", Shared.Config.SelectedRankingType.ToString()))
        {
            foreach (Configuration.RankingType rankingType in Enum.GetValues(typeof(Configuration.RankingType)))
            {
                bool isSelected = Shared.Config.SelectedRankingType == rankingType;
                if (ImGui.Selectable(rankingType.ToString(), isSelected))
                {
                    Shared.Config.SelectedRankingType = rankingType;
                    Shared.Config.Save();
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
        }

        // Dropdown for selecting the API source
        ImGui.Text("API:");
        if (ImGui.BeginCombo("##APISelection", Shared.Config.SelectedAPI.ToString()))
        {
            foreach (var api in Enum.GetValues(typeof(Configuration.APISelection)))
            {
                bool isSelected = Shared.Config.SelectedAPI == (Configuration.APISelection)api;
                if (ImGui.Selectable(api.ToString(), isSelected))
                {
                    Shared.Config.SelectedAPI = (Configuration.APISelection)api;
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
