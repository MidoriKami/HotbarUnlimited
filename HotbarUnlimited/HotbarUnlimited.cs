using Dalamud.Plugin;
using HotbarUnlimited.Controllers;
using HotbarUnlimited.Views.Config;
using HotbarUnlimited.Views.Overlay;
using KamiLib;
using KamiLib.System;

namespace HotbarUnlimited;

public sealed class HotbarUnlimitedPlugin : IDalamudPlugin {
    public static HotbarUnlimitedSystem System = null!;
    
    public HotbarUnlimitedPlugin(DalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();

        KamiCommon.Initialize(pluginInterface, "HotbarUnlimited");
        
        KamiCommon.WindowManager.AddConfigurationWindow(new ConfigurationWindow(), true);
        KamiCommon.WindowManager.AddWindow(new OverlayWindow());
        
        CommandController.RegisterMainCommand("/hotbarunlimited", "/uhotbar");
        
        System = new HotbarUnlimitedSystem();
    }

    public void Dispose() {
        System.Dispose();
    }
}