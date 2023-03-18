using ByteSizeLib;
using FlightLibrary;
using Microsoft.Extensions.Logging;
using RabbitMQ.Stream.Client;
using SlugEnt;
using SlugEnt.MQStreamProcessor;


namespace FlightOps;

public class FlightInfoEngine : Engine
{
    public static readonly  string TaskName_FlightCreation = "Create Flight";
    private static readonly string REDIS_FLIGHT_NUMBER     = "FlightInfo.FlightNumber";

    private string            _streamFlightInfoName;
    private IMqStreamProducer _flightInfoProducer;
    private string            _appName = "FlightInfoService";

    private ulong _currentFlightId = 0;



#region "BasicEngineStuff"

    public FlightInfoEngine(ILogger<FlightInfoEngine> logger, IServiceProvider serviceProvider) : base(logger, serviceProvider)
    {
        _streamFlightInfoName = FlightConstants.STREAM_FLIGHT_INFO;

        // Set Redis cache expiration to 10 days - We are using it as a database...
        _redisCacheExpireTimeSpan = TimeSpan.FromDays(10);

        GetNextFlightNumberFromRedis();


        _flightInfoProducer = _mqStreamEngine.GetProducer(_streamFlightInfoName, _appName);


        // Setup Scheduled Tasks for the Engine
        _internalTaskScheduler.AddTask(new InternalScheduledTask(TaskName_FlightCreation, CreateScheduledFlight, TimeSpan.FromSeconds(10)));
    }


    /// <summary>
    /// Performs initialization logic for the FlightInfo Engine
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync() { await base.InitializeAsync(); }



    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        // Create the stream if it does not exist.
        _flightInfoProducer.SetStreamLimits(ByteSize.FromMegaBytes(100), ByteSize.FromMegaBytes(10), TimeSpan.FromHours(4));

        await base.StartEngineAsync();
    }


    internal IMqStreamProducer FlightInfoProducer
    {
        get { return _flightInfoProducer; }
    }


    /// <summary>
    ///  The current Flight Number
    /// </summary>
    public ulong CurrentFlightNumber
    {
        get { return _currentFlightId; }
    }


    public ulong CreatedFlightSuccess { get; private set; }
    public ulong CreatedFlightError { get; private set; }


    /// <summary>
    /// Number of Flights created during this run
    /// </summary>
    public ulong Stat_FlightsCreatedCount { get; private set; }


    public async Task<ulong> GetNextFlightNumberFromRedis()
    {
        try
        {
            ulong value = 0;
            if (await _redisClient.Db0.ExistsAsync(REDIS_FLIGHT_NUMBER))
                value = await _redisClient.Db0.GetAsync<ulong>(REDIS_FLIGHT_NUMBER);

            _currentFlightId = value;
            _currentFlightId++;
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to retrieve Flight Number from Redis DB.  Error: {ex.Message}", ex);
            return 0;
        }
    }


    /// <summary>
    /// Increases the next flight number and stores in redis.
    /// </summary>
    /// <returns></returns>
    public async Task<ulong> SetNextFlightNumber()
    {
        _currentFlightId++;
        await _redisClient.Db0.AddAsync<ulong>(REDIS_FLIGHT_NUMBER, _currentFlightId);
        return _currentFlightId;
    }

#endregion


    /// <summary>
    /// Sets how often flights are created.
    /// </summary>
    /// <param name="seconds"></param>
    public void SetFlightCreationInterval(int seconds)
    {
        InternalScheduledTask t = _internalTaskScheduler.GetTask(TaskName_FlightCreation);
        if (t == null)
        {
            Console.WriteLine("Unable to find the Flight Creation Task Name.  Flight Creation Interval remains at current value");
            return;
        }

        t.RunInterval = TimeSpan.FromSeconds(seconds);
    }


    public void DeleteStream() { _flightInfoProducer.DeleteStreamFromRabbitMQ(); }


    /// <summary>
    /// Adds a scheduled flight.
    /// </summary>
    /// <param name="internalScheduledTask"></param>
    /// <returns></returns>
    private async Task<EnumInternalTaskReturn> CreateScheduledFlight(InternalScheduledTask internalScheduledTask)
    {
        // If circuit Breaker still tripped, then return without running task.
        if (CheckCircuitBreaker())
        {
            return EnumInternalTaskReturn.NotRun;
        }

        // Create a flight
        ulong  fltnum = await SetNextFlightNumber();
        Flight flight = new Flight(fltnum);

        Message message = _flightInfoProducer.CreateMessage(flight);

        message.AddApplicationProperty(FlightConstants.MQ_EVENT_CATEGORY, EnumMessageEvents.FlightInfo);
        message.AddApplicationProperty(FlightConstants.MQ_EVENT_NAME, FlightConstants.MQEN_FlightCreated);
        message.AddApplicationProperty(FlightConstants.MQ_EVENT_ID, flight.FlightId);

        bool success = await _flightInfoProducer.SendMessageAsync(message);
        if (success)
            CreatedFlightSuccess++;
        else
            CreatedFlightError++;

        return EnumInternalTaskReturn.Success;
    }
}