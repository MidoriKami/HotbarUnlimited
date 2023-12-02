using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiLib.FileIO;

namespace HotbarUnlimited.Controllers;

public unsafe class HotbarUnlimitedSystem : IDisposable {
    public static Configuration Config = null!;
    public static bool HudLayoutOpen;

    private readonly ActionBarController actionBarController;
    private uint playerClassJob = uint.MaxValue;

    private delegate nint ChangeHudLayoutDelegate(AddonConfig* addonConfig, uint layoutIndex, bool unk1 = false, bool unk2 = true);
    private readonly Hook<ChangeHudLayoutDelegate>? hudLayoutChangedHook; 
    
    public HotbarUnlimitedSystem() {
        Config = new Configuration();
        actionBarController = new ActionBarController();
        
        hudLayoutChangedHook ??= Service.Hooker.HookFromAddress<ChangeHudLayoutDelegate>((nint)AddonConfig.Addresses.ChangeHudLayout.Value, OnHudLayoutChanged);
        hudLayoutChangedHook?.Enable();

        Service.Framework.Update += OnFrameworkUpdate;

        Service.ClientState.EnterPvP += OnEnterPvP;
        
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, new[] { "Tooltip", "ActionDetail", "ItemDetail" }, OnTooltipPreDraw);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "HudLayout", OnHudLayoutOpen);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "HudLayout", OnHudLayoutClose);
    }
    
    private void OnEnterPvP() {
        actionBarController.ResetAddons();
    }
    
    private void OnTooltipPreDraw(AddonEvent type, AddonArgs args) {
        if (Config.EditModeEnabled) {
            var addon = (AtkUnitBase*) args.Addon;
            addon->IsVisible = false;
        }
    }

    private void OnHudLayoutOpen(AddonEvent type, AddonArgs args) {
        HudLayoutOpen = true;
        actionBarController.ResetAddons();
    }

    private void OnHudLayoutClose(AddonEvent type, AddonArgs args) {
        HudLayoutOpen = false;
    }
    
    private void OnFrameworkUpdate(IFramework framework) {
        if (Service.ClientState is not { LocalPlayer.ClassJob.GameData: { } classJob }) return;
        
        if (playerClassJob != classJob.RowId) {
            Config = CharacterFileController.LoadFile<Configuration>($"{classJob.NameEnglish}-{AddonConfig.Instance()->ModuleData->CurrentHudLayout:00}.config.json", new Configuration());
            actionBarController.ResetAddons();
        }

        playerClassJob = classJob.RowId;

        if (Config.ResetJob) {
            Config = new Configuration();
            actionBarController.ResetAddons();
            Config.Save();
            Config.ResetJob = false;
        }
    }

    private nint OnHudLayoutChanged(AddonConfig* addonConfig, uint layoutIndex, bool unk1 = false, bool unk2 = true) {
        try {
            if (Service.ClientState is { LocalPlayer.ClassJob.GameData: { } classJob }) {
                Config.Save();
                Config = CharacterFileController.LoadFile<Configuration>($"{classJob.NameEnglish}-{layoutIndex:00}.config.json", new Configuration());
                actionBarController.ResetAddons();
            }
        }
        catch (Exception e) {
            Service.PluginLog.Error(e, "Error on HudLayout Change.");
        }

        return hudLayoutChangedHook!.Original(addonConfig, layoutIndex, unk1, unk2);
    }

    public void Dispose() {
        Service.AddonLifecycle.UnregisterListener(OnTooltipPreDraw);
        Service.AddonLifecycle.UnregisterListener(OnHudLayoutOpen);
        Service.AddonLifecycle.UnregisterListener(OnHudLayoutClose);

        Service.Framework.Update -= OnFrameworkUpdate;
        
        Service.ClientState.EnterPvP -= OnEnterPvP;

        hudLayoutChangedHook?.Dispose();
        actionBarController.Dispose();
    }
}