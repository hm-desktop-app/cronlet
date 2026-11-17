using System.Text;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Serialization;

namespace HiroMuraki.Cronlet;

/// <summary>源生成的绑定，避免反射序列化在 trimming / NativeAOT 下不可用。</summary>
[TomlSerializable(typeof(RunnerConfig))]
internal partial class TomlContext : TomlSerializerContext;

/// <summary>
/// config.toml 模型。属性名刻意用小写，与 TOML key 一一对应。
/// </summary>
internal sealed class RunnerConfig
{
    /// <summary>
    /// 是否随登录启动。这是唯一的事实来源：程序启动时按它写/删 Run 键，
    /// 所以程序目录被移动后，下一次启动就能把注册表里的旧路径纠正过来。
    /// </summary>
    public bool autostart { get; set; }

    /// <summary>托盘菜单顶部最多列出多少个待触发任务。</summary>
    public int upcoming { get; set; } = 10;

    /// <summary>哪些退出码要弹通知；空表示不按退出码挑选。</summary>
    public List<int> notify_if { get; set; } = new();

    /// <summary>哪些退出码不弹通知；优先于 <see cref="notify_if"/>。</summary>
    public List<int> notify_if_not { get; set; } = new() { 0 };

    public List<TaskConfig> tasks { get; set; } = new();

    /// <summary>收集未识别的顶层键，用来把拼写错误暴露成配置错误。</summary>
    [TomlExtensionData]
    public Dictionary<string, object>? extra { get; set; }
}

internal sealed class TaskConfig
{
    public string name { get; set; } = "";
    public bool enabled { get; set; } = true;

    /// <summary>要执行的完整命令行（交给 cmd）。相对路径以 <see cref="Paths.Base"/> 为当前目录。</summary>
    public string command { get; set; } = "";

    public string cron { get; set; } = "";
    public int jitter { get; set; }
    public int timeout { get; set; }
    public string overlap { get; set; } = "skip";
    public string? cwd { get; set; }

    /// <summary>覆盖顶层的 <see cref="RunnerConfig.notify_if"/>；null 表示沿用顶层。</summary>
    public List<int>? notify_if { get; set; }

    /// <summary>覆盖顶层的 <see cref="RunnerConfig.notify_if_not"/>；null 表示沿用顶层。</summary>
    public List<int>? notify_if_not { get; set; }

    /// <summary>收集未识别的任务键，例如把 enabled 写成 enabeld。</summary>
    [TomlExtensionData]
    public Dictionary<string, object>? extra { get; set; }
}

internal static partial class ConfigLoader
{
    public static string ConfigPath => Path.Combine(Paths.Base, "config.toml");

    public const string DefaultTemplate = """
# ==============================================================
# Cronlet 任务配置
#
# 每个 [[tasks]] 定义一个任务，可以写多组。
#
#   name        任务名（唯一，日志里用它标识）
#               只允许 A-Z a-z 0-9 _ -，长度 1-128
#   enabled     是否启用
#   command     完整命令行，交给 cmd 执行；相对路径以 exe 目录为当前目录
#               例：pwsh -File ./scripts/login.ps1
#                   python -X utf8 ./scripts/check.py
#   cron        5 段 unix cron：分 时 日 月 周（本地时区）
#   jitter      0..jitter 秒随机延迟；会被限制在下一次触发之前
#   timeout     秒；0 表示不限制，超时杀进程树
#   overlap     上次未结束时的策略：skip | kill | queue
#   cwd         工作目录；空则用 exe 目录
# ==============================================================

# 可选：是否随登录启动（默认 false）。程序启动时按这个值写/删
# HKCU\Software\Microsoft\Windows\CurrentVersion\Run，托盘菜单里的
# "开机启动"改的也是这一项。
# autostart = false

# 可选：托盘菜单顶部最多列出多少个待触发任务（默认 10）
# upcoming = 10

# 可选：哪些退出码要弹通知。两个列表都按"退出码"筛选，notify_if_not 优先；
# notify_if 非空时只认它里面的退出码，否则其余退出码都算要弹。
# 只有退出码与上一次不同才真的弹——连续同样的失败只提醒一次。
# 例如只对 1、2 弹：notify_if = [1, 2]
# notify_if = []
# notify_if_not = [0]


[[tasks]]
name = "example"
enabled = false
command = 'pwsh -File ./scripts/example.ps1'   # 也可以直接写一条命令，例如 'echo hello'
cron = "*/5 * * * *"                # 分 时 日 月 周（本地时区）
jitter = 10                         # 0..jitter 秒随机延迟，不会推过下一次触发
timeout = 60                        # 秒，0 表示不限制
overlap = "skip"                    # skip | kill | queue
cwd = ""                            # 工作目录，空则用 exe 目录
""";

