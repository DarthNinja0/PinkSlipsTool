using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class InjuryPopup : Window
{
    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private PlayerData _selectedPlayer;

    public InjuryPopup(DynastyFile dynasty)
    {
        InitializeComponent();
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();
        LoadRoster();
    }

    private void LoadRoster()
    {
        if (_userTeamIndex < 0)
        {
            StatusText.Text = "Could not find your team (no user coach)";
            return;
        }

        var players = _editor.GetPlayersByTeam(_userTeamIndex);
        PlayerListBox.ItemsSource = players;
        RosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({players.Count} players)";
        PlayerCountText.Text = $"{players.Count} players";
        if (players.Count == 0)
            StatusText.Text = "No players found. " + _editor.DiagnosticInfo();
        else
            StatusText.Text = $"Select one of your players to injure";
    }

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        if (_selectedPlayer != null)
        {
            if (_selectedPlayer.IsInjured)
            {
                InjureButton.IsEnabled = false;
                StatusText.Text = $"{_selectedPlayer.Name} is already injured — pick someone else";
            }
            else
            {
                InjureButton.IsEnabled = true;
                StatusText.Text = $"Ready to injure: {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)";
            }
        }
    }

    private void InjureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;

        var result = MessageBox.Show(
            $"Give {player.Name} ({player.Position}, {player.OverallRating} OVR) an injury?\n" +
            "They'll get a game-ending hand injury — out for your team's NEXT game, back the game after.",
            "Confirm Injury", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var (ok, description) = _editor.ApplyInjury(player.RecordIndex);
        if (!ok)
        {
            StatusText.Text = "Injury failed — field detection may be incorrect";
            return;
        }

        LoadRoster();
        InjureButton.IsEnabled = false;
        StatusText.Text = description == "already"
            ? $"{player.Name} was already injured — nothing changed"
            : $"INJURED: {player.Name} — {description}! Save the dynasty file to write the change.";
    }
}
