namespace FlightLibrary;

public class Passenger
{
    public Passenger(string name, long id)
    {
        Name = name;
        Id   = id;
    }


    public long Id { get; private set; }

    public string Name { get; private set; }

    public List<long> FlightIds { get; set; }

    public long CurrentFlight { get; set; }
}