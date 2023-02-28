using Microsoft.Extensions.Logging;
using SlugEnt.StreamProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MQSample_Common;
using Spectre.Console;

namespace ConsumerApp;

public class MainMenu
{
    private readonly ILogger _logger;
    private IServiceProvider _serviceProvider;
    private bool _started;
    private string _streamName = "";
    private IMqStreamProducer _producer = null;

    private FlightConsumerEngine _flightConsumerEngine;
    private DisplayStats _displayStats;


    public MainMenu(ILogger<MainMenu> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
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
                keepProcessing = await ProcessUserInput();
                
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
        if (_displayStats != null ) _displayStats.Refresh();
    }



    /// <summary>
    /// Processes user input.  Returns True, if we should keep processing.  False if user choose to exit.
    /// </summary>
    /// <returns></returns>
    internal async Task<bool> ProcessUserInput()
    {
        if (Console.KeyAvailable)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey();

            switch (keyInfo.Key)
            {
                case ConsoleKey.S:
                    if (!_started)
                    {
                        // StartAsync the engine
                        _flightConsumerEngine = (FlightConsumerEngine)_serviceProvider.GetService(typeof(FlightConsumerEngine));
                        await _flightConsumerEngine.StartEngine();
                        _displayStats = new DisplayStats(_flightConsumerEngine.Stats);
                    }
                    else if (_started)
                    {
                        // Stop the engine
                        await _flightConsumerEngine.StopEngine();
                        // TODO Dispose it in future.
                        _flightConsumerEngine = null;
                    }
                    _started = !_started;
                    break;

                case ConsoleKey.X:
                    return false;
            }

        }

        return true;
    }
}

