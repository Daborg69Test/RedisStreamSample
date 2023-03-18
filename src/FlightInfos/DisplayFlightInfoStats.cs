using MQSample_Common;
using Spectre.Console;

namespace FlightOps;

public class DisplayFlightInfoStats : DisplayStats
{
    public DisplayFlightInfoStats() : base()
    {
        AddColumn("FlightInfo", 6);
        AddColumn("Data", 6);
        AddColumn("Success", 6);
        AddColumn("Failures", 6);

        AddRow("Engine Running");
        AddRow("Current Flt #");
        AddRow("Flight Created");

        // Create Menu Items
        AddMenuItem("S", "Start / Stop Engine");
        AddMenuItem("D", "Delete FlightInfo MQ Stream");
        AddMenuItem("I", "Change Flight Creation Interval");
        AddMenuItem("X", "Exit");
        DisplayMenu();
    }


    public ulong CurrentFlightNumber { get; set; }

    public ulong CreatedSuccess { get; set; }

    public ulong CreatedError { get; set; }

    public bool EngineRunning { get; set; }


    protected override void UpdateData()
    {
        int row = 0;
        _menuTable.Expand = true;

        _statsTable.UpdateCell(row++, 1, MarkUp(EngineRunning));
        _statsTable.UpdateCell(row++, 1, MarkUp(CurrentFlightNumber));
        _statsTable.UpdateCell(row, 2, MarkUp(CreatedSuccess));
        _statsTable.UpdateCell(row, 3, MarkUp(CreatedError));
    }
}