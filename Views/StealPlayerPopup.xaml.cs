using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class StealPlayerPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private PlayerData _selectedPlayer;
    private PlayerData _swapOutPlayer;
    private List<(int TeamIdx, string Name, int Count)> _teams;
    private List<PlayerData> _yourRoster;

    public StealPlayerPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadYourRoster();
        LoadTeams();
    }

    private void LoadYourRoster()
    {
        _yourRoster = _editor.GetPlayersByTeam(_userTeamIndex);
        YourRosterBox.ItemsSource = _yourRoster;
        YourRosterCountText.Text = $"{_yourRoster.Count} players";
        SwapHintText.Text = _yourRoster.Count >= 85
            ? "Your roster is FULL — pick who gets cut to make room."
            : "Your roster has room — no cut needed.";
    }

    private void LoadTeams()
    {
        _teams = new List<(int, string, int)>();
        var seenTeams = new HashSet<int>();

        // Scan all players to find which teams have players
        for (var ti = 0; ti < 256; ti++)
        {
            var players = _editor.GetPlayersByTeam(ti);
            if (players.Count == 0) continue;
            var name = _editor.GetTeamName(ti);
            var marker = ti == _userTeamIndex ? "  [YOU]" : "";
            _teams.Add((ti, $"{name}{marker}", players.Count));
        }

        TeamListBox.ItemsSource = _teams.Select(t => $"{t.Name} ({t.Count} players)").ToList();
        TeamCountText.Text = $"{_teams.Count} teams with players";
        if (_teams.Count == 0)
            StatusText.Text = "No players found. " + _editor.DiagnosticInfo();
    }

    private void TeamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PlayerListBox.ItemsSource = null;
        StealButton.IsEnabled = false;
        _selectedPlayer = null;

        var idx = TeamListBox.SelectedIndex;
        if (idx < 0 || idx >= _teams.Count) return;

        var (teamIdx, name, _) = _teams[idx];

        if (teamIdx == _userTeamIndex)
        {
            StatusText.Text = "That's your team! Pick an opponent.";
            return;
        }

        var players = _editor.GetPlayersByTeam(teamIdx);
        PlayerListBox.ItemsSource = players;
        RosterHeader.Text = $"ROSTER — {_editor.GetTeamName(teamIdx)} ({players.Count} players)";
        PlayerCountText.Text = $"{players.Count} players";
        StatusText.Text = $"Select a player from {_editor.GetTeamName(teamIdx)} to steal";
    }

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        TransferButton.IsEnabled = _swapOutPlayer != null && _selectedPlayer != null;
        UpdateStealReady();
    }

    private void YourRosterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _swapOutPlayer = YourRosterBox.SelectedItem as PlayerData;
        CutButton.IsEnabled = _swapOutPlayer != null;
        TransferButton.IsEnabled = _swapOutPlayer != null && _selectedPlayer != null;
        UpdateStealReady();
    }

    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_swapOutPlayer == null) return;

        var result = MessageBox.Show(
            $"Cut {_swapOutPlayer.Name} ({_swapOutPlayer.Position}, {_swapOutPlayer.OverallRating} OVR)?\n" +
            "They will be released to free agency.",
            "Confirm Cut", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.CutPlayer(_swapOutPlayer.RecordIndex))
        {
            StatusText.Text = "Cut failed — field detection may be incorrect";
            return;
        }
        StatusText.Text = $"CUT: {_swapOutPlayer.Name} released to free agency.";
        YourRosterBox.SelectedItem = null;
        LoadYourRoster();
    }

    private void TransferButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null || _swapOutPlayer == null) return;

        var result = MessageBox.Show(
            $"Transfer {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)\n" +
            $"to your team in exchange for {_swapOutPlayer.Name} ({_swapOutPlayer.Position}, {_swapOutPlayer.OverallRating} OVR)?\n" +
            "Both rosters stay the same size — no one is cut.",
            "Confirm Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.TransferPlayer(_selectedPlayer.RecordIndex, _swapOutPlayer.RecordIndex))
        {
            StatusText.Text = "Transfer failed — field detection may be incorrect";
            return;
        }
        StatusText.Text = $"TRANSFERRED: {_selectedPlayer.Name} joined your team, {_swapOutPlayer.Name} went to {_editor.GetTeamName(_swapOutPlayer.TeamIndex)}.";
        StealButton.IsEnabled = false;
        TransferButton.IsEnabled = false;
        YourRosterBox.SelectedItem = null;

        // Refresh team list
        LoadYourRoster();
        LoadTeams();
    }

    private void UpdateStealReady()
    {
        if (_selectedPlayer != null)
        {
            StealButton.IsEnabled = true;
            StatusText.Text = $"Ready to steal: {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)" +
                (_yourRoster.Count >= 85 ? " — your roster is full, CUT someone first" : "");
        }
        else
        {
            StealButton.IsEnabled = false;
        }
    }

    private void StealButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var result = MessageBox.Show(
            $"Steal {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)?\n" +
            $"They will be moved to your team permanently.",
            "Confirm Steal", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (!_editor.StealPlayer(_selectedPlayer.RecordIndex, _userTeamIndex))
        {
            StatusText.Text = "Steal failed — field detection may be incorrect";
            return;
        }
        StatusText.Text = $"STOLEN: {_selectedPlayer.Name} added to your team!";
        StealButton.IsEnabled = false;

        // Refresh team list
        LoadYourRoster();
        LoadTeams();
    }
}
