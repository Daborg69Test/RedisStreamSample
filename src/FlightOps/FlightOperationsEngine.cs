using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ByteSizeLib;
using FlightLibrary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.Reliable;
using SlugEnt.StreamProcessor;

namespace FlightOps;

public class FlightOperationsEngine
{
    private          string            _streamFlightOpsName;
    private          string            _streamFlightInfoName;
    private readonly ILogger           _logger;
    private          IServiceProvider  _serviceProvider;
    private          IMQStreamEngine   _mqStreamEngine;
    private          IMqStreamProducer _flightOpsProducer;
    private          IMqStreamProducer _flightInfoProducer;
    private          string            _appName                  = "FlightOps";
    private          bool              _circuitBreakerTripped    = false;
    private          int               _circuitBreakerTimeOut    = 1;
    private          int               _circuitBreakerMaxTimeout = 60;
    private          int               _sleepInterval            = 5000;
    private          long              _messageId                = 0;
    private          Thread            _processingThread;
    private          bool              _stopProcessing = false;


    public FlightOperationsEngine(ILogger<FlightOperationsEngine> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;

        _serviceProvider      = serviceProvider;
        _streamFlightOpsName  = FlightConstants.STREAM_FLIGHT_OPS;
        _streamFlightInfoName = FlightConstants.STREAM_FLIGHT_INFO;
    }


    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        StreamSystemConfig config = HelperFunctions.GetStreamSystemConfig();
        _mqStreamEngine                    = _serviceProvider.GetService<IMQStreamEngine>();
        _mqStreamEngine.StreamSystemConfig = config;


        _flightOpsProducer  = _mqStreamEngine.GetProducer(_streamFlightOpsName, _appName);
        _flightInfoProducer = _mqStreamEngine.GetProducer(_streamFlightInfoName, _appName);

        // Create the stream if it does not exist.
        _flightOpsProducer.SetStreamLimits(ByteSize.FromMegaBytes(100), ByteSize.FromMegaBytes(10), TimeSpan.FromHours(4));
        _flightInfoProducer.SetStreamLimits(ByteSize.FromMegaBytes(100), ByteSize.FromMegaBytes(10), TimeSpan.FromHours(4));
        await _mqStreamEngine.StartAllStreamsAsync();

        _processingThread = new Thread(new ThreadStart(Process));
        _processingThread.Start();
    }



    /// <summary>
    /// Stops all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StopEngineAsync()
    {
        await _mqStreamEngine.StopAllAsync();
        _stopProcessing = true;
    }



    private void Process()
    {
        while (!_stopProcessing)
        {
            Task processLoop = ProcessTasks();
            Task.WaitAll();

            Thread.Sleep(_sleepInterval);
        }
    }



    public async Task ProcessTasks()
    {
        // First check to ensure the circuit breaker is not tripped.  If it is, then we see see if it has been reset, if not wait an increasing amount of time...
        if (_circuitBreakerTripped)
        {
            // See if any of the producers circuit breakers are still tripped
            bool isStillTripped = false;
            foreach (KeyValuePair<string, IMqStreamProducer> mqStreamProducer in _mqStreamEngine.StreamProducersDictionary)
            {
                isStillTripped = mqStreamProducer.Value.CircuitBreakerTripped == true ? true : isStillTripped;
            }

            if (isStillTripped)
            {
                _circuitBreakerTimeOut += 2;
                _circuitBreakerTimeOut =  _circuitBreakerTimeOut > _circuitBreakerMaxTimeout ? _circuitBreakerMaxTimeout : _circuitBreakerTimeOut;
            }
            else
            {
                _circuitBreakerTimeOut = 1;
                _circuitBreakerTripped = false;
            }
        }


        Message message = _flightOpsProducer.CreateMessage("hello to you");
        message.Properties.ReplyTo = "scott";
        message.ApplicationProperties.Add("Type", "test");
        message.ApplicationProperties.Add("Id", _messageId);


//        _statsList[0].CreatedMessages++;

        await _flightOpsProducer.SendMessageAsync(message);


/*
        if (!_producer.CircuitBreakerTripped)
            await _producer.SendMessageAsync(message);
        else
        {
            bool keepTrying = true;
            while (keepTrying)
            {
                if (_stopping)
                    return;

                if (_producer.CircuitBreakerTripped)
                    Thread.Sleep(2000);
                else
                    await _producer.SendMessageAsync(message);
            }
        }

        if (_stopping)
            break;
        
    }
        */
    }
}