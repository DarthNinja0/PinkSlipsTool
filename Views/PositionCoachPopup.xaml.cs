using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class PositionCoachPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;
    private (int Value, string Name)? _selectedPosition;

    public PositionCoachPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        PositionListBox.ItemsSource = PositionNames.All.Select(p => p.Name).ToList();
        LoadRoster();
    }

    private void LoadRoster()
    {
        _players = _editor.GetPlayersByTeam(_userTeamIndex);
        YourRosterBox.ItemsSource = _players;
        YourRosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({_players.Count} players)";
        if (_players.Count == 0)
            StatusText.Text = "No players found. " + _editor.DiagnosticInfo();
    }

    private void SortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(SortBox, YourRosterBox, _players);

    private void YourRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = YourRosterBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void PositionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = PositionListBox.SelectedIndex;
        _selectedPosition = idx >= 0 && idx < PositionNames.All.Length ? PositionNames.All[idx] : null;
        UpdateReady();
    }

    private void UpdateReady()
    {
        if (_selectedPlayer != null && _selectedPosition != null)
        {
            ApplyButton.IsEnabled = true;
            StatusText.Text = $"Change {_selectedPlayer.Name} from {_selectedPlayer.Position} to {_selectedPosition.Value.Name}?";
        }
        else
        {
            ApplyButton.IsEnabled = false;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null || _selectedPosition == null) return;

        var player = _selectedPlayer;
        var pos = _selectedPosition.Value;

        var result = MessageBox.Show(
            $"Change {player.Name}'s position from {player.Position} to {pos.Name}?",
            "Confirm Position Change", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.SetPosition(player.RecordIndex, pos.Value))
        {
            StatusText.Text = "Position change failed — field detection may be incorrect";
            return;
        }

        StatusText.Text = $"CHANGED: {player.Name} is now a {pos.Name}! Save the dynasty file to write the change.";
        ApplyButton.IsEnabled = false;
        LoadRoster();
    }
}
