using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PinkSlipsTool.Models;
using PinkSlipsTool.ViewModels;

namespace PinkSlipsTool;

public partial class MainWindow : Window
{
    private MainViewModel _vm;
    private int _toastToken;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.SaveCompleted += OnSaveCompleted;
    }

    private void WheelPanel_PerkApplied(PerkDef perk)
    {
        _vm?.ApplyWheelPerk(perk);
    }

    private void OnSaveCompleted(object sender, string message)
    {
        ShowToast(message);
    }

    private async void ShowToast(string message)
    {
        var token = ++_toastToken;
        ToastText.Text = message;
        ToastSlide.Y = -60;
        SaveToast.Opacity = 0;
        SaveToast.Visibility = Visibility.Visible;

        ToastSlide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-60, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        SaveToast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));

        await Task.Delay(2200);
        if (token != _toastToken) return;

        ToastSlide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, -60, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
        SaveToast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)));

        await Task.Delay(350);
        if (token != _toastToken) return;
        SaveToast.Visibility = Visibility.Collapsed;
    }
}
