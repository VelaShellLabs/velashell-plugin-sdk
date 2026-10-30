using VelaShell.PluginSdk.Protocols;
using VelaShell.PluginSdk.Workspaces;

namespace VelaShell.PluginSdk.Tests.Workspaces;

/// <summary>
/// 连接表单自定义分页(<see cref="WorkspaceDescriptor.Pages" /> / <see cref="ProtocolSettingField.Page" />)
/// 与类型卡片图标(<see cref="WorkspaceVariant.Icon" />)这三个只增成员。
/// </summary>
/// <remarks>
/// 值得钉住的是**默认值**:老插件一个都不填,宿主拿到的必须是「没有自定义页、字段在默认位置、
/// 卡片回落到描述符图标」—— 而不是 <see langword="null" /> 列表或别的意外形态。
/// 再钉住一条:新成员不参与变体解析,填了图标与分页的描述符照旧按值挑变体。
/// </remarks>
[TestClass]
[TestCategory("Plugins")]
public sealed class WorkspaceDescriptorPagesTests
{
    private const string VariantKey = "dialect";

    [TestMethod]
    public void Pages_DefaultsToEmpty_Page_DefaultsToNull_VariantIcon_DefaultsToNull()
    {
        WorkspaceDescriptor descriptor = new() { Id = "acme.db", DisplayName = "DB", DefaultPort = 3306 };
        ProtocolSettingField field = new() { Key = "database", Label = "Database" };
        WorkspaceVariant variant = new() { Value = "mysql" };

        Assert.IsNotNull(descriptor.Pages, "老插件不填分页时宿主拿到的是空列表,不是 null。");
        Assert.AreEqual(0, descriptor.Pages.Count);
        Assert.IsNull(field.Page, "不填分页的字段留在默认位置(由 IsAdvanced 决定常规还是高级)。");
        Assert.IsNull(variant.Icon, "不填图标的变体回落到描述符图标。");
    }

    [TestMethod]
    public void ResolveVariant_IsUnaffectedByPagesOrIcon()
    {
        PluginIcon mysqlIcon = PluginIcon.Stroked("M4 6h16");
        WorkspaceDescriptor withPages = new()
        {
            Id = "acme.db",
            DisplayName = "DB",
            DefaultPort = 3306,
            VariantKey = VariantKey,
            Fields =
            [
                new ProtocolSettingField
                {
                    Key = VariantKey,
                    Label = "Type",
                    Kind = ProtocolSettingKind.Choice,
                    Choices = [new("mysql", "MySQL"), new("pg", "PostgreSQL")],
                },
                new ProtocolSettingField { Key = "database", Label = "Database", Page = "db" },
                new ProtocolSettingField { Key = "sslMode", Label = "SSL mode", Page = "ssl" },
            ],
            Pages = [new ProtocolSettingPage("db", "数据库"), new ProtocolSettingPage("ssl", "SSL")],
            Variants =
            [
                new WorkspaceVariant { Value = "mysql", Icon = mysqlIcon, DefaultPort = 3306 },
                new WorkspaceVariant { Value = "pg", DefaultPort = 5432 },
            ],
        };

        WorkspaceVariant? mysql = withPages.ResolveVariant(key => key == VariantKey ? "mysql" : null);
        Assert.AreSame(withPages.Variants[0], mysql, "同 Value 带图标的变体照常解析。");
        Assert.AreEqual(mysqlIcon, mysql!.Icon);
        Assert.AreEqual(3306, mysql.DefaultPort);

        WorkspaceVariant? pg = withPages.ResolveVariant(key => key == VariantKey ? "pg" : null);
        Assert.AreSame(withPages.Variants[1], pg);
        Assert.IsNull(pg!.Icon);

        // 分页键不是变体键:把分页键答成某个变体的值,也不能让解析跑偏。
        Assert.IsNull(withPages.ResolveVariant(key => key == "db" ? "mysql" : null));
        Assert.IsNull(withPages.ResolveVariant(key => key == VariantKey ? "sqlite" : null));
    }
}
