using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using AbyssMod.Patches;
using AbyssMod.Services;
using MelonLoader;
using MelonLoader.Utils;
using Utility.Diagnostics;
using Utility.Notifications;

[assembly: MelonInfo(typeof(AbyssMod.Core), AbyssMod.ModInfo.Name, AbyssMod.ModInfo.Version, AbyssMod.ModInfo.Author)]

namespace AbyssMod;

public sealed class Core : MelonMod
{
    private const int HttpTimeoutSeconds = 10;
    private const int PooledConnectionLifetimeMinutes = 5;
    private const int PooledConnectionIdleTimeoutMinutes = 2;

    public static MelonLogger.Instance Log { get; private set; }
    public static TranslationManager Trans;
    private static HttpClient _httpClient;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;

        try
        {
            InitializeUtility();

#if DEBUG
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--offline") || args.Contains("-o"))
                Config.OfflineStartup = true;
#endif

            Config.Initialize();
            Initialize();
            Trans.Initialize();
            PatchManager.Initialize();

            Toast.Success(ModInfo.Name, $"Mod 加载成功，版本: {ModInfo.Version}");
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
            PatchManager.Shutdown();
        }
        catch (Exception e)
        {
            Log?.Error($"Patch shutdown failed: {e}");
        }
        finally
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
            string text = entry.Exception == null
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
        _httpClient = new HttpClient(new CryptoHandler(handler))
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

        Trans = new TranslationManager(
            cache,
            ResolveFontBundlePath(Config.FontBundlePath.Value)
        );
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
