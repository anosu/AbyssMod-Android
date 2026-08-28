using System.Collections.Generic;
using HarmonyLib;
using Il2CppAbsf;
using Il2CppAbsf.Api;
using Il2CppProject.Notice;
using Il2CppProject.Novel;
using Il2CppSystem.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility.Notifications;

namespace AbyssMod.Patches;

/// <summary>
/// 游戏通用增强：关闭动态马赛克、音量警告、标题动画、语音中断控制、网络超时、Live2D 缩放。
/// Android 版移除快捷键与 Ctrl+滚轮运行时缩放，Live2D 缩放仅按配置值在进剧情时应用。
/// </summary>
[HarmonyPatch]
public static class EnhancePatch
{
    private static readonly Dictionary<Transform, Vector3> _novelLive2DOriginalScales = new();
    private static int _allowStopVoiceCount;
    private static float _novelLive2DScale = float.NaN;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelLive2DObject), nameof(NovelLive2DObject.Initialize))]
    public static void DisableMosaic(NovelLive2DObject __instance)
    {
        if (Config.DynamicMosaic.Value)
            return;

        var drawables = __instance.GetDrawables();
        if (drawables == null)
            return;

        foreach (var d in drawables)
        {
            if (d.name.StartsWith("Mosaic", System.StringComparison.Ordinal))
                d.gameObject.SetActive(false);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(SoundCautionPopupController),
        nameof(SoundCautionPopupController.SetupPopupEvent)
    )]
    public static void DisableSoundCaution(SoundCautionPopupController __instance)
    {
        if (!Config.SoundCaution.Value)
            __instance._onClickOk?.Invoke();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelSoundManager), nameof(NovelSoundManager.StopCategory))]
    public static bool CancelStoppingVoice(int nCategory, bool playFade)
    {
        if (Config.VoiceInterruption.Value || _allowStopVoiceCount > 0)
            return true;

        return nCategory != 2 || playFade;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelSoundManager), nameof(NovelSoundManager.PlaySound))]
    public static void StopVoiceBeforePlaying(NovelSoundManager __instance, SoundCategory category)
    {
        if (!Config.VoiceInterruption.Value && category == SoundCategory.Voice)
        {
            _allowStopVoiceCount++;
            try
            {
                __instance.StopCategory(2, false);
            }
            finally
            {
                _allowStopVoiceCount--;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(Il2CppProject.Title.TopView),
        nameof(Il2CppProject.Title.TopView.PlayMovie)
    )]
    public static void DisableTitleMovie(
        Il2CppProject.Title.TopView __instance,
        CancellationToken ct
    )
    {
        if (!Config.TitleMovie.Value)
        {
            if (!__instance.MovieComplete)
                __instance.MovieSkip(ct);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(Il2CppProject.Title.TopView),
        nameof(Il2CppProject.Title.TopView.CheckMovieLoop)
    )]
    public static bool DisableTitleMovieLoop()
    {
        return Config.TitleMovie.Value;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnityWebRequest), nameof(UnityWebRequest.timeout), MethodType.Setter)]
    public static void ChangeTimeoutLimit(ref int value)
    {
        value = 60;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NovelLive2DController), nameof(NovelLive2DController.Setup))]
    public static void BeginNovelLive2DScale(NovelLive2DController __instance)
    {
        var root = __instance._canvasRoot;
        if (root == null)
            return;

        if (!_novelLive2DOriginalScales.TryGetValue(root, out var originalScale))
        {
            originalScale = root.localScale;
            _novelLive2DOriginalScales[root] = originalScale;
        }
        root.localScale = ScaleNovelLive2D(originalScale, GetNovelLive2DScale());
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NovelLive2DController), nameof(NovelLive2DController.Release))]
    public static void EndNovelLive2DScale(NovelLive2DController __instance)
    {
        var root = __instance._canvasRoot;
        if (
            !object.ReferenceEquals(root, null)
            && _novelLive2DOriginalScales.Remove(root, out var originalScale)
            && root != null
        )
            root.localScale = originalScale;
    }

    private static float GetNovelLive2DScale()
    {
        if (float.IsNaN(_novelLive2DScale))
            _novelLive2DScale = Mathf.Clamp(Config.NovelLive2DScale.Value, 0.1f, 10.0f);

        return _novelLive2DScale;
    }

    private static Vector3 ScaleNovelLive2D(Vector3 originalScale, float scale) =>
        new(originalScale.x * scale, originalScale.y * scale, originalScale.z);

    internal static void Reset()
    {
        _allowStopVoiceCount = 0;
        _novelLive2DScale = float.NaN;

        foreach (var (root, originalScale) in _novelLive2DOriginalScales)
        {
            try
            {
                if (!object.ReferenceEquals(root, null) && root != null)
                    root.localScale = originalScale;
            }
            catch (System.Exception e)
            {
                Logger.Warn($"Failed to restore Live2D scale during shutdown: {e.Message}");
            }
        }
        _novelLive2DOriginalScales.Clear();
    }
}
