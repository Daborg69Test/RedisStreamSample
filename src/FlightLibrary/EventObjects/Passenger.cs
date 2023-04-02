using System.Text.Json.Serialization;

namespace FlightLibrary;

public class Passenger
{
    [JsonConstructor]
    public Passenger(string name, ulong id)
    {
        Name = name;
        Id   = id;
    }


    public ulong Id { get; private set; }

    public string Name { get; private set; }

    public List<ulong> FlightIds { get; set; } = new();

    public ulong CurrentFlight { get; set; }
}