using AbyssMod.Patches;
using HarmonyLib;
using Xunit;

namespace AbyssMod.Tests;

public sealed class PatchManagerTests : IDisposable
{
    [Fact]
    public void FailedRollbackRetainsHandleForLaterCleanup()
    {
        Harmony.FailPatch = true;
        Harmony.FailUnpatch = true;
        Assert.Throws<InvalidOperationException>(PatchManager.Initialize);
        Assert.True(Harmony.Installed);
        int attempts = Harmony.UnpatchAttempts;
        Harmony.FailUnpatch = false;
        Assert.True(PatchManager.Shutdown());
        Assert.False(Harmony.Installed);
        Assert.Equal(attempts + 1, Harmony.UnpatchAttempts);
    }

    [Fact]
    public void FailedShutdownCanBeRetried()
    {
        PatchManager.Initialize();
        Harmony.FailUnpatch = true;
        Assert.False(PatchManager.Shutdown());
        Assert.True(Harmony.Installed);
        Harmony.FailUnpatch = false;
        Assert.True(PatchManager.Shutdown());
        Assert.False(Harmony.Installed);
    }

    public void Dispose()
    {
        Harmony.FailPatch = Harmony.FailUnpatch = false;
        PatchManager.Shutdown();
    }
}
