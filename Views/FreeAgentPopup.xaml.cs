using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class FreeAgentPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private List<PlayerData> _freeAgents;
    private List<PlayerData> _yourPlayers;
    private PlayerData _selectedPlayer;   // FA to sign
    private PlayerData _cutPlayer;        // your player to cut when full

    public FreeAgentPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadFreeAgents();
        LoadYourRoster();
    }

    private void LoadFreeAgents()
    {
        if (_userTeamIndex < 0)
        {
            HeaderText.Text = "Could not find your team in Coach table";
            StatusText.Text = _editor.DiagnosticInfo();
            return;
        }

        _freeAgents = _editor.GetPlayersByTeam(DynastyEditor.FreeAgentTeamIndex);
        FaListBox.ItemsSource = _freeAgents;
        HeaderText.Text = $"FREE AGENTS ({_freeAgents.Count} players)";
        if (_freeAgents.Count == 0)
            StatusText.Text = "No free agents found. " + _editor.DiagnosticInfo();
    }

    private void LoadYourRoster()
    {
        _yourPlayers = _editor.GetPlayersByTeam(_userTeamIndex);
        YourRosterBox.ItemsSource = _yourPlayers;
        YourRosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({_yourPlayers.Count}/85)";
    }

    private void FaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = FaListBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void YourRoster_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _cutPlayer = YourRosterBox.SelectedItem as PlayerData;
        UpdateReady();
    }

    private void FaSortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(FaSortBox, FaListBox, _freeAgents);

    private void YourSortBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        PlayerSorter.Apply(YourSortBox, YourRosterBox, _yourPlayers);

    private bool YourRosterFull => _yourPlayers != null && _yourPlayers.Count >= 85;

    private void UpdateReady()
    {
        if (_selectedPlayer == null)
        {
            SignButton.IsEnabled = false;
            SignButton.Content = "SIGN FREE AGENT";
            return;
        }

        if (YourRosterFull)
        {
            if (_cutPlayer == null)
            {
                SignButton.IsEnabled = false;
                SignButton.Content = "SIGN & CUT";
                StatusText.Text = $"Your roster is FULL (85). Pick a player on the right to cut, then sign {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR).";
            }
            else
            {
                SignButton.IsEnabled = true;
                SignButton.Content = "SIGN & CUT";
                StatusText.Text = $"Sign {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR) and cut {_cutPlayer.Name} ({_cutPlayer.OverallRating} OVR)?";
            }
            return;
        }

        SignButton.IsEnabled = true;
        SignButton.Content = "SIGN FREE AGENT";
        StatusText.Text = $"Ready to sign: {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR) — your roster has room ({_yourPlayers.Count}/85).";
    }

    private void SignButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;

        var result = MessageBox.Show(
            YourRosterFull
                ? $"Sign {player.Name} ({player.Position}, {player.OverallRating} OVR)\n" +
                  $"and cut {_cutPlayer?.Name} ({_cutPlayer?.OverallRating} OVR) to make room?"
                : $"Sign {player.Name} ({player.Position}, {player.OverallRating} OVR) to your team?",
            "Confirm Sign", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (YourRosterFull)
        {
            if (_cutPlayer == null || !_editor.CutPlayer(_cutPlayer.RecordIndex))
            {
                StatusText.Text = "Cut failed — field detection may be incorrect";
                return;
            }
        }

        if (!_editor.StealPlayer(player.RecordIndex, _userTeamIndex))
        {
            StatusText.Text = "Sign failed — field detection may be incorrect";
            return;
        }

        StatusText.Text = YourRosterFull
            ? $"SIGNED: {player.Name} added to your team ({_cutPlayer.Name} released). Save the dynasty file to write the change."
            : $"SIGNED: {player.Name} added to your team! Save the dynasty file to write the change.";

        SignButton.IsEnabled = false;
        FaListBox.SelectedItem = null;
        YourRosterBox.SelectedItem = null;
        LoadFreeAgents();
        LoadYourRoster();
    }
}
