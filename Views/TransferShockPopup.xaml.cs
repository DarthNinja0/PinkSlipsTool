using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class TransferShockPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private readonly List<(int TeamIdx, string Name, int Count)> _teams = new();
    private List<PlayerData> _yourPlayers;
    private List<PlayerData> _rivalPlayers = new();
    private PlayerData _selectedPlayer;   // victim (yours)
    private PlayerData _rivalTarget;      // rival player for swap/cut
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
        _yourPlayers = _editor.GetPlayersByTeam(_userTeamIndex);
        YourRosterBox.ItemsSource = _yourPlayers;
        YourRosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({_yourPlayers.Count} players)";
        if (_yourPlayers.Count == 0)
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

    private void LoadRivalRoster(int teamIdx)
    {
        _rivalPlayers = _editor.GetPlayersByTeam(teamIdx);
        RivalRosterBox.ItemsSource = _rivalPlayers;
        RivalRosterHeader.Text = $"RIVAL ROSTER — {_editor.GetTeamName(teamIdx)} ({_rivalPlayers.Count}/85)";
    }

    private void YourRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = YourRosterBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void RivalTeam_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _rivalTarget = null;
        RivalRosterBox.SelectedItem = null;
        SwapButton.IsEnabled = false;
        CutButton.IsEnabled = false;

        var idx = RivalTeamBox.SelectedIndex;
        _selectedTeam = idx >= 0 && idx < _teams.Count ? _teams[idx] : null;
        if (_selectedTeam != null) LoadRivalRoster(_selectedTeam.Value.TeamIdx);
        UpdateReady();
    }

    private void RivalRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _rivalTarget = RivalRosterBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void YourSortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(YourSortBox, YourRosterBox, _yourPlayers);

    private void RivalSortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(RivalSortBox, RivalRosterBox, _rivalPlayers);

    private bool RivalFull => _selectedTeam != null && _selectedTeam.Value.Count >= 85;

    private void UpdateReady()
    {
        var ready = _selectedPlayer != null && _selectedTeam != null;
        if (!ready)
        {
            SendButton.IsEnabled = false;
            SwapButton.IsEnabled = false;
            CutButton.IsEnabled = false;
            return;
        }

        if (RivalFull)
        {
            SendButton.IsEnabled = false;
            if (_rivalTarget != null)
            {
                SwapButton.IsEnabled = true;
                CutButton.IsEnabled = true;
                StatusText.Text = $"{_selectedTeam.Value.Name} is FULL (85). Pick a rival player then SWAP or CUT — {_rivalTarget.Name} ({_rivalTarget.OverallRating} OVR) selected.";
            }
            else
            {
                SwapButton.IsEnabled = false;
                CutButton.IsEnabled = false;
                StatusText.Text = $"{_selectedTeam.Value.Name} is FULL (85). Pick a rival player to SWAP (even) or CUT (release to FA) to make room for {_selectedPlayer.Name}.";
            }
            return;
        }

        SendButton.IsEnabled = true;
        SwapButton.IsEnabled = false;
        CutButton.IsEnabled = false;
        StatusText.Text = $"Send {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR) to {_selectedTeam.Value.Name} — they have room ({_selectedTeam.Value.Count}/85).";
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
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
            StatusText.Text = "Transfer failed — field detection may be incorrect";
            return;
        }

        StatusText.Text = $"TRANSFERRED: {player.Name} now plays for {_selectedTeam.Value.Name}! Save the dynasty file to write the change.";
        AfterMove();
    }

    private void SwapButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null || _rivalTarget == null || _selectedTeam == null) return;

        var player = _selectedPlayer;
        var rival = _rivalTarget;

        var result = MessageBox.Show(
            $"Swap {player.Name} ({player.OverallRating} OVR) to {_selectedTeam.Value.Name}\n" +
            $"in exchange for {rival.Name} ({rival.OverallRating} OVR) coming to your team?",
            "Confirm Swap", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.TransferPlayer(player.RecordIndex, rival.RecordIndex))
        {
            StatusText.Text = "Swap failed — field detection may be incorrect";
            return;
        }

        StatusText.Text = $"SWAPPED: {player.Name} now plays for {_selectedTeam.Value.Name}, {rival.Name} joined your team! Save the dynasty file to write the change.";
        AfterMove();
    }

    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null || _rivalTarget == null || _selectedTeam == null) return;

        var player = _selectedPlayer;
        var rival = _rivalTarget;
        var teamIdx = _selectedTeam.Value.TeamIdx;

        var result = MessageBox.Show(
            $"Send {player.Name} ({player.OverallRating} OVR) to {_selectedTeam.Value.Name}\n" +
            $"and cut {rival.Name} ({rival.OverallRating} OVR) from them to free agency?",
            "Confirm Cut + Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.CutPlayer(rival.RecordIndex))
        {
            StatusText.Text = "Cut failed — field detection may be incorrect";
            return;
        }
        if (!_editor.StealPlayer(player.RecordIndex, teamIdx))
        {
            StatusText.Text = "Transfer failed — field detection may be incorrect";
            return;
        }

        StatusText.Text = $"TRANSFERRED: {player.Name} now plays for {_selectedTeam.Value.Name} ({rival.Name} released). Save the dynasty file to write the change.";
        AfterMove();
    }

    private void AfterMove()
    {
        SendButton.IsEnabled = false;
        SwapButton.IsEnabled = false;
        CutButton.IsEnabled = false;
        YourRosterBox.SelectedItem = null;
        RivalRosterBox.SelectedItem = null;
        LoadYourRoster();
        if (_selectedTeam != null) LoadRivalRoster(_selectedTeam.Value.TeamIdx);
    }
}
