using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using HotbarUnlimited.Controllers;
using ImGuiNET;
using KamiLib.Utility;

namespace HotbarUnlimited.Views.Overlay;

public unsafe class OverlayWindow : Window {
    private Configuration Config => HotbarUnlimitedSystem.Config;
    
    public OverlayWindow() : base("HotbarUnlimited Overlay") {
        Flags |= ImGuiWindowFlags.NoDecoration |
                 ImGuiWindowFlags.NoBackground |
                 ImGuiWindowFlags.NoTitleBar |
                 ImGuiWindowFlags.NoInputs |
                 ImGuiWindowFlags.NoNav |
                 ImGuiWindowFlags.NoFocusOnAppearing |
                 ImGuiWindowFlags.NoBringToFrontOnFocus;
    }

    public override void PreOpenCheck() {
        IsOpen = Service.ClientState.IsLoggedIn && Config.EditModeEnabled;
    }

    public override bool DrawConditions() 
        => Service.ClientState is not ({ IsLoggedIn: false } or { IsPvP: true });

    public override void Draw() {
        foreach (var addonName in ActionBarController.ActionBars) {
            var addon = (AddonActionBarBase*) Service.GameGui.GetAddonByName(addonName);
            if (addon is null) continue;
            if (!((AtkUnitBase*) addon)->IsVisible) continue;

            var hotbarContainer = addon->AtkUnitBase.GetNodeById(2);
            var hotbarPosition = new Vector2(hotbarContainer->X, hotbarContainer->Y) * addon->AtkUnitBase.Scale;

            var savePending = false;
            
            foreach (var index in Enumerable.Range(0, addon->Slot.Length)) {
                var slot = addon->Slot.GetPointer(index);
                ref var containingNode = ref slot->ComponentDragDrop->AtkComponentBase.OwnerNode->AtkResNode;

                var containingNodePosition = new Vector2(containingNode.X, containingNode.Y) * addon->AtkUnitBase.Scale * containingNode.ParentNode->ScaleX;
                var slotNodePosition = new Vector2(containingNode.ParentNode->X, containingNode.ParentNode->Y) * addon->AtkUnitBase.Scale;
                var slotNodeSize = new Vector2(containingNode.Width, containingNode.Height) * addon->AtkUnitBase.Scale * containingNode.ParentNode->ScaleX;

                var color = new ColorHelpers.HsvaColor(index * 0.3f, 0.80f, 1.0f, 1.0f);

                if (!Config.EditEnabledHotbars[addonName]) continue;
                
                ImGui.SetNextWindowPos(hotbarPosition + containingNodePosition + slotNodePosition, ImGuiCond.Appearing);
                ImGui.SetNextWindowSize(slotNodeSize, ImGuiCond.Always);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                if (ImGui.Begin($"##{addonName}{index}", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBackground)) {
                    if (ImGui.GetIO().KeyShift && !ImGui.GetIO().KeyCtrl && ImGui.IsWindowFocused()) {
                        ImGui.SetWindowPos(GetGridWindowPosition(5));
                    } else if (ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl && ImGui.IsWindowFocused()) {
                        ImGui.SetWindowPos(GetGridWindowPosition(15));
                    }

                    ImGui.GetWindowDrawList().AddRect(
                        ImGui.GetWindowPos(),
                        ImGui.GetWindowPos() + slotNodeSize,
                        ImGui.GetColorU32(ColorHelpers.HsvToRgb(color)),
                        5.0f,
                        ImDrawFlags.None,
                        3.0f);
                    
                    var position = ImGui.GetWindowPos() - hotbarPosition - containingNodePosition;
                    
                    Config.SlotPositions.TryAdd(addonName, new Dictionary<int, Vector2>());
                    Config.SlotPositions[addonName].TryAdd(index, position);

                    if (Bound.IsCursorInWindow()) {
                        var scaleChanged = false;
                        if (ImGui.GetIO().MouseWheel > 0) {
                            Config.SlotScales[addonName][index] += 0.10f;
                            scaleChanged = true;
                        }
                        else if (ImGui.GetIO().MouseWheel < 0) {
                            Config.SlotScales[addonName][index] -= 0.10f;
                            scaleChanged = true;
                        }

                        if (scaleChanged) {
                            var scale = Config.SlotScales[addonName][index];
                            containingNode.ParentNode->SetScale(scale, scale);
                        }
                    }
                    
                    if (ImGui.IsWindowFocused()) {
                        if (Config.SlotPositions[addonName][index] != position / addon->AtkUnitBase.Scale) {
                            Config.SlotPositions[addonName][index] = position / addon->AtkUnitBase.Scale;
                            savePending = true;
                        }

                        var xPosition = MathF.Ceiling(position.X / addon->AtkUnitBase.Scale);
                        var yPosition = MathF.Ceiling(position.Y / addon->AtkUnitBase.Scale);
                        
                        containingNode.ParentNode->SetPositionFloat(xPosition, yPosition);
                    }
                    else {
                        ImGui.SetWindowPos(hotbarPosition + containingNodePosition + slotNodePosition) ;
                    }
                }
                ImGui.End();
                ImGui.PopStyleVar();
            }
            
            if (savePending) Config.Save();
        }
    }

    private Vector2 GetGridWindowPosition(int gridSize) {
        var windowX = MathF.Round(ImGui.GetWindowPos().X / gridSize) * gridSize;
        var windowY = MathF.Round(ImGui.GetWindowPos().Y / gridSize) * gridSize;

        return new Vector2(windowX, windowY);
    }
}