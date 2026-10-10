using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using AbyssMod.Patches;
using AbyssMod.Services;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using Utility.Assets;
using Utility.Diagnostics;
using Utility.Notifications;

[assembly: MelonInfo(
    typeof(AbyssMod.Core),
    AbyssMod.ModInfo.Name,
    AbyssMod.ModInfo.Version,
    AbyssMod.ModInfo.Author
)]
[assembly: HarmonyDontPatchAll]

namespace AbyssMod;

public sealed class Core : MelonMod
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;

    public static MelonLogger.Instance Log { get; private set; }
    public static TranslationManager Trans;
    private static HttpClient _httpClient;
    private static AssetBundleLoader<TMP_FontAsset> _font;
    private static IEnumerator _fontLoad;
    private static object _fontCoroutine;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        try
        {
            InitializeUtility();

            Config.Initialize();
            Initialize();
            Trans.Initialize();
            InitializeFont();
            PatchManager.Initialize();

            Toast.Success(ModInfo.Name, $"Mod 加载成功，版本: {ModInfo.Version}", duration: 7f);
        }
        catch (Exception e)
        {
            Log.Error($"Initialization failed: {e}");
            Shutdown();
            throw;
        }
    }

    public override void OnDeinitializeMelon() => Shutdown();

    private static void Shutdown()
    {
        try
        {
            if (!PatchManager.Shutdown())
                return;
        }
        catch (Exception e)
        {
            Log?.Error($"Patch shutdown failed: {e}");
            return;
        }
        // 只有补丁移除成功后，才释放它们访问的运行依赖。
        {
            try
            {
                Trans?.Shutdown();
            }
            catch (Exception e)
            {
                Log?.Error($"Translation shutdown failed: {e}");
            }
            Trans = null;
            Config.Shutdown();
            if (_fontCoroutine != null)
                MelonCoroutines.Stop(_fontCoroutine);
            (_fontLoad as IDisposable)?.Dispose();
            if (_font?.IsLoaded == true)
                TMP_Settings.fallbackFontAssets.Remove(_font.Asset);
            _font = null;
            _fontLoad = null;
            _fontCoroutine = null;
            _httpClient?.Dispose();
            _httpClient = null;
            try
            {
                Toast.Shutdown();
            }
            catch (Exception e)
            {
                Log?.Error($"Toast shutdown failed: {e}");
            }
            Logging.SetSink(null);
        }
    }

    private static void InitializeUtility()
    {
        Logging.SetSink(entry =>
        {
            string text =
                entry.Exception == null
                    ? $"[{entry.Category}] {entry.Message}"
                    : $"[{entry.Category}] {entry.Message}\n{entry.Exception}";

            switch (entry.Level)
            {
                case LogLevel.Warning:
                    Log.Warning(text);
                    break;
                case LogLevel.Error:
                    Log.Error(text);
                    break;
                default:
                    Log.Msg(text);
                    break;
            }
        });
        Toast.Initialize($"{ModInfo.Name}.ToastManager");
    }

    private static void Initialize()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(PooledConnectionLifetimeMinutes),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(PooledConnectionIdleTimeoutMinutes),
        };
        _httpClient = new HttpClient(
            new TranslationCryptoHandler(
                handler,
                () => (Config.TranslationCryptoTag.Value, Config.TranslationCryptoKey.Value)
            )
        )
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds),
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"{ModInfo.Name}/{ModInfo.Version}");

        string cacheDir = ResolveUserDataPath(Config.TranslationCacheDirectory.Value);
        Log.Msg($"Translation cache directory: {cacheDir}");
        var cache = new TranslationCache(
            Config.TranslationCDN.Value,
            cacheDir,
            Config.TranslationLanguage.Value,
            Config.TranslationPreferLocalFiles.Value,
            _httpClient
        );

        Trans = new TranslationManager(cache);
    }

    private static void InitializeFont()
    {
        _font = new AssetBundleLoader<TMP_FontAsset>(
            ResolveFontBundlePath(Config.FontBundlePath.Value)
        );
        _fontLoad = _font.Load(
            () =>
            {
                if (!TMP_Settings.fallbackFontAssets.Contains(_font.Asset))
                    TMP_Settings.fallbackFontAssets.Add(_font.Asset);
                Logger.Info($"Font loaded: {_font.Asset.name}");
            },
            error => Toast.Error("字体加载失败", error.Message)
        );
        _fontCoroutine = MelonCoroutines.Start(_fontLoad);
    }

    private static string ResolveFontBundlePath(string path)
    {
        string resolvedPath = ResolveModsPath(path);
        if (File.Exists(resolvedPath))
        {
            if (string.Equals(resolvedPath, Config.DefaultFontBundlePath, StringComparison.Ordinal))
                Log.Msg($"Using packaged UserData font bundle: {resolvedPath}");
            return resolvedPath;
        }
        if (!string.Equals(resolvedPath, Config.LegacyFontBundlePath, StringComparison.Ordinal))
            return resolvedPath;

        if (File.Exists(Config.DefaultFontBundlePath))
        {
            Log.Msg($"Using packaged UserData font bundle: {Config.DefaultFontBundlePath}");
            return Config.DefaultFontBundlePath;
        }

        return resolvedPath;
    }

    private static string ResolveModsPath(string path) =>
        ResolvePath(path, MelonEnvironment.ModsDirectory);

    private static string ResolveUserDataPath(string path) =>
        ResolvePath(path, MelonEnvironment.UserDataDirectory);

    private static string ResolvePath(string path, string relativeBase)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Configured path cannot be empty", nameof(path));

        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(relativeBase, path));
    }
}
