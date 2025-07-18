﻿using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Novels.Services;
using PetaPoco;
using Tetr4lab;

namespace Novels;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window {

    /// <summary>DBパス</summary>
    public static string DbPath { get; protected set; } = "";
    /// <summary>DBファイル名</summary>
    protected static readonly string DbFile = "novels.db";
    /// <summary>データパス</summary>
    protected static readonly string DataPath = "Tetr4lab/Novels";

    public MainWindow () {
        InitializeComponent ();
        var folder = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), DataPath);
        if (!Directory.Exists (folder)) {
            Directory.CreateDirectory (folder);
        }
        DbPath = System.IO.Path.Combine (folder, DbFile);
        var connectionString = $"Data Source={DbPath};";
        var serviceCollection = new ServiceCollection ();
        serviceCollection.AddWpfBlazorWebView ();
        serviceCollection.AddBlazorWebViewDeveloperTools ();
        serviceCollection.AddMudServices (config => {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 10000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });
        // UIロック状態
        serviceCollection.AddScoped<IAppLockState, AppLockState> ();
        // アプリモード
        serviceCollection.AddScoped<NovelsAppModeService> ();
        // PetaPoco with SQLite
        serviceCollection.AddScoped (_ => (Database) new SqliteDatabase (connectionString, "SQLite"));
        // HTTP Client
        serviceCollection.AddHttpClient ();
        // DataSet
        serviceCollection.AddScoped<NovelsDataSet> ();
#if DEBUG
        Top = 0;
        Left = 2600;
        Height = 1380;
        Width = 1200;
        WindowStartupLocation = WindowStartupLocation.Manual;
#else
        Height = 1380;
        Width = 1200;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
#endif
        Resources.Add ("services", serviceCollection.BuildServiceProvider ());
    }
}