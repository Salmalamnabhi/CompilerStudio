using System.Windows;

namespace CompilerStudio;

public partial class App : Application
{
    public App()
    {
        // Show unexpected UI errors instead of closing the window silently
        DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.Message, "Compiler Studio – unexpected error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}
