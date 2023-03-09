using Microsoft.Extensions.Logging;
using MQSample_Common;
using SlugEnt.StreamProcessor;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace FlightOps;

public class MainMenu
{
    private readonly ILogger           _logger;
    private          IServiceProvider  _serviceProvider;
    private          bool              _started;
    private          string            _streamName = "";
    private          IMqStreamProducer _producer   = null;

    private FlightOperationsEngine _flightOperationsEngine;
    private DisplayStats           _displayStats;


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

            //Display();
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



    /// <summary>
    /// Processes user input.  Returns True, if we should keep processing.  False if user choose to exit.
    /// </summary>
    /// <returns></returns>
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
                        _flightOperationsEngine = _serviceProvider.GetService<FlightOperationsEngine>();
                        try
                        {
                            await _flightOperationsEngine.StartEngineAsync();

                            //ProcessingLoop();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, ex.ToString());
                        }

//                        _displayStats = new DisplayStats(_flightOperationsEngine.Stats);
                    }
                    else if (_started)
                    {
                        // Stop the engine
                        await _flightOperationsEngine.StopEngineAsync();

                        // TODO Dispose it in future.
                        _flightOperationsEngine = null;
                    }

                    _started = !_started;
                    break;

                case ConsoleKey.X:
                    await _flightOperationsEngine.StopEngineAsync();
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
        _flightOperationsEngine.StartEngineAsync();

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