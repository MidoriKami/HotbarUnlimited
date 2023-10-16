﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using KamiLib.FileIO;

namespace HotbarUnlimited;

public class Configuration : IPluginConfiguration {
    public int Version { get; set; } = 1;

    public Dictionary<string, Dictionary<int, Vector2>> SlotPositions = new();

    [NonSerialized] public bool EditModeEnabled = false;
    [NonSerialized] public HashSet<string> DataChanged = new();
    [NonSerialized] public bool ResetJob = false;
    
    public void Save() {
        if (Service.ClientState is not { LocalPlayer.ClassJob.GameData: { } classJob }) return;

        CharacterFileController.SaveFile($"{classJob.NameEnglish}.config.json", typeof(Configuration), this);
    }
}