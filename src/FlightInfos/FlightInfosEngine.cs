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

public class FlightInfoEngine
{
    public static readonly  string TaskName_FlightCreation = "Create Flight";
    private static readonly string REDIS_FLIGHT_NUMBER     = "FlightInfo.FlightNumber";

    private          string                _streamFlightInfoName;
    private readonly ILogger               _logger;
    private          IServiceProvider      _serviceProvider;
    private          IMQStreamEngine       _mqStreamEngine;
    private          IMqStreamProducer     _flightInfoProducer;
    private          string                _appName                  = "FlightInfoService";
    private          bool                  _circuitBreakerTripped    = false;
    private          int                   _circuitBreakerTimeOut    = 1;
    private          int                   _circuitBreakerMaxTimeout = 180;
    private          int                   _sleepInterval            = 5000;
    private          long                  _messageId                = 0;
    private          Thread                _processingThread;
    private          bool                  _stopProcessing = false;
    private          InternalTaskScheduler _internalTaskScheduler;
    private          long                  _nextFlightID = 1;

    private RedisLocker                _redisLocker;
    private RedisConnectionPoolManager _redisConnectionPoolManager;
    private RedisClient                _redisClient;


    private TimeSpan _redisCacheExpireTimeSpan;


#region "BasicEngineStuff"

    public FlightInfoEngine(ILogger<FlightInfoEngine> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;

        _serviceProvider       = serviceProvider;
        _streamFlightInfoName  = FlightConstants.STREAM_FLIGHT_INFO;
        _internalTaskScheduler = new InternalTaskScheduler();

        ConfigureRedis();

        _redisLocker = new(_redisClient);

        // Set Redis lock timeout to 10 days. After 10 days 
        _redisCacheExpireTimeSpan = TimeSpan.FromDays(10);
    }



    public void ConfigureRedis()
    {
        RedisConfiguration redisConfig = new()
        {
            Hosts          = new[] { new RedisHost { Host = "podmanc.slug.local", Port = 6379 } },
            Password       = "redis23",
            ConnectTimeout = 2000,
            SyncTimeout    = 2000,
            AllowAdmin     = true // Enable admin mode to allow flushing of the database
        };


        _redisConnectionPoolManager = new RedisConnectionPoolManager(redisConfig);
        NewtonsoftSerializer serializer = new();
        _redisClient = new RedisClient(_redisConnectionPoolManager, serializer, redisConfig);
        GetNextFlightNumberFromRedis();
    }


    public async Task<long> GetNextFlightNumberFromRedis()
    {
        try
        {
            long value = -1;
            if (await _redisClient.Db0.ExistsAsync(REDIS_FLIGHT_NUMBER))
                value = await _redisClient.Db0.GetAsync<long>(REDIS_FLIGHT_NUMBER);

            _nextFlightID = value;
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to retrieve Flight Number from Redis DB.  Error: {ex.Message}", ex);
            return -1;
        }
    }


    /// <summary>
    /// Increases the next flight number and stores in redis.
    /// </summary>
    /// <returns></returns>
    public async Task<long> SetNextFlightNumber()
    {
        _nextFlightID++;
        await _redisClient.Db0.AddAsync<long>(REDIS_FLIGHT_NUMBER, _nextFlightID);
        return _nextFlightID;
    }


    public long NextFlightNumber
    {
        get { return _nextFlightID; }
    }


    /// <summary>
    /// Starts all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        if (_nextFlightID == -1)
        {
            _nextFlightID = 0;
            await SetNextFlightNumber();
        }

        StreamSystemConfig config = HelperFunctions.GetStreamSystemConfig();
        _mqStreamEngine                    = _serviceProvider.GetService<IMQStreamEngine>();
        _mqStreamEngine.StreamSystemConfig = config;


        _flightInfoProducer = _mqStreamEngine.GetProducer(_streamFlightInfoName, _appName);

        // Create the stream if it does not exist.
        _flightInfoProducer.SetStreamLimits(ByteSize.FromMegaBytes(100), ByteSize.FromMegaBytes(10), TimeSpan.FromHours(4));
        await _mqStreamEngine.StartAllStreamsAsync();

        _processingThread = new Thread(new ThreadStart(Process));
        _processingThread.Start();

        // Setup Scheduled Tasks
        _internalTaskScheduler.AddTask(new InternalScheduledTask(TaskName_FlightCreation, CreateScheduledFlight, TimeSpan.FromSeconds(10)));
    }



    /// <summary>
    /// Stops all producers and consumers
    /// </summary>
    /// <returns></returns>
    public async Task StopEngineAsync()
    {
        if (_mqStreamEngine != null)
            await _mqStreamEngine.StopAllAsync();
        _stopProcessing = true;
    }



    private void Process()
    {
        while (!_stopProcessing)
        {
            try
            {
                Task checkTasks = _internalTaskScheduler.CheckTasks();
                Task.WaitAll(checkTasks);

                Thread.Sleep(_sleepInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }
    }



    /// <summary>
    /// Before we produce a message ,we check the circuit breakers to make sure none are tripped.  Returns True if circuit breaker on one or more producers is tripped.
    /// </summary>
    /// <returns></returns>
    public bool CheckCircuitBreaker()
    {
        // First check to ensure the circuit breaker is not tripped.  If it is, then we see if it has been reset, if not wait an increasing amount of time...
        if (_circuitBreakerTripped)
        {
            // See if any of the producers circuit breakers are still tripped
            bool isStillTripped = false;
            foreach (KeyValuePair<string, IMqStreamProducer> mqStreamProducer in _mqStreamEngine.StreamProducersDictionary)
            {
                bool tripped = mqStreamProducer.Value.CircuitBreakerTripped;
                if (tripped)
                {
                    _logger.LogError($"Circuit Break for MQ Stream {mqStreamProducer.Value.FullName} is still tripped");
                }

                isStillTripped = tripped == true ? true : isStillTripped;
            }

            if (isStillTripped)
            {
                _circuitBreakerTimeOut *= 2;
                _circuitBreakerTimeOut =  _circuitBreakerTimeOut > _circuitBreakerMaxTimeout ? _circuitBreakerMaxTimeout : _circuitBreakerTimeOut;
                return true;
            }
            else
            {
                _logger.LogInformation("Circuit Breakers have all been cleared.");
                _circuitBreakerTimeOut = 1;
                _circuitBreakerTripped = false;
            }
        }

        return false;
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
        long   fltnum = await SetNextFlightNumber();
        Flight flight = new Flight(fltnum);

        Message message = _flightInfoProducer.CreateMessage(flight);
        message.Properties.ReplyTo = "scott";

        message.AddApplicationProperty(FlightConstants.MQ_EVENT_CATEGORY, EnumMessageEvents.FlightInfo);
        message.AddApplicationProperty(FlightConstants.MQ_EVENT_NAME, "FlightCreated");
        message.AddApplicationProperty(FlightConstants.MQ_EVENT_ID, flight.Id);

        bool success = await _flightInfoProducer.SendMessageAsync(message);

        return EnumInternalTaskReturn.Success;
    }
}