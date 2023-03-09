using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlugEnt.StreamProcessor;

namespace ConsumerApp;

public interface IFlightConsumer : IMqStreamConsumer
{
    Task StartAsync();
}


public class FlightConsumer : MqStreamConsumer, IFlightConsumer
{
    //private Func<Message, Task<bool>> _consumptionHandler;
    private ILogger<FlightConsumer> _logger;

    public FlightConsumer(ILogger<FlightConsumer> logger, ServiceProvider serviceProvider) : base(logger, serviceProvider) { _logger = logger; }



    public async Task StartAsync()
    {
        try
        {
            await ConnectAsync();
            await ConsumeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            System.Console.WriteLine("There was an error - {0}", ex.Message);
        }
    }
}