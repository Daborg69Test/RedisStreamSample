using MQSample_Common;
using Spectre.Console;

namespace FlightOps;

public class DisplayPassengerStats : DisplayStats
{
    private PassengerEngine _passengerEngine;


    public DisplayPassengerStats(PassengerEngine passengerEngine) : base()
    {
        _passengerEngine = passengerEngine;

        AddColumn("Passenger Svc", 6);
        AddColumn("Data", 6);
        AddColumn("Success", 6);
        AddColumn("Failures", 6);

        AddRow("Engine Running");
        AddRow("Last Recv Flt #");
        AddRow("MQ: FlightInfo MsgRecv");
        AddRow("MQ:Passenger Created");
        AddRow("Flt # Out Of Sequence");

        // Create Menu Items
        AddMenuItem("S", "Start / Stop Engine");
        AddMenuItem("D", "Delete Passenger MQ Stream");
        AddMenuItem("I", "Change Passenger Creation Interval");
        AddMenuItem("X", "Exit");
        DisplayMenu();
    }


    public ulong LastFlightNumber { get; set; }

    public ulong CreatedSuccess { get; set; }

    public ulong CreatedError { get; set; }

    public bool EngineRunning { get; set; }

    public ulong FlightOutOfSequenceCount { get; set; }

    public ulong FlightInfoMsgReceived { get; set; }


    protected override void UpdateData()
    {
        int row = 0;
        _menuTable.Expand = true;

        _statsTable.UpdateCell(row, 1, MarkUp(EngineRunning));
        _statsTable.UpdateCell(++row, 1, MarkUp(LastFlightNumber));
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightInfoMsgReceived));

        // Passengers Created
        _statsTable.UpdateCell(row, 2, MarkUp(CreatedSuccess));
        _statsTable.UpdateCell(row, 3, MarkUp(CreatedError));
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightOutOfSequenceCount, false));
    }
}