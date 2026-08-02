using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class RestoreBackupPopup : Window
{
    public string SelectedBackupPath { get; private set; }

    public RestoreBackupPopup(IEnumerable<BackupEntry> backups)
    {
        InitializeComponent();
        var list = backups.ToList();
        BackupListBox.ItemsSource = list;
        CountText.Text = list.Count == 1 ? "1 backup found" : $"{list.Count} backups found";
        if (list.Count > 0)
            BackupListBox.SelectedIndex = 0;
    }

    private void BackupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var entry = BackupListBox.SelectedItem as BackupEntry;
        RestoreButton.IsEnabled = entry != null;
        if (entry != null)
            StatusText.Text = $"Restore from backup created {entry.Timestamp:MM/dd/yyyy h:mm:ss tt}";
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupListBox.SelectedItem is BackupEntry entry)
        {
            SelectedBackupPath = entry.Path;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
