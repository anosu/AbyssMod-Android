using HarmonyLib;

namespace AbyssMod.Patches;

/// <summary>
/// Harmony 补丁管理器。负责初始化所有子补丁类、提供共享工具方法。
/// </summary>
public static class PatchManager
{
    private static HarmonyLib.Harmony _harmony;

    /// <summary>
    /// 创建并注册所有 Harmony 补丁。
    /// </summary>
    public static void Initialize()
    {
        if (_harmony != null)
            return;

        EnhancePatch.Reset();
        MasterDataPatch.Reset();
        TranslationPatch.Reset();

        var harmony = new HarmonyLib.Harmony(ModInfo.Name);
        try
        {
            harmony.PatchAll(typeof(EnhancePatch));
            if (Config.TranslationEnabledAtStartup)
            {
                harmony.PatchAll(typeof(MasterDataPatch));
                harmony.PatchAll(typeof(UiTranslationPatch));
            }
            harmony.PatchAll(typeof(TranslationPatch));
#if DEBUG
            harmony.PatchAll(typeof(DebugPatch));
#endif
            _harmony = harmony;
        }
        catch
        {
            try
            {
                harmony.UnpatchSelf();
            }
            catch (System.Exception e)
            {
                Logger.Error($"Harmony rollback failed: {e}");
            }
            EnhancePatch.Reset();
            MasterDataPatch.Reset();
            TranslationPatch.Reset();
            throw;
        }
    }

    public static void Shutdown()
    {
        var harmony = _harmony;
        _harmony = null;
        try
        {
            harmony?.UnpatchSelf();
        }
        finally
        {
            EnhancePatch.Reset();
            MasterDataPatch.Reset();
            TranslationPatch.Reset();
        }
    }
}
