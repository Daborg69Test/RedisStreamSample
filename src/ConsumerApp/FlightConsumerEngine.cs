using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using RabbitMQ.Stream.Client;
using SlugEnt.StreamProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsumerApp;

public class FlightConsumerEngine
{
    private string _streamName;
    private readonly ILogger _logger;
    private IServiceProvider _serviceProvider;
    private IFlightConsumer _consumer;

    private string _flightDay;



    public FlightConsumerEngine(ILogger<FlightConsumerEngine> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _streamName = "MQStreamSample.Flights";
    }



    public async Task StartEngine()
    {
        StreamSystemConfig config = HelperFunctions.GetStreamSystemConfig();



        _consumer = _serviceProvider.GetService<IFlightConsumer>();
        _consumer.Initialize(_streamName, "FlightConsumer", config);
        _consumer.SetConsumptionHandler(ConsumeSlow);
        _consumer.EventCheckPointSaved += OnEventCheckPointSaved;

        Stats = new Stats(_streamName);

        await _consumer.StartAsync();
    }




    public async Task StopEngine()
    {
        //await _consumer.Stop();
    }

    public Stats Stats { get; set; }



    private void OnEventCheckPointSaved(object sender, MqStreamCheckPointEventArgs e)
    {
        Stats.ConsumeLastCheckpoint = e.CommittedOffset;
    }


    /// <summary>
    /// The Consumer Slow Method
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task<bool> ConsumeSlow(Message message)
    {
        Stats.ConsumedMessages++;
        Stats.ConsumeLastBatchReceived = (string)message.ApplicationProperties[SampleCommon.AP_DAY];
        //        _statsList[1].ConsumeLastCheckpoint = _consumerB_slow.CheckpointLastOffset;
        Stats.ConsumeCurrentAwaitingCheckpoint = _consumer.CheckpointOffsetCounter;
        // Simulate slow
        Thread.Sleep(1500);
        return true;
    }
}