    /// <summary>配置文件不存在时写一份模板；托盘"打开配置文件"也复用它。</summary>
    public static void EnsureExists(string path)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, DefaultTemplate, new UTF8Encoding(false));
        }
    }

    public static RunnerConfig LoadOrCreate(string path)
    {
        EnsureExists(path);
        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// 只改顶层 autostart 那一行，文件其余内容和注释原样保留。
    /// Run 键是派生状态，由启动时的 <see cref="AutoStart.Sync"/> 按这里的值修正。
    /// </summary>
    internal static void SetAutoStart(string path, bool enabled)
    {
        string text = SetAutoStartText(File.ReadAllText(path), enabled);
        string temp = path + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    [GeneratedRegex(@"^([ \t]*)autostart[ \t]*=[^\r\n]*", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AutoStartLine();

    [GeneratedRegex(@"^[ \t]*\[", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TableStart();

    /// <summary>
    /// 把文本里顶层的 autostart 改成给定值；没有这一行就插到第一个表定义之前。
    /// 只认行首，所以注释里出现的 autostart 字面量不受影响。
    /// </summary>
    internal static string SetAutoStartText(string text, bool enabled)
    {
        string value = enabled ? "true" : "false";
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        // 表层从第一个非注释的 "[..." 行开始，之前的部分只允许放顶层键
        var table = TableStart().Match(text);
        string head = table.Success ? text[..table.Index] : text;
        string rest = table.Success ? text[table.Index..] : "";

        if (AutoStartLine().IsMatch(head))
        {
            // $1 是原来的缩进：只改值，不动排版
            return AutoStartLine().Replace(head, "$1autostart = " + value, 1) + rest;
        }

        string prefix = head.Length > 0 && !head.EndsWith(newline, StringComparison.Ordinal) ? head + newline : head;
        return prefix + "autostart = " + value + newline + rest;
    }

    internal static RunnerConfig Parse(string text)
    {
        var config = TomlSerializer.Deserialize<RunnerConfig>(text, TomlContext.Default)
                     ?? throw new Exception("配置为空");

        RejectUnknownKeys(config.extra, null);

        foreach (var task in config.tasks)
        {
            string label = string.IsNullOrWhiteSpace(task.name) ? "(未命名)" : task.name;
            RejectUnknownKeys(task.extra, label);
        }

        return config;
    }

    /// <summary>
    /// Tomlyn 默认忽略未知键，拼错的表名或字段会静默生效（例如 tasks 写成 task 就变成零任务）。
    /// 这里把它们当成配置错误。
    /// </summary>
    private static void RejectUnknownKeys(Dictionary<string, object>? extra, string? taskName)
    {
        if (extra is null || extra.Count == 0) return;

        string keys = string.Join(", ", extra.Keys);
        throw new Exception(taskName is null
            ? $"顶层有未知的键: {keys}（检查拼写，例如表名应为 [[tasks]]）"
            : $"任务 {taskName}: 有未知字段: {keys}");
    }
}
