using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Stream.Client;
using SlugEnt;
using SlugEnt.MQStreamProcessor;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;

namespace FlightLibrary;

public class Engine
{
    protected readonly ILogger               _logger;
    protected readonly IServiceProvider      _serviceProvider;
    protected          IMQStreamEngine       _mqStreamEngine;
    protected          InternalTaskScheduler _internalTaskScheduler;

    //protected RedisLocker                _redisLocker;
    protected RedisConnectionPoolManager _redisConnectionPoolManager;
    protected RedisClient                _redisClient;
    protected TimeSpan                   _redisCacheExpireTimeSpan;

    private bool _circuitBreakerTripped    = false;
    private int  _circuitBreakerTimeOut    = 1;
    private int  _circuitBreakerMaxTimeout = 180;

//    private Thread _processingThread;
    private bool _stopProcessing = false;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    public Engine(ILogger<Engine> logger, IServiceProvider serviceProvider)
    {
        _logger                = logger;
        _serviceProvider       = serviceProvider;
        _internalTaskScheduler = new InternalTaskScheduler();


        // Setup the MQ Stream Engine and Producer
        StreamSystemConfig config = GetStreamSystemConfig();
        _mqStreamEngine                    = _serviceProvider.GetService<IMQStreamEngine>();
        _mqStreamEngine.StreamSystemConfig = config;

        ConfigureRedis();
    }


    /// <summary>
    /// How long the engine sleeps between processing loop cycles.
    /// </summary>
    public int EngineSleepInterval { get; set; } = 5000;


    /// <summary>
    /// True if the engine is running.  False if it is paused or stopped.
    /// </summary>
    public bool IsRunning { get; set; }


    /// <summary>
    /// This is set to true when the thread has received the stop command AND has actually stopped.
    /// </summary>
    protected bool IsStopped { get; set; }


    /// <summary>
    /// Defines the configuration for connecting to RabbitMQ Servers
    /// </summary>
    /// <returns></returns>
    public static StreamSystemConfig GetStreamSystemConfig()
    {
        IPEndPoint a = Helpers.GetIPEndPointFromHostName("rabbitmqa.slug.local", 5552);
        IPEndPoint b = Helpers.GetIPEndPointFromHostName("rabbitmqb.slug.local", 5552);
        IPEndPoint c = Helpers.GetIPEndPointFromHostName("rabbitmqc.slug.local", 5552);

        StreamSystemConfig config = new StreamSystemConfig
        {
            UserName = "testUser", Password = "TESTUSER", VirtualHost = "Test", Endpoints = new List<EndPoint> { a, b, c },
        };
        return config;
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
        SystemTextJsonSerializer serializer = new();

        //NewtonsoftSerializer     serializer = new();
        _redisClient = new RedisClient(_redisConnectionPoolManager, serializer, redisConfig);
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



    /// <summary>
    /// Initializes the engine.  
    /// </summary>
    /// <para>This can be overridden in derived classes, but they should call back to this method.</para>
    /// <returns></returns>
    public async Task InitializeAsync() { }



    /// <summary>
    /// Starts the Engine Processing loop.
    /// </summary>
    /// <para>If overridden by derived classes, they must call back to this method</para>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        if (IsRunning)
            return;

        await _mqStreamEngine.StartAllStreamsAsync();

        Thread processingThread = new Thread(new ThreadStart(Process));
        processingThread.Start();
        IsRunning = true;
    }


    /// <summary>
    /// Stops the Engine
    /// </summary>
    /// <para>If overridden by derived classes, they must call back to this method</para>
    /// <returns></returns>
    public async Task StopEngineAsync()
    {
        if (!IsRunning)
            return;


        if (_mqStreamEngine != null)
            await _mqStreamEngine.StopAllAsync();

        _stopProcessing = true;
        IsRunning       = false;
    }



    /// <summary>
    /// The engine's main processing thread.  It goes thru tasks each cycle and determines if anything needs to be don.
    /// </summary>
    private void Process()
    {
        IsStopped = false;

        while (!_stopProcessing)
        {
            try
            {
                Task checkTasks = _internalTaskScheduler.CheckTasks();
                Task.WaitAll(checkTasks);

                Thread.Sleep(EngineSleepInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        IsStopped = true;
    }
}