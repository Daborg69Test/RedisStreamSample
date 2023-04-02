using FlightLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SlugEnt;
using SlugEnt.SLRStreamProcessing;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;


namespace FlightOps;

public class FlightInfoEngine : Engine
{
    public static readonly  string TaskName_FlightCreation = "Create Flight";
    private static readonly string REDIS_FLIGHT_NUMBER     = "FlightInfo.FlightNumber";

    private string _streamFlightInfoName;

    private SLRStream _flightInfoStream;

    private string _appName = "FlightInfoService";

    private ulong                      _currentFlightId = 0;
    private RedisConfiguration         _redisNonStreamConfigation;
    private RedisConnectionPoolManager _redisConnectionManager;

    private IConfiguration _configuration;


#region "BasicEngineStuff"

    public FlightInfoEngine(ILogger<FlightInfoEngine> logger, IConfiguration configuration, IServiceProvider serviceProvider) :
        base(logger, configuration, serviceProvider)
    {
        _streamFlightInfoName = FlightConstants.STREAM_FLIGHT_INFO;
    }


    /// <summary>
    /// Performs initialization logic for the FlightInfo Engine
    /// </summary>
    /// <returns></returns>
    public async Task InitializeAsync(RedisConfiguration redisStreamConfiguration, RedisConfiguration redisNonStreamConfiguration)
    {
        try
        {
            // In this scenario this is not necessary as we are using the same Redis DB backend, but it might be the case this is not true.
            _redisNonStreamConfigation = redisNonStreamConfiguration;

            await base.InitializeAsync(redisStreamConfiguration);

            SLRStreamConfig config = new()
            {
                StreamName = FlightConstants.STREAM_FLIGHT_INFO, ApplicationName = "FlightInfos", StreamType = EnumSLRStreamTypes.ProducerOnly,
            };
            _flightInfoStream = await _slrStreamEngine.GetSLRStreamAsync(config);


            // Setup the non-Streaming Redis Connection.
            _redisConnectionPoolManager = new(redisStreamConfiguration);

            // Get the starting Flight Number
            await GetNextFlightNumberFromRedis();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during FlightInfo Engine Initialization.  {ex.Message}");
        }
    }



    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        await base.StartEngineAsync();

        // Setup Scheduled Tasks for the Engine
        _internalTaskScheduler.AddTask(new InternalScheduledTask(TaskName_FlightCreation, CreateScheduledFlight, TimeSpan.FromSeconds(5)));
    }



    /// <summary>
    /// Returns the Flight Info Stream Object
    /// </summary>
    internal SLRStream FlightInfoStream
    {
        get { return _flightInfoStream; }
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
    public ulong Statistic_FlightsCreatedCount { get; private set; }


    public async Task<ulong> GetNextFlightNumberFromRedis()
    {
        try
        {
            ulong value = 0;
            if (await _redisClient.Db1.Database.KeyExistsAsync(REDIS_FLIGHT_NUMBER))
                value = await _redisClient.Db1.GetAsync<ulong>(REDIS_FLIGHT_NUMBER);

            _currentFlightId = value;
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
    /// <param name="reset">If true then reset value to initial</param>
    /// <returns></returns>
    public async Task<ulong> SetNextFlightNumber(bool shouldReset = false)
    {
        if (shouldReset)
            _currentFlightId = 0;
        else
            _currentFlightId++;

        await _redisClient.Db1.AddAsync<ulong>(REDIS_FLIGHT_NUMBER, _currentFlightId);
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



    /// <summary>
    /// Deletes the Stream
    /// </summary>
    /// <returns></returns>
    public async Task DeleteStreamAsync() { _flightInfoStream.DeleteStream(); }


    //TODO Remove this - Put in the Base Engine Object


    /// <summary>
    /// This is only needed for this sample. It deletes the stream and then resets the flight number
    /// </summary>
    /// <returns></returns>
    public async Task Reset()
    {
        // First Delete the Stream:
        await DeleteStreamAsync();

        // Reset the Flight Number
        SetNextFlightNumber(true);
    }



    /// <summary>
    /// Adds a scheduled flight.
    /// </summary>
    /// <param name="internalScheduledTask"></param>
    /// <returns></returns>
    private async Task<EnumInternalTaskReturn> CreateScheduledFlight(InternalScheduledTask internalScheduledTask)
    {
        // Create a flight
        ulong  fltnum = await SetNextFlightNumber();
        Flight flight = new Flight(fltnum);

        SLRMessage message = SLRMessage.CreateMessage<Flight>(flight);

        ;
        message.AddProperty(MSG_EVENT_CATEGORY, EventCategories.Flights);
        message.AddProperty(MSG_EVENT_NAME, FlightConstants.MQEN_FlightCreated);
        message.AddProperty(MSG_EVENT_ID, flight.FlightId);

        Statistic_FlightsCreatedCount += 1;
        await _flightInfoStream.SendMessageAsync(message);

/*        if (success)
            CreatedFlightSuccess++;
        else
            CreatedFlightError++;
*/
        return EnumInternalTaskReturn.Success;
    }
}