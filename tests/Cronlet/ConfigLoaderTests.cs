using Xunit;

namespace HiroMuraki.Cronlet.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Default_template_deserializes()
    {
        var config = ConfigLoader.Parse(ConfigLoader.DefaultTemplate);
        Assert.NotNull(config);
        var task = Assert.Single(config!.tasks);
        Assert.Equal("example", task.name);
        Assert.False(task.enabled);
        Assert.Equal("pwsh -File ./scripts/example.ps1", task.command);
        Assert.Equal("*/5 * * * *", task.cron);
        Assert.Equal(10, task.jitter);
        Assert.False(config.autostart); // 模板里是注释掉的，默认不随登录启动
        Assert.Equal(60, task.timeout);
        Assert.Equal("skip", task.overlap);
    }

    [Fact]
    public void AutoStart_is_read_from_the_file()
    {
        var config = ConfigLoader.Parse("autostart = true\n");
        Assert.True(config.autostart);
    }

    [Fact]
    public void SetAutoStartText_replaces_the_existing_line()
    {
        const string text = "autostart = true\n\n[[tasks]]\nname = \"a\"\n";

        string updated = ConfigLoader.SetAutoStartText(text, false);

        Assert.Equal("autostart = false\n\n[[tasks]]\nname = \"a\"\n", updated);
        Assert.False(ConfigLoader.Parse(updated).autostart);
    }

    [Fact]
    public void SetAutoStartText_keeps_indentation_of_the_replaced_line()
    {
        const string text = "upcoming = 5\n  autostart = true\n[[tasks]]\nname = \"a\"\n";

        string updated = ConfigLoader.SetAutoStartText(text, false);

        Assert.Equal("upcoming = 5\n  autostart = false\n[[tasks]]\nname = \"a\"\n", updated);
    }

    [Fact]
    public void SetAutoStartText_inserts_before_the_first_table()
    {
        const string text = "# 说明\n\n[[tasks]]\nname = \"a\"\n";

        string updated = ConfigLoader.SetAutoStartText(text, true);

        Assert.Equal("# 说明\n\nautostart = true\n[[tasks]]\nname = \"a\"\n", updated);
        Assert.True(ConfigLoader.Parse(updated).autostart);
    }

    [Fact]
    public void SetAutoStartText_appends_when_there_is_no_table()
    {
        const string text = "upcoming = 5\n";

        Assert.Equal("upcoming = 5\nautostart = true\n", ConfigLoader.SetAutoStartText(text, true));
    }

    [Fact]
    public void SetAutoStartText_ignores_commented_lines()
    {
        const string text = "# autostart = false\n# 用来开关开机启动\n[[tasks]]\nname = \"a\"\n";

        string updated = ConfigLoader.SetAutoStartText(text, true);

        Assert.Equal("# autostart = false\n# 用来开关开机启动\nautostart = true\n[[tasks]]\nname = \"a\"\n", updated);
    }

    [Fact]
    public void SetAutoStartText_keeps_crlf()
    {
        const string text = "upcoming = 5\r\n\r\n[[tasks]]\r\nname = \"a\"\r\n";

        string updated = ConfigLoader.SetAutoStartText(text, true);

        Assert.Equal("upcoming = 5\r\n\r\nautostart = true\r\n[[tasks]]\r\nname = \"a\"\r\n", updated);
    }

    [Fact]
    public void SetAutoStartText_is_idempotent()
    {
        const string text = "autostart = true\n[[tasks]]\nname = \"a\"\n";

        string once = ConfigLoader.SetAutoStartText(text, true);

        Assert.Equal("autostart = true\n[[tasks]]\nname = \"a\"\n", once);
        Assert.Equal(once, ConfigLoader.SetAutoStartText(once, true));
    }

    [Fact]
    public void LoadOrCreate_writes_template_when_missing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cronlet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "config.toml");
        try
        {
            var config = ConfigLoader.LoadOrCreate(path);
            Assert.True(File.Exists(path));
            Assert.Single(config.tasks);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_reads_existing_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cronlet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "config.toml");
        try
        {
            File.WriteAllText(path, """
                [[tasks]]
                name = "campus"
                command = "echo hi"
                cron = "*/2 * * * *"
                jitter = 10
                timeout = 120
                overlap = "skip"
                """);

            var config = ConfigLoader.LoadOrCreate(path);
            var task = Assert.Single(config.tasks);
            Assert.Equal("campus", task.name);
            Assert.True(task.enabled); // 默认值
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
