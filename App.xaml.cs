using System;
using System.Windows;

namespace TrashAll;
 
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && e.Args[0] == "--capture-screenshots")
        {
            try
            {
                await Services.ScreenshotGeneratorService.GenerateScreenshotsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro na captura de screenshots: {ex}");
            }
            finally
            {
                Shutdown();
            }
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

