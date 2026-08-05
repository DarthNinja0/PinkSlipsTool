using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class TransferShockPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private readonly List<(int TeamIdx, string Name, int Count)> _teams = new();
    private PlayerData _selectedPlayer;
    private (int TeamIdx, string Name, int Count)? _selectedTeam;

    public TransferShockPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadYourRoster();
        LoadTeams();
    }

    private void LoadYourRoster()
    {
        var players = _editor.GetPlayersByTeam(_userTeamIndex);
        YourRosterBox.ItemsSource = players;
        YourRosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({players.Count} players)";
        if (players.Count == 0)
            StatusText.Text = "No players found. " + _editor.DiagnosticInfo();
    }

    private void LoadTeams()
    {
        _teams.Clear();
        for (var ti = 0; ti < 256; ti++)
        {
            if (ti == _userTeamIndex) continue;
            var players = _editor.GetPlayersByTeam(ti);
            if (players.Count == 0) continue;
            _teams.Add((ti, _editor.GetTeamName(ti), players.Count));
        }

        RivalTeamBox.ItemsSource = _teams.Select(t => $"{t.Name} ({t.Count} players)").ToList();
        if (_teams.Count == 0)
            StatusText.Text = "No rival teams found. " + _editor.DiagnosticInfo();
    }

    private void YourRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = YourRosterBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void RivalTeam_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = RivalTeamBox.SelectedIndex;
        _selectedTeam = idx >= 0 && idx < _teams.Count ? _teams[idx] : null;
        UpdateReady();
    }

    private void UpdateReady()
    {
        if (_selectedPlayer != null && _selectedTeam != null)
        {
            TransferButton.IsEnabled = true;
            StatusText.Text = $"Send {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR) to {_selectedTeam.Value.Name}?";
        }
        else
        {
            TransferButton.IsEnabled = false;
        }
    }

    private void TransferButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null || _selectedTeam == null) return;

        var player = _selectedPlayer;
        var teamIdx = _selectedTeam.Value.TeamIdx;

        var result = MessageBox.Show(
            $"Send {player.Name} ({player.Position}, {player.OverallRating} OVR) to {_selectedTeam.Value.Name}?\n" +
            "They will leave your team immediately (penalty).",
            "Confirm Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.StealPlayer(player.RecordIndex, teamIdx))
        {
            // Every roster sits at the 85 cap, so a plain move is rejected. Release the
            // rival's lowest-rated player to free agency to make room, then move.
            var rivalPlayers = _editor.GetPlayersByTeam(teamIdx);
            var lowest = rivalPlayers.OrderBy(p => p.OverallRating).FirstOrDefault();
            if (lowest == null || !_editor.CutPlayer(lowest.RecordIndex))
            {
                StatusText.Text = $"Transfer failed — could not make room on {_selectedTeam.Value.Name}.";
                return;
            }
            if (!_editor.StealPlayer(player.RecordIndex, teamIdx))
            {
                StatusText.Text = "Transfer failed — field detection may be incorrect";
                return;
            }
            StatusText.Text = $"TRANSFERRED: {player.Name} now plays for {_selectedTeam.Value.Name}! " +
                              $"Their lowest-rated player ({lowest.Name}, {lowest.OverallRating} OVR) was released to make room. " +
                              "Save the dynasty file to write the change.";
            TransferButton.IsEnabled = false;
            LoadYourRoster();
            return;
        }

        StatusText.Text = $"TRANSFERRED: {player.Name} now plays for {_selectedTeam.Value.Name}! Save the dynasty file to write the change.";
        TransferButton.IsEnabled = false;
        LoadYourRoster();
    }
}
