using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PinkSlipsTool;
using PinkSlipsTool.Models;

namespace PinkSlipsTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private readonly StarCalculator _calculator = new();
    private readonly PerkManager _perks = new();
    private DynastyFile _dynasty;
    private StarCalculation _lastCalc;

    public List<PerkDef> AvailablePerks => PerkManager.DefaultPerks;

    private int _starsEarned;
    public int StarsEarned
    {
        get => _starsEarned;
        set { _starsEarned = value; OnPropertyChanged(); OnPropertyChanged(nameof(StarsString)); }
    }

    public string StarsString => _starsEarned switch
    {
        10 => "⭐⭐⭐⭐⭐ PERFECT!",
        >= 7 => new string('⭐', _starsEarned) + " Amazing!",
        >= 4 => new string('⭐', _starsEarned),
        _ => new string('⭐', Math.Max(0, _starsEarned))
    };

    private string _statusText = "Enter your game stats and calculate stars!";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private void SetStatus(string text, bool success = false)
    {
        StatusText = text;
        StatusIsSuccess = success;
    }

    private bool _statusIsSuccess;
    public bool StatusIsSuccess
    {
        get => _statusIsSuccess;
        set { _statusIsSuccess = value; OnPropertyChanged(); }
    }

    public event EventHandler<string> SaveCompleted;

    private bool _isSaving;
    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            _isSaving = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _saveButtonText = "💾 Save";
    public string SaveButtonText
    {
        get => _saveButtonText;
        set { _saveButtonText = value; OnPropertyChanged(); }
    }

    private bool _hasData;
    public bool HasData
    {
        get => _hasData;
        set { _hasData = value; OnPropertyChanged(); }
    }

    private bool _isPerfectGame;
    public bool IsPerfectGame
    {
        get => _isPerfectGame;
        set { _isPerfectGame = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowWheelIcon)); }
    }

    public bool ShowWheelIcon => HasData && (IsPerfectGame || StarsEarned > 0);

    private string _conditionsText = "";
    public string ConditionsText
    {
        get => _conditionsText;
        set { _conditionsText = value; OnPropertyChanged(); }
    }

    // Dynasty file state
    private bool _isDynastyLoaded;
    public bool IsDynastyLoaded
    {
        get => _isDynastyLoaded;
        set { _isDynastyLoaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEditFile)); }
    }

    private bool _isBackedUp;
    public bool IsBackedUp
    {
        get => _isBackedUp;
        set { _isBackedUp = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanRestoreBackup)); OnPropertyChanged(nameof(FileStatus)); }
    }

    private string _dynastyFileName;
    public string DynastyFileName
    {
        get => _dynastyFileName;
        set { _dynastyFileName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileStatus)); }
    }

    private int _tableCount;
    public int TableCount
    {
        get => _tableCount;
        set { _tableCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileStatus)); }
    }

    public string FileStatus
    {
        get
        {
            var status = DynastyFileName ?? "No file loaded";
            if (IsBackedUp) status += "  |  Backup saved";
            if (IsDynastyLoaded && TableCount > 0) status += $"  |  {TableCount} tables";
            return status;
        }
    }

    public bool CanEditFile => IsDynastyLoaded;
    public bool CanRestoreBackup => IsBackedUp;

    // Manual stat input properties
    private int _yourScore;
    public int YourScore { get => _yourScore; set { _yourScore = value; OnPropertyChanged(); } }

    private int _opponentScore;
    public int OpponentScore { get => _opponentScore; set { _opponentScore = value; OnPropertyChanged(); } }

    private int _turnoverDiff;
    public int TurnoverDiff { get => _turnoverDiff; set { _turnoverDiff = value; OnPropertyChanged(); } }

    private int _passYards;
    public int PassYards { get => _passYards; set { _passYards = value; OnPropertyChanged(); } }

    private int _passTDs;
    public int PassTDs { get => _passTDs; set { _passTDs = value; OnPropertyChanged(); } }

    private int _rushYards;
    public int RushYards { get => _rushYards; set { _rushYards = value; OnPropertyChanged(); } }

    private int _rushTDs;
    public int RushTDs { get => _rushTDs; set { _rushTDs = value; OnPropertyChanged(); } }

    private int _recYards;
    public int RecYards { get => _recYards; set { _recYards = value; OnPropertyChanged(); } }

    private int _recTDs;
    public int RecTDs { get => _recTDs; set { _recTDs = value; OnPropertyChanged(); } }

    private int _sacks;
    public int Sacks { get => _sacks; set { _sacks = value; OnPropertyChanged(); } }

    private int _ints;
    public int Ints { get => _ints; set { _ints = value; OnPropertyChanged(); } }

    private int _defTDs;
    public int DefTDs { get => _defTDs; set { _defTDs = value; OnPropertyChanged(); } }

    private int _stTDs;
    public int StTDs { get => _stTDs; set { _stTDs = value; OnPropertyChanged(); } }

    public ICommand CalculateStarsCommand { get; }
    public ICommand OpenWheelCommand { get; }
    public ICommand SpendStarsCommand { get; }

    // File management commands
    public ICommand LoadDynastyCommand { get; }
    public ICommand SaveDynastyCommand { get; }
    public ICommand RestoreBackupCommand { get; }

    public MainViewModel()
    {
        CalculateStarsCommand = new RelayCommand(CalculateStars, () => true);
        OpenWheelCommand = new RelayCommand(OpenWheel, () => ShowWheelIcon);
        SpendStarsCommand = new RelayCommand<PerkDef>(ApplyPerk, p => _perks.CanAfford(p) && _perks.CanApply(p));
        LoadDynastyCommand = new RelayCommand(LoadDynasty, () => true);
        SaveDynastyCommand = new RelayCommand(SaveDynasty, () => IsDynastyLoaded && !IsSaving);
        RestoreBackupCommand = new RelayCommand(RestoreDynastyBackup, () => IsBackedUp);
    }

    public void CalculateStars()
    {
        _lastCalc = _calculator.Calculate(
            YourScore, OpponentScore, TurnoverDiff,
            PassYards, PassTDs, RushYards, RushTDs,
            RecYards, RecTDs, Sacks, Ints, DefTDs, StTDs);

        StarsEarned = _lastCalc.TotalStars;
        IsPerfectGame = _lastCalc.PerfectGame;
        _perks.StarsAvailable = StarsEarned;
        ConditionsText = string.Join("\n", _lastCalc.ConditionsMet.Select(c => $"  • {c}"));
        HasData = true;

        if (IsPerfectGame)
            SetStatus($"PERFECT GAME! {StarsEarned} stars! Wheel spin earned!");
        else
            SetStatus($"Game complete: {StarsEarned} stars earned");

        OnPropertyChanged(nameof(ShowWheelIcon));
    }

    public void OpenWheel()
    {
        var popup = new PinkSlipsWheelPopup();
        popup.Owner = Application.Current.MainWindow;
        if (popup.ShowDialog() == true && popup.SelectedPerk != null)
            ApplyWheelPerk(popup.SelectedPerk);
    }

    private void ApplyWheelPerk(PerkDef perk)
    {
        _perks.PerksApplied.Add(perk.Name);

        if (perk.Name == "Steal Player" && _dynasty != null)
        {
            var popup = new StealPlayerPopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }
        else if (perk.Name == "Dev Upgrade" && _dynasty != null)
        {
            var popup = new DevUpgradePopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }
        else if (perk.Name == "Drug Test" && _dynasty != null)
        {
            var popup = new InjuryPopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }

        SetStatus($"Wheel reward: {perk.Name}");
    }

    public void ApplyPerk(PerkDef perk)
    {
        if (!_perks.ApplyPerk(perk)) return;

        if (perk.Name == "Steal Player" && _dynasty != null)
        {
            var popup = new StealPlayerPopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }
        else if (perk.Name == "Dev Upgrade" && _dynasty != null)
        {
            var popup = new DevUpgradePopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }
        else if (perk.Name == "Drug Test" && _dynasty != null)
        {
            var popup = new InjuryPopup(_dynasty);
            popup.Owner = Application.Current.MainWindow;
            popup.ShowDialog();
        }

        SetStatus($"Applied: {perk.Name} ({perk.StarCost} stars)");
        OnPropertyChanged(nameof(StarsEarned));
    }

    // Dynasty file operations
    public void LoadDynasty()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Dynasty File",
                Filter = "Dynasty files|*.*|All files|*.*",
                InitialDirectory = @"C:\Users\Ninja\Documents\EA SPORTS College Football 27\saves"
            };

            if (dialog.ShowDialog() != true) return;

            SetStatus($"Loading dynasty file: {System.IO.Path.GetFileName(dialog.FileName)}...");
            _dynasty = DynastyFile.Load(dialog.FileName);
            _dynasty.LoadedPath = dialog.FileName;

            // Auto-backup
            _dynasty.CreateBackup();
            IsBackedUp = true;
            DynastyFileName = System.IO.Path.GetFileName(dialog.FileName);
            TableCount = _dynasty.Tables.Count;
            IsDynastyLoaded = true;
            _perks.DynastyFile = _dynasty;

            SetStatus($"Loaded: {DynastyFileName} ({TableCount} tables)  |  Backup created");
            OnPropertyChanged(nameof(FileStatus));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
            MessageBox.Show(ex.ToString(), "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async void SaveDynasty()
    {
        if (_dynasty == null || _dynasty.LoadedPath == null || IsSaving) return;
        IsSaving = true;
        SaveButtonText = "Saving...";
        try
        {
            await Task.Run(() => _dynasty.Save());
            SetStatus($"Saved: {DynastyFileName}  |  Backup: {System.IO.Path.GetFileName(_dynasty.BackupPath)}", success: true);
            SaveButtonText = "✓ Saved!";
            OnPropertyChanged(nameof(FileStatus));
            SaveCompleted?.Invoke(this, $"Saved: {DynastyFileName}");
            await Task.Delay(1800);
        }
        catch (Exception ex)
        {
            SetStatus($"Save error: {ex.Message}");
            SaveButtonText = "✗ Error";
            await Task.Delay(1800);
        }
        finally
        {
            SaveButtonText = "💾 Save";
            IsSaving = false;
        }
    }

    public void RestoreDynastyBackup()
    {
        if (_dynasty?.LoadedPath == null) return;

        var backups = _dynasty.FindBackups();
        if (backups.Count == 0)
        {
            SetStatus("No backups found for this file");
            return;
        }

        var popup = new RestoreBackupPopup(backups);
        popup.Owner = Application.Current.MainWindow;
        if (popup.ShowDialog() != true || popup.SelectedBackupPath == null) return;

        var result = MessageBox.Show(
            $"Restore from backup?\n{System.IO.Path.GetFileName(popup.SelectedBackupPath)}\n\nThis will reload the file from this backup, losing any unsaved changes.",
            "Restore Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _dynasty.RestoreFrom(popup.SelectedBackupPath);
            _dynasty.LoadedPath = _dynasty.BackupPath.Replace(".bak", "");
            OnPropertyChanged(nameof(FileStatus));
            SetStatus($"Backup loaded: {System.IO.Path.GetFileName(popup.SelectedBackupPath)}. Use 💾 Save to write it out.");
        }
        catch (Exception ex)
        {
            SetStatus($"Restore error: {ex.Message}");
        }
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object parameter) => _execute();
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool> _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
    public void Execute(object parameter) => _execute((T)parameter);
}
