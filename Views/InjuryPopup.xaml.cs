using System.Windows;
using System.Windows.Controls;
using PinkSlipsTool.Models;

namespace PinkSlipsTool;

public partial class InjuryPopup : Window
{
    public enum Mode { Injure, Heal, TeamIllness, Academic }

    private readonly DynastyEditor _editor;
    private readonly int _userTeamIndex;
    private readonly Mode _mode;
    private readonly Random _rng = new();
    private PlayerData _selectedPlayer;

    public InjuryPopup(DynastyFile dynasty, Mode mode = Mode.Injure)
    {
        InitializeComponent();
        _mode = mode;
        _editor = new DynastyEditor(dynasty);
        _userTeamIndex = _editor.FindUserTeamIndex();

        Title = mode switch
        {
            Mode.Heal => "Injury Heal",
            Mode.TeamIllness => "Team Illness",
            Mode.Academic => "Academic Ineligibility",
            _ => "Drug Test — Injure a Player",
        };

        if (mode != Mode.Injure)
        {
            InstructionText.Text = mode switch
            {
                Mode.Heal => "Pick an INJURED player to heal instantly:",
                Mode.TeamIllness => "A mystery illness sweeps your locker room — a random player gets injured (penalty):",
                Mode.Academic => "Pick one of YOUR players to sit the game (academic ineligibility):",
                _ => InstructionText.Text,
            };
            InjureButton.Content = mode switch
            {
                Mode.Heal => "HEAL PLAYER",
                Mode.TeamIllness => "STRIKE RANDOM PLAYER",
                Mode.Academic => "SIT PLAYER",
                _ => InjureButton.Content,
            };
        }

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
        if (_mode == Mode.Heal) players = players.Where(p => p.IsInjured).ToList();
        PlayerListBox.ItemsSource = players;
        RosterHeader.Text = $"YOUR ROSTER — {_editor.GetTeamName(_userTeamIndex)} ({players.Count} players)";
        PlayerCountText.Text = $"{players.Count} players";
        if (players.Count == 0)
        {
            StatusText.Text = _mode == Mode.Heal
                ? "No injured players — everyone is healthy!"
                : "No players found. " + _editor.DiagnosticInfo();
            return;
        }

        if (_mode == Mode.TeamIllness)
        {
            // Random victim pre-selected; the user just confirms the strike.
            PlayerListBox.SelectedIndex = _rng.Next(players.Count);
        }
        else
        {
            StatusText.Text = _mode == Mode.Heal
                ? "Select an injured player to heal"
                : "Select one of your players to injure";
        }
    }

    private void PlayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPlayer = PlayerListBox.SelectedItem as PlayerData;
        if (_selectedPlayer == null) return;

        if (_mode == Mode.Heal)
        {
            if (!_selectedPlayer.IsInjured)
            {
                InjureButton.IsEnabled = false;
                StatusText.Text = $"{_selectedPlayer.Name} is healthy — pick someone injured";
            }
            else
            {
                InjureButton.IsEnabled = true;
                StatusText.Text = $"Ready to heal: {_selectedPlayer.Name} ({_selectedPlayer.Position}, {_selectedPlayer.OverallRating} OVR)";
            }
            return;
        }

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

    private void InjureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlayer == null) return;

        var player = _selectedPlayer;

        if (_mode == Mode.Heal)
        {
            var healResult = MessageBox.Show(
                $"Heal {player.Name} ({player.Position}, {player.OverallRating} OVR) instantly?",
                "Confirm Heal", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (healResult != MessageBoxResult.Yes) return;

            if (!_editor.HealInjury(player.RecordIndex))
            {
                StatusText.Text = "Heal failed — field detection may be incorrect";
                return;
            }

            LoadRoster();
            InjureButton.IsEnabled = false;
            StatusText.Text = $"HEALED: {player.Name} is healthy again! Save the dynasty file to write the change.";
            return;
        }

        var verb = _mode == Mode.Academic ? "Sit" : "Give";
        var result = MessageBox.Show(
            $"{verb} {player.Name} ({player.Position}, {player.OverallRating} OVR) an injury?\n" +
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
