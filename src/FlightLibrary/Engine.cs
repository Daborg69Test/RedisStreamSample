using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Stream.Client;
using SlugEnt;
using SlugEnt.MQStreamProcessor;
using SlugEnt.SLRStreamProcessing;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;
using Microsoft.Extensions.Configuration;


namespace FlightLibrary;

public class Engine
{
    public static readonly string MSG_EVENT_CATEGORY = ".MC";
    public static readonly string MSG_EVENT_NAME     = ".MN";
    public static readonly string MSG_EVENT_ID       = ".MI";

    protected readonly ILogger _logger;

    protected readonly IServiceProvider _serviceProvider;

    protected SLRStreamEngine _slrStreamEngine;

    //protected          IMQStreamEngine       _mqStreamEngine;
    protected InternalTaskScheduler _internalTaskScheduler;
    protected ConfigurationOptions  _redisConfigurationOptions;

    protected RedisConnectionPoolManager _redisConnectionPoolManager;
    protected RedisClient                _redisClient;
    protected TimeSpan                   _redisCacheExpireTimeSpan;
    protected IConfiguration             _configuration;
    private   bool                       _stopProcessing = false;


    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="serviceProvider"></param>
    public Engine(ILogger<Engine> logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger                = logger;
        _serviceProvider       = serviceProvider;
        _internalTaskScheduler = new InternalTaskScheduler();
        _configuration         = configuration;

        // Setup the streaming engine
        _slrStreamEngine = _serviceProvider.GetService<SLRStreamEngine>();
        if (_slrStreamEngine == null)
            throw new ApplicationException($"Unable to locate the SLRStreamEngine class in the Service Directory");


        // Set Parallel mode of internal tasks.
        bool RunParallel = configuration.GetValue<bool>("RunInParallel");
        _internalTaskScheduler.RunTasksInParallel = RunParallel;
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
    /// Initializes the engine.  
    /// </summary>
    /// <para>This can be overridden in derived classes, but they should call back to this method.</para>
    /// <returns></returns>
    public async Task<bool> InitializeAsync(RedisConfiguration redisConfiguration)
    {
        _redisConnectionPoolManager = new RedisConnectionPoolManager(redisConfiguration);
        SystemTextJsonSerializer serializer = new();
        _redisClient = new RedisClient(_redisConnectionPoolManager, serializer, redisConfiguration);

        _slrStreamEngine.RedisConfiguration = redisConfiguration;
        bool initialized = _slrStreamEngine.Initialize(redisConfiguration);
        return initialized;
    }



    /// <summary>
    /// Starts the Engine Processing loop.
    /// </summary>
    /// <para>If overridden by derived classes, they must call back to this method</para>
    /// <returns></returns>
    public async Task StartEngineAsync()
    {
        if (IsRunning)
            return;

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

        _internalTaskScheduler.RemoveAllTasks();

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
                _internalTaskScheduler.CheckTasks();

                //Task checkTasks = _internalTaskScheduler.CheckTasks();

                //Task.WaitAll(checkTasks);

                Thread.Sleep(EngineSleepInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        IsStopped = true;
    }



    /// <summary>
    /// Determines if message has an Event Category Name property on it.
    /// </summary>
    /// <param name="message">The SLRMessage</param>
    /// <param name="streamName">Name of stream the message is from</param>
    /// <returns></returns>
    public (bool isValid, string eventCategoryName ) ValidateEventCategory(SLRMessage message, string streamName)
    {
        string eventCategory = message.GetPropertyAsString(MSG_EVENT_CATEGORY);
        if (eventCategory == String.Empty)
        {
            string msgInfo = message.PrintMessageInfo();
            _logger.LogError($"Received an empty eventCategory on the {streamName} stream.  Message: {msgInfo}");

            return (false, string.Empty);
        }

        return (true, eventCategory);
    }


    public (bool isValid, string eventName) ValidateEventName(SLRMessage message, string streamName)
    {
        string eventName = message.GetPropertyAsString(MSG_EVENT_NAME);
        if (eventName == String.Empty)
        {
            string msgInfo = message.PrintMessageInfo();
            _logger.LogError($"Received an empty eventName for the message on stream {streamName}.  Message: {msgInfo}");

            // Return True, event though technically it is an issue,  This should be a testing thing only.
            return (false, string.Empty);
        }

        return (true, eventName);
    }
}