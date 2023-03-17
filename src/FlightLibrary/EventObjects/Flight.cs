using System.Text.Json.Serialization;

namespace FlightLibrary;

public class Flight
{
    public Flight(long flightId)
    {
        Id              = flightId;
        CreatedDateTime = DateTime.Now;
    }


    [JsonConstructor]
    public Flight(long flightId, DateTime createdDateTime)
    {
        Id              = flightId;
        CreatedDateTime = createdDateTime;
    }



    public long Id { get; private set; }

    public DateTime CreatedDateTime { get; private set; }

    public DateTime ScheduledDeparture { get; set; }

    public DateTime ScheduledArrival { get; set; }

    public DateTime ScheduledBoarding { get; set; }

    public Dictionary<string, Passenger> Passengers { get; set; }
}