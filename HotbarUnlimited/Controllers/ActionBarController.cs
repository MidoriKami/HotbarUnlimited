using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;

namespace HotbarUnlimited.Controllers;

public unsafe class ActionBarController : IDisposable {
    public static readonly string[] ActionBars = {
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
        "_ActionBarEx",
    };
    
    private Configuration Config => HotbarUnlimitedSystem.Config;

    private readonly Dictionary<string, Vector2> originalAddonSize = new();
    private readonly Dictionary<string, Vector2> originalAddonPosition = new();
    private readonly Dictionary<(string, int), Vector2> originalSlotPositions = new();

    public ActionBarController() {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, ActionBars, OnActionBarDraw);
    }

    private void OnActionBarDraw(AddonEvent type, AddonArgs args) {
        var windowSize = ImGui.GetMainViewport().Size;
        var addon = (AtkUnitBase*) args.Addon;

        var containingNode = addon->GetNodeById(2);
        if (containingNode->X is 0 && containingNode->Y is 0) {
            originalAddonSize.TryAdd(args.AddonName, new Vector2(addon->RootNode->Width, addon->RootNode->Height));
            originalAddonPosition.TryAdd(args.AddonName, new Vector2(addon->X, addon->Y));
            containingNode->SetPositionFloat((addon->X + containingNode->X) / addon->Scale, (addon->Y + containingNode->Y) / addon->Scale);
            
            UpdateSlotPositions(args, addon);
        }
        
        if (Config.DataChanged.Contains(args.AddonName)) {
            UpdateSlotPositions(args, addon);
            Config.DataChanged.Remove(args.AddonName);
        }
            
        addon->SetPosition(0, 0);
        addon->SetSize((ushort)(windowSize.X / addon->Scale), (ushort)(windowSize.Y / addon->Scale));
    }

    private void UpdateSlotPositions(AddonArgs args, AtkUnitBase* addon) {
        var hotbar = (AddonActionBarBase*) addon;
        foreach (var index in Enumerable.Range(0, hotbar->Slot.Length)) {
            var slot = hotbar->Slot.GetPointer(index);
            var slotContainer = slot->ComponentDragDrop->AtkComponentBase.OwnerNode->AtkResNode;

            originalSlotPositions.TryAdd((args.AddonName, index), new Vector2(slotContainer.ParentNode->X, slotContainer.ParentNode->Y));
            if (Config.SlotPositions.TryGetValue(args.AddonName, out var indexDictionary) && indexDictionary.TryGetValue(index, out var position)) {
                slotContainer.ParentNode->SetPositionFloat(position.X, position.Y);
            }
            else { // Value not found, so lets save what it currently is
                Config.SlotPositions.TryAdd(args.AddonName, new Dictionary<int, Vector2>());
                Config.SlotPositions[args.AddonName].TryAdd(index, new Vector2(slotContainer.ParentNode->X, slotContainer.ParentNode->Y));
            }
        }
    }

    public void ResetAddons() {
        foreach(var addonName in originalAddonPosition.Keys) {
            var addon = (AtkUnitBase*) Service.GameGui.GetAddonByName(addonName);
            if (addon is null) continue;

            var containingNode = addon->GetNodeById(2);
            containingNode->SetPositionFloat(0.0f, 0.0f);
            
            addon->SetPosition((short)originalAddonPosition[addonName].X, (short)originalAddonPosition[addonName].Y);
            addon->SetSize((ushort)originalAddonSize[addonName].X, (ushort)originalAddonSize[addonName].Y);
            
            var hotbar = (AddonActionBarBase*) addon;
            foreach (var index in Enumerable.Range(0, hotbar->Slot.Length)) {
                var slot = hotbar->Slot.GetPointer(index);
                ref var slotContainer = ref slot->ComponentDragDrop->AtkComponentBase.OwnerNode->AtkResNode.ParentNode;

                if (originalSlotPositions.TryGetValue((addonName, index), out var position)) {
                    slotContainer->SetPositionFloat(position.X, position.Y);
                }
            }
        }
        
        originalAddonPosition.Clear();
        originalAddonSize.Clear();
        originalSlotPositions.Clear();
    }
    
    public void Dispose() {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, ActionBars, OnActionBarDraw);

        ResetAddons();
    }
}