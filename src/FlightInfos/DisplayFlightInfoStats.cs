using MQSample_Common;
using Spectre.Console;

namespace FlightOps;

public class DisplayFlightInfoStats : DisplayStats
{
    private FlightInfoEngine _flightInfoEngine;


    public DisplayFlightInfoStats(FlightInfoEngine flightInfoEngine) : base()
    {
        // Store the engine where we will get the stats from
        _flightInfoEngine = flightInfoEngine;

        AddColumn("FlightInfo", 6);
        AddColumn("Data", 6);
        AddColumn("Conf. Pass", 6);
        AddColumn("Conf. Fail", 6);

        AddRow("Engine Running");
        AddRow("Current Flt #");
        AddRow("Flight Info Prod");
        AddRow("Flights Created");

        // Create Menu Items
        AddMenuItem("S", "Start / Stop Engine");
        AddMenuItem("D", "Delete FlightInfo MQ Stream");
        AddMenuItem("I", "Change Flight Creation Interval");
        AddMenuItem("X", "Exit");
        DisplayMenu();
    }


    public ulong CurrentFlightNumber
    {
        get { return _flightInfoEngine.CurrentFlightNumber; }
    }

    public ulong Created
    {
        get { return _flightInfoEngine.FlightInfoProducer.MessageCounter; }
    }

    public ulong CreateConfirmedSuccess
    {
        get { return _flightInfoEngine.FlightInfoProducer.Stat_MessagesSuccessfullyConfirmed; }
    }

    public ulong CreatedError
    {
        get { return _flightInfoEngine.FlightInfoProducer.Stat_MessagesErrored; }
    }

    public bool EngineRunning
    {
        get { return _flightInfoEngine.IsRunning; }
    }


    public ulong FlightsCreated
    {
        get { return _flightInfoEngine.Stat_FlightsCreatedCount; }
    }


    protected override void UpdateData()
    {
        int row = 0;
        _menuTable.Expand = true;

        _statsTable.UpdateCell(row, 1, MarkUp(EngineRunning));
        _statsTable.UpdateCell(++row, 1, MarkUp(CurrentFlightNumber));

        // Flight Info Producer
        _statsTable.UpdateCell(++row, 1, MarkUp(Created));
        _statsTable.UpdateCell(row, 2, MarkUp(CreateConfirmedSuccess));
        _statsTable.UpdateCell(row, 3, MarkUp(CreatedError));

        // Flights Created This Run
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightsCreated));
    }
}