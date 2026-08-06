using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool.Models;

internal static class PlayerSorter
{
    public static string CurrentMode(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Content as string ?? "OVR";

    public static List<PlayerData> Sort(IEnumerable<PlayerData> list, string mode) => mode switch
    {
        "Name" => list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        "Position" => list.OrderBy(p => p.PositionValue).ThenByDescending(p => p.OverallRating).ToList(),
        _ => list.OrderByDescending(p => p.OverallRating).ThenBy(p => p.Name).ToList(),
    };

    // Re-sorts the list box in place, preserving the current selection (by reference —
    // the items are the same PlayerData instances).
    public static void Apply(ComboBox box, ListBox list, List<PlayerData> full)
    {
        if (full == null) return;
        var sel = list.SelectedItem as PlayerData;
        list.ItemsSource = Sort(full, CurrentMode(box));
        if (sel != null) list.SelectedItem = sel;
    }
}
