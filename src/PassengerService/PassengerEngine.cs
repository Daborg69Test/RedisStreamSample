using Bogus;
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
    private static readonly string REDIS_RECV_FLIGHT_NUMBER     = "PassengerSvc.LastCreatedFlightNumber";
    private static readonly string REDIS_PSVC_LAST_PASSENGER_ID = "PassengerSvc.LastPassenger.Id";

    private string            _streamPassengerName;
    private string            _streamFlightInfoName;
    private IMqStreamProducer _passengerProducer;
    private IMqStreamConsumer _passengerConsumer;
    private IMqStreamConsumer _flightInfoConsumer;
    private string            _appName = "Passenger.App";

    // For generating fake passenger data
    private Faker _faker = new Faker();


    // Keeping Track of Flights that need to be booked to passengers.
    private Dictionary<ulong, Flight> _flightsNeedingBooked = new();

    // Flights that have completed booking
    private Dictionary<ulong, Flight> _bookedFlights = new();



    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    public PassengerEngine(ILogger<PassengerEngine> logger, IServiceProvider serviceProvider) : base(logger, serviceProvider)
    {
        _streamPassengerName   = FlightConstants.STREAM_PASSENGER;
        _streamFlightInfoName  = FlightConstants.STREAM_FLIGHT_INFO;
        _internalTaskScheduler = new InternalTaskScheduler();


        // Set Redis expiration timeout to 10 days.
        _redisCacheExpireTimeSpan = TimeSpan.FromDays(10);


        _passengerProducer  = _mqStreamEngine.GetProducer(_streamPassengerName, _appName);
        _passengerConsumer  = _mqStreamEngine.GetConsumer(_streamPassengerName, _appName, ReceivePassengerMessages);
        _flightInfoConsumer = _mqStreamEngine.GetConsumer(_streamFlightInfoName, _appName, ReceiveFlightInfoMessages);

        // Setup Scheduled Tasks
        _internalTaskScheduler.AddTask(new InternalScheduledTask("Add Passenger", AddPassengersToFlights, TimeSpan.FromSeconds(10)));
    }



    /// <summary>
    /// Performs initialization logic for the FlightInfo Engine
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await GetInitialDataFromRedis();
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


    /// <summary>
    /// Resets Passenger Service to Defaults
    /// </summary>
    /// <returns></returns>
    public async Task Reset()
    {
        await _passengerProducer.DeleteStreamFromRabbitMQ();
        SetNextPassengerNumber(true);
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
    /// The Current Passenger Id number (The last one created).
    /// </summary>
    public ulong CurrentPassengerIdNumber { get; private set; }


    /// <summary>
    /// The last received Flight Number
    /// </summary>
    public ulong FlightNumberLast { get; protected set; }


    /// <summary>
    /// Number of times Flight number was out of sequence
    /// </summary>
    public ulong FlightNumberOutOfSync { get; protected set; }

    public ulong PassengerCreatedSuccess { get; protected set; }
    public ulong PassengerCreatedError { get; protected set; }

    /// <summary>
    /// Number of Messages Consumed (Received) from the FlightInfo Stream
    /// </summary>
    public ulong FlightInfoMessagesConsumed
    {
        get { return _flightInfoConsumer != null ? _flightInfoConsumer.MessageCounter : 0; }
    }

    public IMqStreamConsumer PassengerConsumer
    {
        get { return _passengerConsumer; }
    }

    public IMqStreamProducer PassengerProducer
    {
        get { return _passengerProducer; }
    }


    /// <summary>
    /// Retrieves start up values from the last time this ran.
    /// </summary>
    /// <returns></returns>
    public async Task GetInitialDataFromRedis()
    {
        string valueRetrieving = "";
        try
        {
            // Get Last Received Flight Number
            valueRetrieving = REDIS_RECV_FLIGHT_NUMBER;
            if (await _redisClient.Db0.ExistsAsync(valueRetrieving))
                FlightNumberLast = await _redisClient.Db0.GetAsync<ulong>(valueRetrieving);

            valueRetrieving = REDIS_PSVC_LAST_PASSENGER_ID;
            if (await _redisClient.Db0.ExistsAsync(valueRetrieving))
                CurrentPassengerIdNumber = await _redisClient.Db0.GetAsync<ulong>(valueRetrieving);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to retrieve Flight Number from Redis DB.  Error: {ex.Message}", ex);
        }
    }



    /// <summary>
    /// Increases the next flight number and stores in redis.
    /// </summary>
    /// <param name="shouldReset">CAUTION: This should practically never need to be done in production systems - If true, the PassengerNumber will reset to initial value.</param>
    /// <returns></returns>
    public async Task<ulong> SetNextPassengerNumber(bool shouldReset = false)
    {
        if (shouldReset)
            CurrentPassengerIdNumber = 0;
        else
            CurrentPassengerIdNumber++;

        await _redisClient.Db0.AddAsync<ulong>(REDIS_PSVC_LAST_PASSENGER_ID, CurrentPassengerIdNumber);
        return CurrentPassengerIdNumber;
    }



    /// <summary>
    /// Processes FlightInfo Stream messages
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
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

                    // Store Flight Number into a number of places
                    if ((FlightNumberLast + 1) != flight.FlightId)
                    {
                        _logger.LogError($"Received Flight Number out of Sequence.  Expecting: {FlightNumberLast + 1} but actually was: {flight.FlightId}.");
                        FlightNumberOutOfSync++;
                    }


                    FlightNumberLast = flight.FlightId;
                    await _redisClient.Db0.AddAsync<ulong>(REDIS_RECV_FLIGHT_NUMBER, flight.FlightId);
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
    private async Task<EnumInternalTaskReturn> AddPassengersToFlights(InternalScheduledTask internalScheduledTask)
    {
        // If circuit Breaker still tripped, then return without running task.
        if (CheckCircuitBreaker())
        {
            return EnumInternalTaskReturn.NotRunMissingResources;
        }


        if (_flightsNeedingBooked.Count == 0)
            return EnumInternalTaskReturn.NotRunNoData;

        foreach (KeyValuePair<ulong, Flight> flight in _flightsNeedingBooked)
        {
            // Determine how many passengers to put on plane.  Our planes hold a max of 10 passengers
            int numPassengers = Random.Shared.Next(1, 10);
            for (int i = 0; i < numPassengers; i++)
            {
                // Create Passenger
                string    passName          = _faker.Person.FullName;
                ulong     passengerIdNumber = await SetNextPassengerNumber();
                Passenger passenger         = new(passName, passengerIdNumber);

                // Create Message
                Message message = _passengerProducer.CreateMessage(passenger);
                message.AddApplicationProperty(FlightConstants.MQ_EVENT_CATEGORY, EnumMessageEvents.Passenger);
                message.AddApplicationProperty(FlightConstants.MQ_EVENT_NAME, FlightConstants.MQEN_PassengerBooked);
                message.AddApplicationProperty(FlightConstants.MQ_EVENT_ID, passenger.Id);

                bool success = await _passengerProducer.SendMessageAsync(message);
                if (success)
                    PassengerCreatedSuccess++;
                else
                    PassengerCreatedError++;
            }
        }


        return EnumInternalTaskReturn.Success;
    }
}