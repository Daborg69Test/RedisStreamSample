using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis.Extensions.Core.Configuration;

namespace FlightOps;

public class MainMenu
{
    private readonly ILogger          _logger;
    private          IServiceProvider _serviceProvider;
    private          bool             _started;


    private RedisConfiguration     _redisConfiguration;
    private DisplayFlightInfoStats _displayStats;
    private FlightInfoEngine       _flightInfoEngine;


    public MainMenu(ILogger<MainMenu> logger, IServiceProvider serviceProvider)
    {
        _logger           = logger;
        _serviceProvider  = serviceProvider;
        _flightInfoEngine = _serviceProvider.GetService<FlightInfoEngine>();
        if (_flightInfoEngine == null)
        {
            _logger.LogError($"Failed to load the FlightInfoEngine from ServiceProvider");
            return;
        }


        _displayStats = new DisplayFlightInfoStats(_flightInfoEngine);
    }



    internal async Task Start()
    {
        bool keepProcessing = true;

        // Initialize the Engines
        _redisConfiguration = new RedisConfiguration
        {
            Password = "redispw", Hosts = new[] { new RedisHost { Host = "localhost", Port = 6379 } }, ConnectTimeout = 700, PoolSize = 1,
        };


        await _flightInfoEngine.InitializeAsync(_redisConfiguration, _redisConfiguration);

        while (keepProcessing)
        {
            if (Console.KeyAvailable)
            {
                keepProcessing = await MainMenuUserInput();
            }
            else
                Thread.Sleep(1000);


            Display();
        }
    }



    internal async Task Display()
    {
        if (_displayStats != null)
            _displayStats.Refresh();
    }


    internal async Task<bool> MainMenuUserInput()
    {
        if (Console.KeyAvailable)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey();

            switch (keyInfo.Key)
            {
                case ConsoleKey.S:
                    if (!_started)
                    {
                        // Start the engine
                        try
                        {
                            await _flightInfoEngine.StartEngineAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.ToString());
                        }
                    }
                    else if (_started)
                    {
                        // Stop the engine
                        await _flightInfoEngine.StopEngineAsync();
                    }

                    _started = !_started;
                    break;

                case ConsoleKey.I:
                    Console.WriteLine("Enter the number of minutes between flight creations");
                    string interval = Console.ReadLine();
                    if (int.TryParse(interval, out int secondInterval))
                    {
                        _flightInfoEngine.SetFlightCreationInterval(secondInterval * 60);
                        Console.WriteLine($"Flights will now be created every {interval} minutes");
                    }
                    else
                        Console.WriteLine("Must enter a numeric integer value");

                    break;

                case ConsoleKey.D:
                    _flightInfoEngine.DeleteStreamAsync();
                    Console.WriteLine($"Deleted Stream for Engine FlightInfo");
                    Thread.Sleep(5000);
                    break;

                case ConsoleKey.R:
                    _flightInfoEngine.Reset();
                    return false;

                    break;

                case ConsoleKey.X:
                    if (_flightInfoEngine != null)
                        await _flightInfoEngine.StopEngineAsync();
                    return false;
            }
        }


        return true;
    }



    /// <summary>
    /// The thread the engine runs on.
    /// </summary>
    internal void ProcessingLoop()
    {
        _flightInfoEngine.StartEngineAsync();

        bool continueProcessing = true;
        while (continueProcessing)
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                if (keyInfo.Key == ConsoleKey.X)
                {
                    return;
                }
            }


            // Processing logic


            // Update Display
        }
    }
}