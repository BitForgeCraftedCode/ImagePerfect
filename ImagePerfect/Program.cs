using Avalonia;
using ReactiveUI.Avalonia;
using Serilog;
using Serilog.Exceptions;
using System;
using System.IO;

namespace ImagePerfect
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static int Main(string[] args)
        {     
            ConfigureSerilog();
       
            try
            {
                Log.Information("ImagePerfect starting");
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                Log.Information("ImagePerfect shutting down normally");
                return 0;
            }
            catch (Exception ex) 
            {
                Log.Fatal(ex, "Fatal error during application startup");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
            
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .With(new X11PlatformOptions
                {
                    UseDBusFilePicker = false // to disable FreeDesktop file picker -- open file picker at location Ubuntu
                })
                .WithInterFont()
                .LogToTrace()
                .UseReactiveUI();

        private static void ConfigureSerilog()
        {
            // Create logs folder next to executable
            string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithExceptionDetails()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("App", "ImagePerfect")
                .WriteTo.File(
                    Path.Combine(logDir, "imageperfect-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true
                )
                .CreateLogger();
        }
    }
}
