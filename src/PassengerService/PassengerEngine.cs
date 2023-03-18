using ByteSizeLib;
using FlightLibrary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQSample_Common;
using RabbitMQ.Stream.Client;
using SlugEnt;
using SlugEnt.Locker;
using SlugEnt.MQStreamProcessor;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.Newtonsoft;


namespace FlightOps;

public class PassengerEngine : Engine
{
    private string            _streamPassengerName;
    private string            _streamFlightInfoName;
    private IMqStreamProducer _passengerProducer;
    private IMqStreamConsumer _passengerConsumer;
    private IMqStreamConsumer _flightInfoConsumer;
    private string            _appName = "Passenger.App";



    public PassengerEngine(ILogger<PassengerEngine> logger, IServiceProvider serviceProvider) : base(logger, serviceProvider)
    {
        _streamPassengerName   = FlightConstants.STREAM_PASSENGER;
        _streamFlightInfoName  = FlightConstants.STREAM_FLIGHT_INFO;
        _internalTaskScheduler = new InternalTaskScheduler();


        // Set Redis lock timeout to 10 days. After 10 days 
        _redisCacheExpireTimeSpan = TimeSpan.FromDays(10);

        _passengerProducer  = _mqStreamEngine.GetProducer(_streamPassengerName, _appName);
        _passengerConsumer  = _mqStreamEngine.GetConsumer(_streamPassengerName, _appName, ReceivePassengerMessages);
        _flightInfoConsumer = _mqStreamEngine.GetConsumer(_streamFlightInfoName, _appName, ReceiveFlightInfoMessages);

        // Setup Scheduled Tasks
        _internalTaskScheduler.AddTask(new InternalScheduledTask("Add Flight", AddPassengerToFlight, TimeSpan.FromSeconds(10)));
    }



    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        // Create the stream if it does not exist.
        _passengerProducer.SetStreamLimits(ByteSize.FromMegaBytes(100), ByteSize.FromMegaBytes(10), TimeSpan.FromHours(4));

        await base.StartEngineAsync();
    }



    private async Task ReceivePassengerMessages(Message message)
    {
        try { }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }


    /// <summary>
    /// The last received Flight Number
    /// </summary>
    public ulong FlightNumberLast { get; protected set; }


    private async Task ReceiveFlightInfoMessages(Message message)
    {
        try
        {
            string eventCategory = message.GetApplicationPropertyAsString(FlightConstants.MQ_EVENT_NAME);
            if (eventCategory == String.Empty)
            {
                string msgInfo = message.PrintMessageInfo();
                _logger.LogError($"Received an empty eventCategory for FlightInfo message.  {msgInfo}");
                return;
            }

            // Process the message
            switch (eventCategory)
            {
                case FlightConstants.MQEN_FlightCreated:
                    Flight flight = message.GetObject<Flight>();
                    if (flight == null)
                        _logger.LogError($"Received a FlightInfo Message event name of: {eventCategory}. It did not contain any Flight Information and should have.");
                    Console.WriteLine($"Flight Number {flight.FlightId} was created at: {flight.CreatedDateTime}");
                    FlightNumberLast = flight.FlightId;
                    break;
                default:
                    string msgInfo = message.PrintMessageInfo();
                    _logger.LogError($"Received a FlightInfo Message event name of: {eventCategory}. This is an unknown eventCategory.  Event Info:  {msgInfo}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in receipt of message in ReceiveFlightInfoMessages.  {ex.Message}", ex);
        }

        return;
    }



    /// <summary>
    /// Adds a scheduled flight.
    /// </summary>
    /// <param name="internalScheduledTask"></param>
    /// <returns></returns>
    private async Task<EnumInternalTaskReturn> AddPassengerToFlight(InternalScheduledTask internalScheduledTask)
    {
        // If circuit Breaker still tripped, then return without running task.
        if (CheckCircuitBreaker())
        {
            return EnumInternalTaskReturn.NotRun;
        }

        /*
        Message message = _flightOpsProducer.CreateMessage("hello to you");
        message.Properties.ReplyTo = "scott";
        message.ApplicationProperties.Add("Type", "test");
        message.ApplicationProperties.Add("Id", _messageId);
        bool success = await _flightOpsProducer.SendMessageAsync(message);
        */
        return EnumInternalTaskReturn.Success;
    }
}