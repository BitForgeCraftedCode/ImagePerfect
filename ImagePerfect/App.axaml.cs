using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using ImagePerfect.Services;
using ImagePerfect.ViewModels;
using ImagePerfect.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Threading.Tasks;

namespace ImagePerfect
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            try
            {
                // Register all the services needed for the application to run
                ServiceCollection collection = new ServiceCollection();
                collection.AddCommonServices();
                // Creates a ServiceProvider containing services from the provided IServiceCollection
                var services = collection.BuildServiceProvider();

                var vm = services.GetRequiredService<MainWindowViewModel>();
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    MainWindow mainWindow = new MainWindow
                    {
                        DataContext = vm,
                    };

                    Globals.MainWindow = mainWindow; //store global reference for message dialogs
                    desktop.MainWindow = mainWindow;

                    Log.Information("Main window initialized");
                }
            }
            catch (Exception ex) 
            {
                Log.Fatal(ex, "Fatal error during framework initialization");
                //Rethrows the same exception without altering it
                //Lets the exception bubble up to higher - level handlers(like Program.Main) or crash the app if uncaught
                throw;
            }
            
            base.OnFrameworkInitializationCompleted();
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain exception");
            }
            else
            {
                Log.Fatal("Unhandled AppDomain exception (non-exception object)");
            }
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unobserved task exception");
            e.SetObserved();
        }

    }
}