using SlugEnt.MQStreamProcessor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ProducerApp;

/// <summary>
/// Interface for FlightProducer
/// </summary>
internal interface IFlightProducer : IMqStreamProducer
{
    public Task StartAsync();
    public Task Stop();
    public void SetProducerMethod(Func<FlightProducer, Task> method);
}


/// <summary>
/// Produces Flight Data
/// </summary>
internal class FlightProducer : MqStreamProducer, IMqStreamProducer, IFlightProducer
{
    private Func<FlightProducer, Task> _produceMessagesMethod;
    private Thread                     _workerThread;



    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="mqStreamName"></param>
    /// <param name="appName"></param>
    /// <param name="produceMessagesMethod">Method that should be called to produce messages</param>
    public FlightProducer(ILogger<FlightProducer> logger, ServiceProvider serviceProvider) : base(logger, serviceProvider) { }



    public void SetProducerMethod(Func<FlightProducer, Task> method) { _produceMessagesMethod = method; }



    /// <summary>
    /// Initiates the startup of the Producer, establishes connection to RabbitMQ
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync()
    {
        await ConnectAsync();
        await base.StartAsync();

        _workerThread = new Thread(ProduceMessages);
        _workerThread.Start();
    }


    public async Task Stop()
    {
        IsCancelled = true;

        // Print Final Totals
        System.Console.WriteLine("Messages:");
        System.Console.WriteLine($"  Produced:    {MessageCounter}");
    }



    /// <summary>
    /// Calls the method to produce messages.  That method does not return until done.
    /// </summary>
    /// <param name="worker"></param>
    private void ProduceMessages() { _produceMessagesMethod(this); }



    /// <summary>
    /// When set to true the producing method from the caller should stop processing
    /// </summary>
    public bool IsCancelled { get; protected set; }
}