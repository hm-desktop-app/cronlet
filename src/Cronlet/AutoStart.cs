using Microsoft.Win32;

namespace HiroMuraki.Cronlet;

/// <summary>
/// 开机启动：只维护当前用户的 Run 键（HKCU\...\Run），不需要管理员权限，也不落地额外文件。
/// 值固定是当前 exe 的带引号完整路径，所以开没开可以直接比对这个字符串。
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Cronlet";

    /// <summary>Run 键里应存的内容：带引号的 exe 路径，避免路径里的空格被 shell 拆开。</summary>
    internal static string CommandLine =>
        "\"" + (Environment.ProcessPath ?? throw new InvalidOperationException("无法确定当前进程路径")) + "\"";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value
               && string.Equals(value.Trim(), CommandLine, StringComparison.OrdinalIgnoreCase);
    }

    internal static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new IOException($@"无法打开注册表键 HKCU\{RunKeyPath}");

        if (enabled)
        {
            key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
