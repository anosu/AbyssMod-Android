using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using Utility.Notifications;

namespace AbyssMod;

/// <summary>
/// 全局配置：初始化所有 MelonPreferences 配置项并绑定变更事件。
/// </summary>
public static class Config
{
    public static readonly string FilePath = Path.Combine(
        MelonEnvironment.UserDataDirectory,
        $"{ModInfo.Name}.cfg"
    );
    internal static string DefaultFontBundlePath => Path.Combine(
        MelonEnvironment.UserDataDirectory,
        ModInfo.Name,
        "ttcuyuanj"
    );
    internal static string LegacyFontBundlePath => Path.Combine(
        Application.persistentDataPath,
        "il2cpp",
        "ttcuyuanj"
    );

#if DEBUG
    public static MelonPreferences_Entry<bool> Offline;
    public static MelonPreferences_Entry<string> OfflineAPI;
    public static MelonPreferences_Entry<string> DmmSdkAPI;
    public static bool OfflineStartup;
#endif

    public static MelonPreferences_Entry<bool> DynamicMosaic;
    public static MelonPreferences_Entry<bool> SoundCaution;
    public static MelonPreferences_Entry<bool> VoiceInterruption;
    public static MelonPreferences_Entry<bool> TitleMovie;
    public static MelonPreferences_Entry<float> NovelLive2DScale;

    public static MelonPreferences_Entry<bool> Translation;
    public static MelonPreferences_Entry<string> TranslationCDN;
    public static MelonPreferences_Entry<string> TranslationLanguage;
    public static MelonPreferences_Entry<string> TranslationCacheDirectory;
    public static MelonPreferences_Entry<bool> TranslationPreferLocalFiles;
    public static MelonPreferences_Entry<string> TranslationCryptoTag;
    public static MelonPreferences_Entry<string> TranslationCryptoKey;
    public static MelonPreferences_Entry<string> FontBundlePath;

    internal static bool TranslationEnabledAtStartup { get; private set; }
    private static bool _initializing;
    private static bool _entriesBound;
    private static MelonPreferences_Category _preferenceCategory;

    public static void Initialize()
    {
        _initializing = true;
        try
        {
            if (!_entriesBound)
            {
                BindAllEntries();
                _entriesBound = true;
            }
            _preferenceCategory.LoadFromFile(false);
            TranslationEnabledAtStartup = Translation.Value;
        }
        finally
        {
            _initializing = false;
        }
    }

    private static void BindAllEntries()
    {
#if DEBUG
        var debug = CreateCategory("Debug.Offline");
        Offline = CreateEntry(debug, "Enabled", false, "API localization");
        OfflineAPI = CreateEntry(
            debug,
            "API",
            "http://localhost:33333/abyss/",
            "API for debugging"
        );
        DmmSdkAPI = CreateEntry(
            debug,
            "DmmSdkAPI",
            "http://localhost:33333/dmmsdk",
            "API for debugging"
        );
#endif

        var general = CreateCategory("General");
        DynamicMosaic = CreateEntry(general, "DynamicMosaic", false, "是否启用游戏内动态马赛克");
        SoundCaution = CreateEntry(
            general,
            "SoundCaution",
            false,
            "是否启用进入游戏时的音量提醒弹窗"
        );
        VoiceInterruption = CreateEntry(
            general,
            "VoiceInterruption",
            false,
            "剧情中播放下一段无声文本时是否中断当前角色语音"
        );
        TitleMovie = CreateEntry(general, "TitleMovie", true, "是否开启进入游戏时的标题动画");
        NovelLive2DScale = CreateEntry(
            general,
            "NovelLive2DScale",
            1.0f,
            "剧情 Live2D 的缩放倍率（进剧情时应用，范围 0.1 ~ 10.0）"
        );

        var translation = CreateCategory("Translation");
        Translation = CreateEntry(
            translation,
            "Enabled",
            true,
            "是否开启翻译；MasterData 与 UI 文本仅在启动时读取此设置，剧情翻译可在运行时切换"
        );
        TranslationCDN = CreateEntry(
            translation,
            "CDN",
            "https://raw.githubusercontent.com/anosu/dotabyss-translation/refs/heads/main/translations",
            "翻译加载的CDN，修改后重启生效"
        );
        TranslationLanguage = CreateEntry(
            translation,
            "Language",
            "zh_Hans",
            "翻译语言，取值范围：[zh_Hans]，修改后重启生效"
        );

        var cache = CreateCategory("Translation.Cache");
        TranslationCacheDirectory = CreateEntry(
            cache,
            "Directory",
            $"{ModInfo.Name}/translations",
            "翻译缓存目录，默认相对于用户数据目录，也可使用绝对路径；修改后重启生效"
        );
        TranslationPreferLocalFiles = CreateEntry(
            cache,
            "PreferLocalFiles",
            false,
            "本地翻译文件存在时是否忽略清单哈希并优先使用本地文件（manifest 除外）；修改后重启生效"
        );

        var crypto = CreateCategory("Translation.Crypto");
        TranslationCryptoTag = CreateEntry(crypto, "Tag", "ENC:", "翻译文本加密标签（可选）");
        TranslationCryptoKey = CreateEntry(
            crypto,
            "Key",
            "woshitonghuadawang",
            "翻译文本解密密钥（可选）"
        );

        var font = CreateCategory("Translation.Font");
        FontBundlePath = CreateEntry(
            font,
            "AssetBundlePath",
            DefaultFontBundlePath,
            $"TMP字体AssetBundle的路径，默认取 MelonLoader/UserData/{ModInfo.Name}/ttcuyuanj，也可使用绝对路径；修改后重启生效"
        );
    }

    private static MelonPreferences_Category CreateCategory(string name)
    {
        var category = MelonPreferences.CreateCategory(name);
        category.SetFilePath(FilePath, false, false);
        _preferenceCategory ??= category;
        return category;
    }

    private static MelonPreferences_Entry<T> CreateEntry<T>(
        MelonPreferences_Category category,
        string key,
        T defaultValue,
        string description
    )
    {
        var entry = category.CreateEntry(key, defaultValue, description, description);
        entry.OnEntryValueChanged.Subscribe(
            (_, newValue) =>
            {
                if (_initializing)
                    return;
                category.SaveToFile(false);
                object value = ReferenceEquals(entry, TranslationCryptoKey) ? "***" : newValue;
                Core.Log.Msg($"[{category.Identifier}] {key} => {value}");
                Toast.Info($"[{category.Identifier}]", $"{key} => {value}");
            }
        );
        return entry;
    }
}
