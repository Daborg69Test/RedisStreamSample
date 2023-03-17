namespace FlightLibrary;

// NOTE:   This list must never have existing values changed as they may be stored in a database and multiple simultaneous running apps use them.
//        - You can only add to the list.


/// <summary>
/// Identifies the type of the event.
/// </summary>
public enum EnumMessageEvents
{
    FlightInfo          = 0,
    Passenger           = 1,
    FlightOps           = 2,
    CommunicationOutput = 3,
}