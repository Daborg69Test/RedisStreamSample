using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Stream.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using ProducerApp;
using SlugEnt.StreamProcessor;
using RabbitMQ.Stream.Client.Reliable;
using Spectre.Console;

namespace ProducerApp;


internal class FlightProducerEngine
{
    private string _streamName;
    private readonly ILogger<FlightProducerEngine> _logger;
    private IServiceProvider _serviceProvider;
    private IFlightProducer _producer;

    private string _flightDay;
    


    public FlightProducerEngine(ILogger<FlightProducerEngine> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _streamName = "MQStreamSample.Flights";
    }


    public async Task StartEngine()
    {
        StreamSystemConfig config = HelperFunctions.GetStreamSystemConfig();


        // Build a producer
        _producer = _serviceProvider.GetService<IFlightProducer>();
        _producer.Initialize(_streamName, "FlightProducer", config);
        _producer.SetProducerMethod(ProduceFlights);


        // 2 GB max, 200MB segments, 36 hours
        await _producer.SetStreamLimits(2000, 200, 36);

        _producer.MessageConfirmationError += MessageConfirmationError;
        _producer.MessageConfirmationSuccess += MessageConfirmationSuccess;

        Stats = new Stats(_streamName);

        await _producer.StartAsync();
    }


    public async Task StopEngine()
    {
        await _producer.Stop();
    }

    public Stats Stats { get; set; }


    /// <summary>
    /// The number of messages that should be produced per batch
    /// </summary>
    public short FlightsPerDay{ get; set; } = 6;


    /// <summary>
    /// This is actually number of seconds between "flight day batches"  OR wait between a batch of messages being produced and sent.
    /// </summary>
    public int WaitBetweenDays { get; set; } = 3;



    /// <summary>
    /// Produces Flights
    /// </summary>
    /// <param name="producer"></param>
    /// <returns></returns>
    protected async Task ProduceFlights(FlightProducer producer)
    {
        // Initiate the Batch Number
        _flightDay = "A";
        while (!producer.IsCancelled)
        {
            try
            {
                // Publish the messages
                for (short i = 0; i < FlightsPerDay; i++)
                {
                    string fullBatchId = _flightDay + i;
                    string msg = String.Format($"Id: {i} ");
                    Message message = producer.CreateMessage(msg);

                    string fullBatch = _flightDay.ToString() + i.ToString();
                    message.ApplicationProperties.Add(SampleCommon.AP_DAY, fullBatch);
                    message.Properties.ReplyTo = "scott";

                    Stats.CreatedMessages++;

                    if (!producer.CircuitBreakerTripped)
                        await producer.SendMessage(message);
                    else
                    {
                        bool keepTrying = true;
                        while (keepTrying)
                        {
                            if (producer.IsCancelled) return;
                            if (producer.CircuitBreakerTripped) Thread.Sleep(2000);
                            else await producer.SendMessage(message);
                        }
                    }

                    if (producer.IsCancelled) return;
                }

                _flightDay = HelperFunctions.NextFlightDay(_flightDay);
                //DisplayStats.Refresh();
                Thread.Sleep(WaitBetweenDays * 1000);
            }
            catch (Exception ex) { }
        }
    }



    private void MessageConfirmationError(object sender, MessageConfirmationEventArgs e)
    {
        Stats.FailureMessages++;

        bool success = e.Status == ConfirmationStatus.Confirmed ? true : false;
        string flightDay = (string)e.Message.ApplicationProperties[SampleCommon.AP_DAY];
        string flightDaymethod = HelperFunctions.GetFlightDay(e);

        AnsiConsole.Markup($"[red] Error sending Flight data for {flightDay}[/]");

        
    }


    private void MessageConfirmationSuccess(object sender, MessageConfirmationEventArgs e)
    {
        string flightDay = HelperFunctions.GetFlightDay(e);
        bool success = e.Status == ConfirmationStatus.Confirmed ? true : false;

        Stats.SuccessMessages++;
    }


}
