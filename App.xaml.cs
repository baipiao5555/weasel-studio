using System.Windows;
using WeaselStudio.ViewModels;

namespace WeaselStudio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "程序错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var userDirIndex = Array.IndexOf(e.Args, "--user-dir");
        if (userDirIndex >= 0 && userDirIndex + 1 < e.Args.Length)
        {
            Environment.SetEnvironmentVariable(
                "WEASEL_STUDIO_USER_DIR",
                e.Args[userDirIndex + 1]);
        }

        var window = new MainWindow
        {
            DataContext = new MainViewModel()
        };

        MainWindow = window;
        window.Show();
    }
}
