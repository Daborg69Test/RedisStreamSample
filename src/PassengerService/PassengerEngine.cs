using Bogus;
using FlightLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SlugEnt;
using SlugEnt.SLRStreamProcessing;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Configuration;
using System.Collections.Concurrent;


namespace FlightOps;

public class PassengerEngine : Engine
{
    private static readonly string REDIS_RECV_FLIGHT_NUMBER     = "PassengerSvc.LastReceivedFlightNumber";
    private static readonly string REDIS_PSVC_LAST_PASSENGER_ID = "PassengerSvc.LastPassenger.Id";

    private string    _streamPassengerName;
    private string    _streamFlightInfoName;
    private string    _appName = "Passenger.App";
    private SLRStream _passengerStream;
    private SLRStream _flightInfoStream;


    // For generating fake passenger data
    private Faker _faker = new Faker();


    // Keeping Track of Flights that need to be booked to passengers.
    private ConcurrentQueue<Flight> _flightsNeedingBookedQueue;

    // Flights that have completed booking
    private Dictionary<ulong, Flight> _bookedFlights = new();



    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    public PassengerEngine(ILogger<PassengerEngine> logger, IConfiguration configuration, IServiceProvider serviceProvider) :
        base(logger, configuration, serviceProvider)
    {
        _streamPassengerName       = FlightConstants.STREAM_PASSENGER;
        _streamFlightInfoName      = FlightConstants.STREAM_FLIGHT_INFO;
        _flightsNeedingBookedQueue = new();
    }



    /// <summary>
    /// Performs initialization logic for the FlightInfo Engine
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync(RedisConfiguration redisStreamConfiguration)
    {
        await base.InitializeAsync(redisStreamConfiguration);
        await GetInitialDataFromRedis();


        // Setup FlightInfo Stream
        SLRStreamConfig config = new()
        {
            StreamName = FlightConstants.STREAM_FLIGHT_INFO, ApplicationName = _appName, StreamType = EnumSLRStreamTypes.ConsumerGroupOnly,
        };
        _flightInfoStream = await _slrStreamEngine.GetSLRStreamAsync(config);
        if (_flightInfoStream == null)
            throw new ApplicationException($"Did not get a good {config.StreamName} stream object.");


        // Setup Passenger Stream
        // Setup FlightInfo Stream
        SLRStreamConfig passengerConfig = new()
        {
            StreamName = FlightConstants.STREAM_PASSENGER, ApplicationName = _appName, StreamType = EnumSLRStreamTypes.ProducerAndConsumerGroup,
        };
        _passengerStream = await _slrStreamEngine.GetSLRStreamAsync(passengerConfig);
        if (_passengerStream == null)
            throw new ApplicationException($"Did not get a good {config.StreamName} stream object.");


        // Setup the non-Streaming Redis Connection.
        _redisConnectionPoolManager = new(redisStreamConfiguration);
    }



    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        await base.StartEngineAsync();

