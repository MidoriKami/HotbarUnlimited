using System;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc.UserFileManager;
using KamiLib.FileIO;

namespace HotbarUnlimited.Controllers;

// Temporary Struct until ClientStructs updates
[StructLayout(LayoutKind.Explicit, Size = 0x68)]
public unsafe struct AddonConfig {
    public static AddonConfig* Instance() => (AddonConfig*)Framework.Instance()->GetUiModule()->GetAddonConfig();
    
    [FieldOffset(0x00)] public UserFileEvent UserFileEvent;
    [FieldOffset(0x50)] public AddonConfigData* ModuleData;
}

// Temporary Struct until ClientStructs updates
[StructLayout(LayoutKind.Explicit, Size = 0x9E90)]
public struct AddonConfigData {
    [FieldOffset(0x00)] public Utf8String DefaultString; // Literally says "Default"
    // [FieldOffset(0x68)] public StdList<[SomeStruct Size 48]> SomeList; //Contains 300 elements
    // [FieldOffset(0x78)] public StdList<[SomeStruct size 16]> SomeList; //Contains 400 elements
    // [FixedSizeArray<[SomeStruct Size 36]>(400)] public byte SomeArray[400 * 36]; //Contains 400 elements
    // There's a LOT more data here
    
    [FieldOffset(0x9E88)] public int CurrentHudLayout;
}

public unsafe class HotbarUnlimitedSystem : IDisposable {
    public static Configuration Config = null!;

    private readonly ActionBarController actionBarController;
    private uint playerClassJob = uint.MaxValue;

    private delegate nint ChangeHudLayoutDelegate(AddonConfig* addonConfig, uint layoutIndex, bool unk1 = false, bool unk2 = true);

    [Signature("E8 ?? ?? ?? ?? 33 C0 EB 15", DetourName = nameof(OnHudLayoutChanged))]
    private readonly Hook<ChangeHudLayoutDelegate>? hudLayoutChangedHook = null; 
    
    public HotbarUnlimitedSystem() {
        Config = new Configuration();
        actionBarController = new ActionBarController();
        
        Service.Hooker.InitializeFromAttributes(this);
        hudLayoutChangedHook?.Enable();

        Service.Framework.Update += OnFrameworkUpdate;
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
        Service.Framework.Update -= OnFrameworkUpdate;

        hudLayoutChangedHook?.Dispose();
        actionBarController.Dispose();
    }
}