using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Cammy;

public class Cammy(IDalamudPluginInterface pluginInterface) : DalamudPlugin<Configuration>(pluginInterface), IDalamudPlugin
{
    protected override void Initialize()
    {
        Game.Initialize();
        IPC.Initialize();
        DalamudApi.ClientState.Login += Login;
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    private const string cammySubcommands = "/cammy [ help | preset | zoom | fov | spectate | nocollide | freecam ]";

    [PluginCommand("/cammy", HelpMessage = "開啟／關閉設定視窗。其他用法：" + cammySubcommands)]
    private unsafe void ToggleConfig(string command, string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            ToggleConfig();
            return;
        }

        var regex = Regex.Match(argument, "^(\\w+) ?(.*)");
        var subcommand = regex.Success && regex.Groups.Count > 1 ? regex.Groups[1].Value : string.Empty;

        switch (subcommand.ToLower())
        {
            case "preset":
                {
                    if (regex.Groups.Count < 2 || string.IsNullOrEmpty(regex.Groups[2].Value))
                    {
                        PresetManager.CurrentPreset = null;
                        DalamudApi.PrintEcho("已取消預設覆寫。");
                        return;
                    }

                    var arg = regex.Groups[2].Value;
                    var preset = Config.Presets.FirstOrDefault(preset => preset.Name == arg);

                    if (preset == null)
                    {
                        DalamudApi.PrintError($"找不到預設「{arg}」。");
                        return;
                    }

                    PresetManager.CurrentPreset = preset;
                    DalamudApi.PrintEcho($"已套用預設「{arg}」。");
                    break;
                }
            case "zoom":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        DalamudApi.PrintError("數值無效。");
                        return;
                    }

                    Common.CameraManager->worldCamera->currentZoom = amount;
                    break;
                }
            case "fov":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        DalamudApi.PrintError("數值無效。");
                        return;
                    }

                    Common.CameraManager->worldCamera->currentFoV = amount;
                    break;
                }
            case "spectate":
                {
                    Game.EnableSpectating ^= true;
                    DalamudApi.PrintEcho($"觀戰功能已{(Game.EnableSpectating ? "啟用" : "停用")}！");
                    break;
                }
            case "nocollide":
                {
                    Config.EnableCameraNoClippy ^= true;
                    if (!FreeCam.Enabled)
                        Game.cameraNoClippyReplacer.Toggle();
                    Config.Save();
                    DalamudApi.PrintEcho($"鏡頭碰撞已{(Config.EnableCameraNoClippy ? "停用" : "啟用")}！");
                    break;
                }
            case "freecam":
                {
                    FreeCam.Toggle();
                    break;
                }
            case "help":
                {
                    DalamudApi.PrintEcho("子命令：" +
                        "\npreset <name>－依名稱套用預設並覆寫自動預設；不指定名稱可停用覆寫。" +
                        "\nzoom <amount>－設定目前鏡頭距離。" +
                        "\nfov <amount>－設定目前視野。" +
                        "\nspectate－切換「觀察焦點目標／軟目標」選項。" +
                        "\nnocollide－切換「停用鏡頭碰撞」選項。" +
                        "\nfreecam－切換「自由鏡頭」選項。");
                    break;
                }
            default:
                {
                    DalamudApi.PrintError("用法錯誤：" + cammySubcommands);
                    break;
                }
        }
    }

    protected override void Update()
    {
        FreeCam.Update();
        PresetManager.Update();
    }

    protected override void Draw() => PluginUI.Draw();

    private static void Login()
    {
        DalamudApi.Framework.Update += UpdateDefaultPreset;
        PresetManager.DisableCameraPresets();
        PresetManager.CheckCameraConditionSets(true);
    }

    private static void UpdateDefaultPreset(IFramework framework)
    {
        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]) return;
        PresetManager.DefaultPreset = new();
        DalamudApi.Framework.Update -= UpdateDefaultPreset;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        IPC.Dispose();
        PresetManager.DefaultPreset.Apply();
        DalamudApi.ClientState.Login -= Login;

        if (FreeCam.Enabled)
            FreeCam.Toggle();

        Game.Dispose();
    }
}