        // Setup Scheduled Tasks
        _internalTaskScheduler.AddTask(new InternalScheduledTask("Add Passenger", AddPassengersToFlights, TimeSpan.FromSeconds(10)));
        _internalTaskScheduler.AddTask(new InternalScheduledTask("Receive FlightInfo Messages", ReceiveFlightInfoMessages, TimeSpan.FromSeconds(5)));
    }


    /// <summary>
    /// Resets Passenger Service to Defaults
    /// </summary>
    /// <returns></returns>
    public async Task Reset()
    {
        _passengerStream.DeleteStream();
        SetNextPassengerNumber(true);
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


    /// <summary>
    /// Number of Passengers Created
    /// </summary>
    public ulong StatisticPassengersCreated { get; protected set; }


    /// <summary>
    /// Number of Flight Info Messages processed 
    /// </summary>
    public ulong StatisticFlightMessagesProcessed { get; protected set; }



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
            if (await _redisClient.Db1.ExistsAsync(valueRetrieving))
                FlightNumberLast = await _redisClient.Db1.GetAsync<ulong>(valueRetrieving);

            valueRetrieving = REDIS_PSVC_LAST_PASSENGER_ID;
            if (await _redisClient.Db1.ExistsAsync(valueRetrieving))
                CurrentPassengerIdNumber = await _redisClient.Db1.GetAsync<ulong>(valueRetrieving);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving a key [{valueRetrieving}] that should exist in redis.  Error - {ex.Message}", ex);
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

        await _redisClient.Db1.AddAsync<ulong>(REDIS_PSVC_LAST_PASSENGER_ID, CurrentPassengerIdNumber);
        return CurrentPassengerIdNumber;
    }



    /// <summary>
    /// Processes FlightInfo Stream messages
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task<EnumInternalTaskReturn> ReceiveFlightInfoMessages(InternalScheduledTask internalScheduledTask)
    {
        try
        {
            // Get Flights from FlightInfo
            StreamEntry[] flightEntries = await _flightInfoStream.ReadStreamGroupAsync();
            foreach (StreamEntry flightEntry in flightEntries)
            {
                SLRMessage message = new(flightEntry);
                ProcessFlightMessage(message);
                StatisticFlightMessagesProcessed++;
            }

            return EnumInternalTaskReturn.Success;
        }
        catch (Exception e)
        {
            return EnumInternalTaskReturn.Failed;
        }
    }



    private async Task<bool> ProcessFlightMessage(SLRMessage message)
    {
        try
        {
            (bool isValid, string eventCategory) = ValidateEventCategory(message, _flightInfoStream.StreamName);
            if (!isValid)
                return true; // Even though invalid we did process the message, so return true.

            // This is only for this sample... To make sure I have not messed something up.
            if (eventCategory != EventCategories.Flights)
            {
                string msgInfo = message.PrintMessageInfo();
                _logger.LogError($"Received an eventCategory that does not appear to be for FlightInfo message.  {msgInfo}");

                // Return True, even though technically it is an issue,  This should be a testing thing only.
                return true;
            }


            (isValid, string eventName) = ValidateEventName(message, _flightInfoStream.StreamName);
            if (!isValid)
                return true; // Even though invalid, we did process message.


            // Process the message
            switch (eventName)
            {
                case FlightConstants.MQEN_FlightCreated:
                    Flight flight = message.GetMessageObject<Flight>();
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

                    _flightsNeedingBookedQueue.Enqueue(flight);

                    // Store Last received flt number
                    await _redisClient.Db1.AddAsync<ulong>(REDIS_RECV_FLIGHT_NUMBER, flight.FlightId);
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
            return false;
        }

        return true;
    }



    /// <summary>
    /// Adds a scheduled flight.
    /// </summary>
    /// <param name="internalScheduledTask"></param>
    /// <returns></returns>
    private async Task<EnumInternalTaskReturn> AddPassengersToFlights(InternalScheduledTask internalScheduledTask)
    {
        if (_flightsNeedingBookedQueue.IsEmpty)
            return EnumInternalTaskReturn.NotRunNoData;

        while (_flightsNeedingBookedQueue.TryDequeue(out Flight flight))
        {
            // Determine how many passengers to put on plane.  Our planes hold a max of 10 passengers
            int numPassengers = Random.Shared.Next(1, 10);
            for (int i = 0; i < numPassengers; i++)
            {
                // Create Passenger
                string    passName          = _faker.Person.FullName;
                ulong     passengerIdNumber = await SetNextPassengerNumber();
                Passenger passenger         = new(passName, passengerIdNumber);
                passenger.FlightIds.Add(flight.FlightId);

                // Create Message
                SLRMessage message = SLRMessage.CreateMessage<Passenger>(passenger);
                message.AddProperty(MSG_EVENT_CATEGORY, EventCategories.Passengers);
                message.AddProperty(MSG_EVENT_NAME, FlightConstants.MQEN_PassengerBooked);
                message.AddProperty(MSG_EVENT_ID, passenger.Id);

                await _passengerStream.SendMessageAsync(message);
                StatisticPassengersCreated++;
            }
        }

        return EnumInternalTaskReturn.Success;
    }
}