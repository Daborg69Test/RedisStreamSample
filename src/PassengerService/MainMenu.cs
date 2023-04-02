using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using SlugEnt.MQStreamProcessor;
using Spectre.Console;
using StackExchange.Redis.Extensions.Core.Configuration;

namespace FlightOps;

public class MainMenu
{
    private readonly ILogger          _logger;
    private          IServiceProvider _serviceProvider;
    private          bool             _started;
    private          string           _streamName = "";

    private RedisConfiguration    _redisConfiguration;
    private DisplayPassengerStats _displayStats;
    private PassengerEngine       _passengerEngine;



    public MainMenu(ILogger<MainMenu> logger, IServiceProvider serviceProvider)
    {
        _logger          = logger;
        _serviceProvider = serviceProvider;
        _passengerEngine = _serviceProvider.GetService<PassengerEngine>();
        if (_passengerEngine == null)
        {
            _logger.LogError($"Failed to load the PassengerEngine from ServiceProvider");
            return;
        }

        _displayStats = new DisplayPassengerStats(_passengerEngine);
    }



    internal async Task Start()
    {
        bool keepProcessing = true;

        // Initialize the Engines
        _redisConfiguration = new RedisConfiguration
        {
            Password = "redispw", Hosts = new[] { new RedisHost { Host = "localhost", Port = 6379 } }, ConnectTimeout = 700, PoolSize = 1,
        };


        await _passengerEngine.InitializeAsync(_redisConfiguration);


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



    internal void Display()
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
                            await _passengerEngine.StartEngineAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.ToString());
                        }
                    }
                    else if (_started)
                    {
                        // Stop the engine
                        await _passengerEngine.StopEngineAsync();
                    }

                    _started = !_started;
                    break;

                case ConsoleKey.R:
                    await _passengerEngine.Reset();
                    return false;

                    break;

                case ConsoleKey.X:
                    if (_passengerEngine != null)
                        await _passengerEngine.StopEngineAsync();
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
        _passengerEngine.StartEngineAsync();

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