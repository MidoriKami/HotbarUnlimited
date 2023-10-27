using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using HotbarUnlimited.Controllers;
using KamiLib.FileIO;

namespace HotbarUnlimited;

public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public Dictionary<string, Dictionary<int, Vector2>> SlotPositions = new();

    [NonSerialized] public bool EditModeEnabled = false;
    [NonSerialized] public HashSet<string> DataChanged = new();
    [NonSerialized] public bool ResetJob = false;
    [NonSerialized] public Dictionary<string, bool> EditEnabledHotbars = ActionBarController.ActionBars.ToDictionary(k => k, v => false);
    
    public unsafe void Save() {
        if (Service.ClientState is not { LocalPlayer.ClassJob.GameData: { } classJob }) return;

        CharacterFileController.SaveFile($"{classJob.NameEnglish}-{AddonConfig.Instance()->ModuleData->CurrentHudLayout:00}.config.json", typeof(Configuration), this);
    }
}