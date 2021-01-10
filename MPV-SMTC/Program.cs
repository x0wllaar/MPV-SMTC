using System;
using System.Threading.Tasks;
using CommandLine;
using Serilog;

namespace MPVSMTC
{
    public class Options
    {
        [Option('w', "showwindow", Required = false, HelpText = "Hide command line window on start", Default = false)]
        public bool ShowWindow { get; set; }
        
        [Option('p', "pipename", Required = false, HelpText = "Name of the IPC named pipe", Default = @"mpvsocket")]
        public string PipeName { get; set; }

        [Option('f', "usefilemetadata", Required = false, HelpText = "Hide command line window on start", Default = false)]
        public bool UseFileMetadata { get; set; }

        [Option("logfile", Required = false, HelpText = "File to save log to", Default = null)]
        public string LogFile { get; set; }

        [Option('v', "loglevel", Required = false, HelpText = "Log verbosity level", 
            Default = (int)Serilog.Events.LogEventLevel.Information)]
        public int LogLevel { get; set; }

    }

    class Program
    {

        static void ExitSuccessMPVDisconnect() {
            Log.Information("MPV Disconnected");
            Environment.Exit(0);
        }

        static async Task Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed<Options>((options) => {

                if (options.ShowWindow)
                {
                    ConsoleWindowManager.UnhideWindow();
                }
                else
                {
                    ConsoleWindowManager.HideWindow();
                }

                var log = new LoggerConfiguration();
                
                if (options.LogFile is not null)
                {
                    log = log.WriteTo.File(options.LogFile);
                }
                else
                {
                    log = log.WriteTo.Console();
                }

                var log_level = new Serilog.Core.LoggingLevelSwitch((Serilog.Events.LogEventLevel)options.LogLevel);
                log = log.MinimumLevel.ControlledBy(log_level);

                Log.Logger = log.CreateLogger();

                var conn = new MPVSMTCConnector(options.PipeName, ExitSuccessMPVDisconnect, options.UseFileMetadata);
                conn.Start();
            });
            
            //Infinite wait loop
            while (true)
            {
                await Task.Delay(1000);
            }
        }
    }
}
