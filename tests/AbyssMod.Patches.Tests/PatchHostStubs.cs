namespace HarmonyLib
{
    internal sealed class Harmony
    {
        internal static bool FailPatch;
        internal static bool FailUnpatch;
        internal static bool Installed;
        internal static int UnpatchAttempts;

        internal Harmony(string id) { }

        internal void PatchAll(Type type)
        {
            Installed = true;
            if (FailPatch)
                throw new InvalidOperationException("Simulated patch failure");
        }

        internal void PatchAll(System.Reflection.Assembly assembly) => PatchAll(typeof(Harmony));

        internal void UnpatchSelf()
        {
            UnpatchAttempts++;
            if (FailUnpatch)
                throw new InvalidOperationException("Simulated unpatch failure");
            Installed = false;
        }
    }
}

namespace AbyssMod
{
    internal static class Logger
    {
        internal static void Error(string text) { }

        internal static void Info(string text) { }

        internal static void Warn(string text) { }
    }

    internal static class ModInfo
    {
        internal const string Name = "AbyssMod";
    }
}

namespace AbyssMod
{
    internal static class Config
    {
        public static bool TranslationEnabledAtStartup = true;
    }
}

namespace AbyssMod.Patches
{
    internal static class EnhancePatch
    {
        internal static void Reset() { }
    }

    internal static class MasterDataPatch
    {
        internal static void Reset() { }
    }

    internal sealed class UiTranslationPatch { }

    internal static class TranslationPatch
    {
        internal static void Reset() { }
    }
}
