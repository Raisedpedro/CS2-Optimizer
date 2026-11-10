using CS2Optimizer.Core.Fixes;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Tests;

public sealed class FixCatalogTests
{
    [Fact]
    public void Build_only_returns_items_with_explanation_and_action()
    {
        var fixes = FixCatalog.Build(new FixContext());

        Assert.NotEmpty(fixes);
        Assert.All(fixes, fix => Assert.False(string.IsNullOrWhiteSpace(fix.Explanation)));
        Assert.All(fixes, fix => Assert.False(string.IsNullOrWhiteSpace(fix.Detail)));
        Assert.All(fixes, fix => Assert.NotNull(fix.ApplyAsync));
    }

    [Fact]
    public void Recommended_items_are_safe_or_the_explicit_optional_allowlist()
    {
        var allowlist = new[] {
            "mouse", "trim", "temp", "shaders", "syscache",
            "privacy-clipboard", "privacy-suggestions", "privacy-app-caps",
            "privacy-cortana", "privacy-spynet", "privacy-update-p2p", "privacy-explorer",
            "copilot-disable", "copilot-recall", "copilot-taskbar",
            "phone-link", "phone-suggestions",
            "taskbar-widgets", "taskbar-meetnnow",
            "sync-credentials", "sync-onedrive",
            "privacy-handwriting", "privacy-bluetooth-ads", "privacy-lockscreen-camera",
            "privacy-inventory", "privacy-onesettings", "privacy-location-service",
            "privacy-edge", "privacy-device-meta", "privacy-people-taskbar",
            "privacy-update-defer"
        };
        var fixes = FixCatalog.Build(new FixContext());

        foreach (var fix in fixes.Where(f => f.Recommended))
        {
            Assert.True(
                fix.Level == FixLevel.Safe || allowlist.Contains(fix.Id),
                $"Item recomendado inesperado: {fix.Id} ({fix.Level})");
        }
    }

    [Fact]
    public void No_risky_item_is_recommended()
    {
        var fixes = FixCatalog.Build(new FixContext());
        Assert.DoesNotContain(fixes, fix => fix.Level == FixLevel.Risky && fix.Recommended);
    }

    [Fact]
    public void Power_plan_is_safe_and_recommended_on_desktop()
    {
        var fixes = FixCatalog.Build(new FixContext());
        var power = Assert.Single(fixes, fix => fix.Id == "power-plan");

        Assert.Equal(FixLevel.Safe, power.Level);
        Assert.True(power.Recommended);
    }

    [Fact]
    public void Power_plan_is_optional_and_not_recommended_on_battery()
    {
        var fixes = FixCatalog.Build(new FixContext { HasBattery = true });
        var power = Assert.Single(fixes, fix => fix.Id == "power-plan");

        Assert.Equal(FixLevel.Optional, power.Level);
        Assert.False(power.Recommended);
    }

    [Fact]
    public void Power_plan_is_not_offered_when_already_on_performance_plan()
    {
        var fixes = FixCatalog.Build(new FixContext { OnPerformancePowerPlan = true });
        Assert.DoesNotContain(fixes, fix => fix.Id == "power-plan");
    }
}
