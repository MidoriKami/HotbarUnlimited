using System;
using Dalamud.Plugin.Services;
using KamiLib.FileIO;

namespace HotbarUnlimited.Controllers;

public class HotbarUnlimitedSystem : IDisposable {
    public static Configuration Config = null!;

    private readonly ActionBarController actionBarController;
    private uint playerClassJob = uint.MaxValue;
    
    public HotbarUnlimitedSystem() {
        Config = new Configuration();
        actionBarController = new ActionBarController();

        Service.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework) {
        if (Service.ClientState is not { LocalPlayer.ClassJob.GameData: { } classJob }) return;
        
        if (playerClassJob != classJob.RowId) {
            Config = CharacterFileController.LoadFile<Configuration>($"{classJob.NameEnglish}.config.json", new Configuration());
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

    public void Dispose() {
        Service.Framework.Update -= OnFrameworkUpdate;

        actionBarController.Dispose();
    }
}