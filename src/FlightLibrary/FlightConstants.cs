namespace FlightLibrary;

public class FlightConstants
{
    public const string STREAM_FLIGHT_OPS  = "FlightOps";
    public const string STREAM_FLIGHT_INFO = "FlightInfo";
    public const string STREAM_PASSENGER   = "Passenger";

    public const string MQ_EVENT_CATEGORY = "Evt.Cat";  // FlightInfo / Flightops / Passenger
    public const string MQ_EVENT_NAME     = "Evt.Name"; // ReserveFlight, FlightDeparted, FlightArrived
    public const string MQ_EVENT_ID       = "Evt.Id";   // The ID of the Type / name.  For instance Passenger ID.

    public const string MQEN_FlightCreated   = "created";
    public const string MQEN_PassengerBooked = "booked";
}