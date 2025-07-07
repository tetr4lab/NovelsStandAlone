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
    public MainWindow () {
        InitializeComponent ();
        var connectionString = $"";
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
        // PetaPoco with MySqlConnector
        serviceCollection.AddScoped (_ => (Database) new MySqlDatabase (connectionString, "MySqlConnector"));
        // HTTP Client
        serviceCollection.AddHttpClient ();
        // DataSet
        serviceCollection.AddScoped<NovelsDataSet> ();
        Resources.Add ("services", serviceCollection.BuildServiceProvider ());
    }
}