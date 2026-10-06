// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DeZ4pAndroidManager.Services;
using DeZ4pAndroidManager.ViewModels;

namespace DeZ4pAndroidManager;

public partial class MainWindow : Window
{
    private MainViewModel? _subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;

        // React to language change - flip FlowDirection for RTL languages
        LocalizationService.LanguageChanged += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                FlowDirection = LocalizationService.IsRtl
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
            });
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        bool isLight = ThemeService.IsSystemLightTheme();
        int caption = isLight
            ? unchecked((int)0x00FAF5F5)
            : unchecked((int)0x00151A1A);
        WindowChromeService.ApplyTitleBarTheme(this, isDark: !isLight, captionColorHex: caption);

        // Apply initial RTL
        FlowDirection = LocalizationService.IsRtl
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        Animations.FadeInFromLeft(SidebarBorder, 0, 240);
        Animations.FadeInFromBottom(SidebarHeader, 100, 220);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Animations.FadeIn(ContentHost, 60, 240);
        }), DispatcherPriority.Loaded);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel != null)
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribedViewModel = e.NewValue as MainViewModel;

        if (_subscribedViewModel != null)
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentView))
        {
            Animations.FadeIn(ContentHost, 0, 180);
        }
    }
}