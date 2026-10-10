using System.Collections.Generic;

namespace AbyssMod
{
    internal static class Config
    {
        internal const bool TranslationEnabledAtStartup = true;
        internal static BoolEntry Translation = new();

        internal sealed class BoolEntry
        {
            internal bool Value = true;
        }
    }
}

namespace Il2CppSystem
{
    public sealed class Type { }
}

namespace Il2CppAbsf.Master
{
    public interface IMasterLoadResult { }
}

namespace AbyssMod.Services
{
    internal sealed class MasterDataTranslator
    {
        internal int TableCount => 0;

        internal static MasterDataTranslator Create(
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> tables
        ) => new();

        internal bool Contains(Il2CppSystem.Type type) => false;

        internal bool TryTranslate(
            Il2CppSystem.Type type,
            Il2CppAbsf.Master.IMasterLoadResult result,
            out int count
        )
        {
            count = 0;
            return false;
        }
    }
}

namespace Utility.Notifications
{
    internal static class Toast
    {
        internal static readonly List<string> Messages = new();

        internal static void Warning(string title, string message) => Messages.Add(message);
    }
}
