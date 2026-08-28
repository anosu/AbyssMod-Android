using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Il2CppAbsf.Master;
using Il2CppProject.Master;
using MelonLoader;

namespace AbyssMod.Patches;

/// <summary>
/// 在 MasterData 单表写入缓存后立即替换静态文本。
/// 剧情正文脚本不在 masterdata 内，仍由 TranslationPatch 处理。
/// </summary>
[HarmonyPatch]
public static class MasterDataPatch
{
    private const string DownloadCallbackPrefix = "_DownloadFirstAsync_b__";
    private const int SummaryIdleFrames = 60;

    private static readonly Dictionary<
        string,
        (Il2CppSystem.Type Type, IMasterLoadResult Result)
    > Pending = new(StringComparer.Ordinal);
    private static readonly HashSet<string> ProcessedTables = new(StringComparer.Ordinal);
    private static bool _pendingTranslationRunning;
    private static bool _summaryCoroutineRunning;
    private static bool _summaryLogged;
    private static int _summaryRevision;
    private static int _summaryEpoch;
    private static int _translatedTables;
    private static int _translatedEntries;
    private static int _pendingEpoch;

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        Type[] parameterTypes =
        {
            typeof(Il2CppSystem.Type),
            typeof(Il2CppSystem.Object),
            typeof(bool),
        };
        var method = AccessTools
            .GetDeclaredMethods(typeof(MasterDataStore))
            .SingleOrDefault(candidate =>
                candidate.Name.StartsWith(DownloadCallbackPrefix, StringComparison.Ordinal)
                && candidate.ReturnType == typeof(void)
                && candidate
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .SequenceEqual(parameterTypes)
            );

        return method
            ?? throw new MissingMethodException(
                typeof(MasterDataStore).FullName,
                $"{DownloadCallbackPrefix}*(Type, Object, Boolean)"
            );
    }

    [HarmonyPostfix]
    public static void TranslateCachedTable(
        MasterDataStore __instance,
        Il2CppSystem.Type elementType
    )
    {
        try
        {
            bool ready = Core.Trans.MasterDataTranslationReady;
            if (ready && !Core.Trans.HasMasterDataTranslation(elementType))
                return;

            var result = __instance._caches[elementType];
            if (ready)
            {
                Translate(elementType, result);
                return;
            }

            Pending[elementType.Name] = (elementType, result);
            if (!_pendingTranslationRunning)
            {
                _pendingTranslationRunning = true;
                try
                {
                    MelonCoroutines.Start(TranslatePendingWhenReady(_pendingEpoch));
                }
                catch
                {
                    _pendingTranslationRunning = false;
                    throw;
                }
            }
            else
                _ = Core.Trans.EnsureStaticTranslationsLoadedAsync();
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterDataTranslation] failed [{elementType?.Name}]: {e}");
        }
    }

    private static IEnumerator TranslatePendingWhenReady(int epoch)
    {
        Task loadTask;
        try
        {
            loadTask = Core.Trans.EnsureStaticTranslationsLoadedAsync();
        }
        catch (Exception e)
        {
            Logger.Error($"[MasterDataTranslation] deferred load failed: {e}");
            _pendingTranslationRunning = false;
            yield break;
        }

        while (!loadTask.IsCompleted && epoch == _pendingEpoch)
            yield return null;

        if (epoch != _pendingEpoch)
            yield break;

        try
        {
            try
            {
                loadTask.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Logger.Error($"[MasterDataTranslation] deferred translation load failed: {e}");
            }

            if (!Core.Trans.MasterDataTranslationReady)
            {
                Logger.Warn(
                    $"[MasterDataTranslation] plan unavailable; skipped cached tables: {Pending.Count}"
                );
                Pending.Clear();
                yield break;
            }

            if (Pending.Count == 0)
                yield break;

            foreach (var (type, result) in Pending.Values)
                Translate(type, result);
            Pending.Clear();
        }
        finally
        {
            if (epoch == _pendingEpoch)
                _pendingTranslationRunning = false;
        }
    }

    private static void Translate(Il2CppSystem.Type type, IMasterLoadResult result)
    {
        if (!Core.Trans.TryTranslateMasterDataCache(type, result, out int translatedEntries))
            return;

        string typeName = type.Name;
        if (!ProcessedTables.Add(typeName))
            return;

        if (translatedEntries > 0)
        {
            _translatedTables++;
            _translatedEntries += translatedEntries;
        }

        int plannedTables = Core.Trans.MasterDataTranslationTableCount;
        if (!_summaryLogged && plannedTables > 0 && ProcessedTables.Count >= plannedTables)
            LogSummary(plannedTables);
        else
            ScheduleSummary();
    }

    private static void ScheduleSummary()
    {
        _summaryRevision++;
        if (_summaryCoroutineRunning || _summaryLogged)
            return;

        _summaryCoroutineRunning = true;
        try
        {
            MelonCoroutines.Start(LogSummaryWhenIdle(_summaryEpoch));
        }
        catch
        {
            _summaryCoroutineRunning = false;
            throw;
        }
    }

    private static IEnumerator LogSummaryWhenIdle(int epoch)
    {
        try
        {
            while (!_summaryLogged && epoch == _summaryEpoch)
            {
                int revision = _summaryRevision;
                for (int frame = 0; frame < SummaryIdleFrames; frame++)
                    yield return null;

                if (revision == _summaryRevision)
                    LogSummary(Core.Trans.MasterDataTranslationTableCount);
            }
        }
        finally
        {
            if (epoch == _summaryEpoch)
                _summaryCoroutineRunning = false;
        }
    }

    private static void LogSummary(int plannedTables)
    {
        _summaryLogged = true;
        Logger.Info(
            $"[MDT] MasterData translation summary. Planned: {plannedTables}, "
                + $"Processed: {ProcessedTables.Count}, Translated: {_translatedTables}, "
                + $"Entries: {_translatedEntries}"
        );
    }

    internal static void Reset()
    {
        ResetPending();
        ProcessedTables.Clear();
        _summaryEpoch++;
        _summaryCoroutineRunning = false;
        _summaryLogged = false;
        _summaryRevision = 0;
        _translatedTables = 0;
        _translatedEntries = 0;
    }

    private static void ResetPending()
    {
        _pendingEpoch++;
        Pending.Clear();
        _pendingTranslationRunning = false;
    }
}
