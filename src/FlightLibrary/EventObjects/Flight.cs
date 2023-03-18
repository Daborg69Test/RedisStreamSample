using System.Text.Json.Serialization;

namespace FlightLibrary;

public class Flight
{
    public Flight(ulong flightId)
    {
        FlightId        = flightId;
        CreatedDateTime = DateTime.Now;
    }


    [JsonConstructor]
    public Flight(ulong flightId, DateTime createdDateTime)
    {
        FlightId        = flightId;
        CreatedDateTime = createdDateTime;
    }



    public ulong FlightId { get; private set; }

    public DateTime CreatedDateTime { get; private set; }

    public DateTime ScheduledDeparture { get; set; }

    public DateTime ScheduledArrival { get; set; }

    public DateTime ScheduledBoarding { get; set; }

    public Dictionary<string, Passenger> Passengers { get; set; }
}