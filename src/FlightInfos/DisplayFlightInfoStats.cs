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
        AddColumn("Sent", 6);
        AddColumn("Received", 6);

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
        get { return _flightInfoEngine.Statistic_FlightsCreatedCount; }
    }

    public ulong Sent
    {
        get { return _flightInfoEngine.FlightInfoStream.StatisticMessagesSent; }

        //  get { return _flightInfoEngine.FlightInfoProducer.Stat_MessagesSuccessfullyConfirmed; }
    }

    public ulong Received
    {
        get { return 99; }

        //    get { return _flightInfoEngine.FlightInfoProducer.Stat_MessagesErrored; }
    }

    public bool EngineRunning
    {
        get { return _flightInfoEngine.IsRunning; }
    }


    public ulong FlightsCreated
    {
        get { return _flightInfoEngine.Statistic_FlightsCreatedCount; }
    }


    protected override void UpdateData()
    {
        int row = 0;
        _menuTable.Expand = true;

        _statsTable.UpdateCell(row, 1, MarkUp(EngineRunning));
        _statsTable.UpdateCell(++row, 1, MarkUp(CurrentFlightNumber));

        // Flight Info Producer
        _statsTable.UpdateCell(++row, 1, MarkUp(Created));
        _statsTable.UpdateCell(row, 2, MarkUp(Sent));
        _statsTable.UpdateCell(row, 3, MarkUp(Received));

        // Flights Created This Run
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightsCreated));
    }
}