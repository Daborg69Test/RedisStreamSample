using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using SlugEnt.MQStreamProcessor;
using Spectre.Console;

namespace FlightOps;

public class MainMenu
{
    private readonly ILogger           _logger;
    private          IServiceProvider  _serviceProvider;
    private          bool              _started;
    private          string            _streamName = "";
    private          IMqStreamProducer _producer   = null;

    private DisplayStats    _displayStats;
    private PassengerEngine _passengerEngine;


    public MainMenu(ILogger<MainMenu> logger, IServiceProvider serviceProvider)
    {
        _logger          = logger;
        _serviceProvider = serviceProvider;
    }



    internal async Task Start()
    {
        bool keepProcessing = true;

        Display();
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
        string engineStatus = _started == true ? "Running" : "Stopped";
        AnsiConsole.WriteLine($" Engine is currently {engineStatus}");
        AnsiConsole.WriteLine();
        Console.WriteLine(" ( S ) StartAsync / Stop Producing Flights");
        Console.WriteLine();
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
                        _passengerEngine = _serviceProvider.GetService<PassengerEngine>();
                        try
                        {
                            await _passengerEngine.StartEngineAsync();

                            //ProcessingLoop();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.ToString());
                        }

                        //                        _displayStats = new DisplayStats(_passengerEngine.Stats);
                    }
                    else if (_started)
                    {
                        // Stop the engine
                        await _passengerEngine.StopEngineAsync();

                        // TODO Dispose it in future.
                        _passengerEngine = null;
                    }

                    _started = !_started;
                    break;

                case ConsoleKey.X:
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