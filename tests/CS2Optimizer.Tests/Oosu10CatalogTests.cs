using CS2Optimizer.Core.Oosu10;
using CS2Optimizer.Core.Services;

namespace CS2Optimizer.Tests;

public sealed class Oosu10CatalogTests
{
    private static IReadOnlyList<Oosu10Tweak> Catalog => Oosu10Catalog.All;

    [Fact]
    public void Catalog_is_not_empty_and_covers_the_reg_file()
    {
        Assert.True(Catalog.Count >= 150, $"Catálogo pequeno demais: {Catalog.Count}");
    }

    [Fact]
    public void Ids_are_unique_and_filled()
    {
        var ids = Catalog.Select(t => t.Id).ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_item_has_title_detail_and_at_least_one_action()
    {
        Assert.All(Catalog, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Title));
            Assert.False(string.IsNullOrWhiteSpace(t.Detail));
            Assert.NotEmpty(t.Enable);
        });
    }

    [Fact]
    public void Groups_are_known()
    {
        var known = Oosu10Groups.Order.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(Catalog, t => Assert.Contains(t.Group, known));
    }

    [Fact]
    public void Preset_is_not_empty_and_never_contains_avoid_items()
    {
        var preset = Catalog.Where(t => t.InPreset).ToList();
        Assert.NotEmpty(preset);
        Assert.DoesNotContain(preset, t => t.Advice == Oosu10Advice.Evitar);
    }

    [Fact]
    public void Actions_only_touch_hklm_or_hkcu_with_supported_kinds()
    {
        foreach (var tweak in Catalog)
        {
            foreach (var action in tweak.Enable.Concat(tweak.Restore))
            {
                Assert.True(
                    action.Path.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase) ||
                    action.Path.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase),
                    $"{tweak.Id}: caminho inválido {action.Path}");

                if (!action.IsDelete)
                {
                    Assert.True(RegistryService.IsSupportedKind(action.Kind),
                        $"{tweak.Id}: tipo não suportado {action.Kind}");
                }
            }
        }
    }

    [Fact]
    public void Restore_actions_fall_back_to_deleting_the_enable_values()
    {
        var withoutRestore = Catalog.First(t => t.Restore.Count == 0);
        var actions = Oosu10Service.RestoreActions(withoutRestore);

        Assert.Equal(withoutRestore.Enable.Count, actions.Count);
        Assert.All(actions, a => Assert.True(a.IsDelete));
    }

    [Fact]
    public void Every_recommended_item_is_part_of_the_cs2_preset()
    {
        Assert.All(Catalog.Where(t => t.Advice == Oosu10Advice.Recomendado),
            t => Assert.True(t.InPreset, $"{t.Id} é recomendado mas ficou fora do preset."));
    }
}
