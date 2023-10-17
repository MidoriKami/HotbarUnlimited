using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HotbarUnlimited.Controllers;
using ImGuiNET;
using KamiLib.Command;
using KamiLib.Interfaces;
using KamiLib.System;
using KamiLib.UserInterface;

namespace HotbarUnlimited.Views.Config;

public class ConfigurationWindow : TabbedSelectionWindow {
    private readonly List<ISelectionWindowTab> tabs;
    private readonly List<ITabItem> regularTabs;
    
    public ConfigurationWindow() : base("HotbarUnlimited Configuration", 0.0f, 150.0f) {

        tabs = new List<ISelectionWindowTab> { new HotbarConfigurationTab() };
        regularTabs = new List<ITabItem> { new GeneralSettingsTab() };

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500.0f, 450.0f),
            MaximumSize = new Vector2(9999.0f, 9999.0f),
        };

        ShowScrollBar = false;
        RegularTabScrollBar = false;
        
        CommandController.RegisterCommands(this);
    }

    public override bool DrawConditions() => Service.ClientState is { IsLoggedIn: true };

    protected override IEnumerable<ISelectionWindowTab> GetTabs() => tabs;
    protected override IEnumerable<ITabItem> GetRegularTabs() => regularTabs;
    
    [BaseCommandHandler("OpenConfigWindow")]
    public void OpenConfigWindow() {
        if (!Service.ClientState.IsLoggedIn) return;

        Toggle();
    }
}

public class HotbarConfigurationTab : ISelectionWindowTab {
    public string TabName => "Hotbars";
    public ISelectable? LastSelection { get; set; }

    private readonly IEnumerable<ISelectable> selectables;

    public HotbarConfigurationTab() {
        selectables = ActionBarController.ActionBars.Select(name => new HotbarSelectable(name));
    }

    public IEnumerable<ISelectable> GetTabSelectables() => selectables;
}

public class GeneralSettingsTab : ITabItem {
    private Configuration Config => HotbarUnlimitedSystem.Config;
    public string TabName => "General Settings";
    public bool Enabled => true;
    public void Draw() {
        ImGui.Text("Edit Mode allows you to click-drag your hotbar slots to wherever you want");

        if (ImGui.BeginTable("HotkeyTable", 2)) {
            ImGui.TableSetupColumn("##Hotkey", ImGuiTableColumnFlags.WidthFixed, 150.0f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("##Tooltip", ImGuiTableColumnFlags.WidthStretch);

            ImGui.TableNextColumn();
            ImGui.Text("[Hold Shift]");
            
            ImGui.TableNextColumn();
            ImGui.Text("snap to coordinate multiple of 5");
            
            ImGui.TableNextColumn();
            ImGui.Text("[Hold Shift + Control]");
            
            ImGui.TableNextColumn();
            ImGui.Text("snap to coordinate multiples of 15");
            
            ImGui.EndTable();
        }
        ImGui.Separator();
        
        ImGui.Checkbox("Enable Edit Mode", ref Config.EditModeEnabled);
        
        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.Columns(2);
        var collectionHalfSize = Config.EditEnabledHotbars.Count / 2 + 1;
        foreach (var (hotbar, enabled) in Config.EditEnabledHotbars.Take(collectionHalfSize)) {
            var isEnabled = enabled;
            if (ImGui.Checkbox(hotbar, ref isEnabled)) {
                Config.EditEnabledHotbars[hotbar] = isEnabled;
            }
        }
        ImGui.NextColumn();
        foreach (var (hotbar, enabled) in Config.EditEnabledHotbars.Skip(collectionHalfSize)) {
            var isEnabled = enabled;
            if (ImGui.Checkbox(hotbar, ref isEnabled)) {
                Config.EditEnabledHotbars[hotbar] = isEnabled;
            }
        }
        ImGui.Columns(1);
        
        var hotkeyHeld = ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl;
        if (!hotkeyHeld) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);
        ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - ImGui.GetFrameHeight());
        if (ImGui.Button("Reset Current Job", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeight())) && hotkeyHeld) {
            Config.ResetJob = true;
        }
        if (!hotkeyHeld) ImGui.PopStyleVar();
        if (ImGui.IsItemHovered() && !hotkeyHeld) {
            ImGui.SetTooltip("Hold Shift + Control while clicking to reset configuration for current job\nWarning: This can not be undone");
        }
    }
}

public unsafe class HotbarSelectable : ISelectable, IDrawable {
    private Configuration Config => HotbarUnlimitedSystem.Config;
    
    public IDrawable Contents => this;
    public string ID => HotbarName;

    public string HotbarName;

    public HotbarSelectable(string hotbarName) {
        HotbarName = hotbarName;
    }

    public void DrawLabel() {
        ImGui.Text(HotbarName);
    }
    
    public void Draw() {
        if (Config.SlotPositions.TryGetValue(HotbarName, out var indexDictionary)) {
            var addon = (AtkUnitBase*) Service.GameGui.GetAddonByName(HotbarName);
            var containingNode = addon->GetNodeById(2);
            var containingNodePosition = new Vector2(containingNode->X, containingNode->Y);
            var configChanged = false;
            
            if (ImGui.BeginTable("HotbarPositionTable", 2)) {
                ImGui.TableSetupColumn("##IndexColumn", ImGuiTableColumnFlags.WidthFixed, 75.0f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("##PositionColumn", ImGuiTableColumnFlags.WidthStretch);
                    
                ImGui.TableNextColumn();
                ImGui.Text($"Container");
                
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.DragFloat2($"##Container{HotbarName}", ref containingNodePosition)) {
                    containingNode->SetPositionFloat(containingNodePosition.X, containingNodePosition.Y);
                }
                
                foreach (var (index, position) in indexDictionary) {
                    ImGui.TableNextColumn();
                    ImGui.Text($"{index + 1}");

                    ImGui.TableNextColumn();
                    var positionValue = position;
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.DragFloat2($"##{HotbarName}{index}", ref positionValue)) {
                        Config.SlotPositions[HotbarName][index] = positionValue;
                        Config.DataChanged.Add(HotbarName);
                        configChanged = true;
                    }
                }
                ImGui.EndTable();
            }

            if (configChanged) Config.Save();
        }
        else {
            const string warningText = "Hotbar not visible, or no data found.";
            var textSize = ImGui.CalcTextSize(warningText);
            
            ImGui.SetCursorPos(ImGui.GetContentRegionMax() / 2.0f - textSize / 2.0f);
            ImGui.TextColored(KnownColor.Orange.Vector(), warningText);
        }
    }
}