namespace AbyssMod;

/// <summary>
/// 统一日志封装，避免各处直接引用 Core.Log。
/// </summary>
public static class Logger
{
    public static void Info(string msg) => Core.Log.Msg(msg);

    public static void Warn(string msg) => Core.Log.Warning(msg);

    public static void Error(string msg) => Core.Log.Error(msg);
}
