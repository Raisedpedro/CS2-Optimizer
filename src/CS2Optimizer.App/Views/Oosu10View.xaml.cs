using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using CS2Optimizer.App.ViewModels;

namespace CS2Optimizer.App.Views;

public partial class Oosu10View : UserControl
{
    private CollectionViewSource? _source;

    public Oosu10View()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not MainViewModel vm) return;

        _source = (CollectionViewSource)Resources["GroupedOosu10"];
        _source.Source = vm.Oosu10Visible;
        using var defer = _source.DeferRefresh();
        _source.GroupDescriptions.Clear();
        _source.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Oosu10TweakViewModel.Group)));
        _source.SortDescriptions.Clear();
        _source.SortDescriptions.Add(new SortDescription(
            nameof(Oosu10TweakViewModel.GroupOrder), ListSortDirection.Ascending));
        _source.SortDescriptions.Add(new SortDescription(
            nameof(Oosu10TweakViewModel.Title), ListSortDirection.Ascending));
    }
}
