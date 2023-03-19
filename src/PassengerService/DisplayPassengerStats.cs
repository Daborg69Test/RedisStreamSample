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


    public ulong LastFlightNumber
    {
        get { return _passengerEngine.FlightNumberLast; }
    }

    public ulong PassengerCreatedSuccess
    {
        get { return _passengerEngine.PassengerProducer.Stat_MessagesSuccessfullyConfirmed; }
    }

    public ulong PassengerCreatedError
    {
        get { return _passengerEngine.PassengerProducer.Stat_MessagesErrored; }
    }

    public ulong PassengerCreated
    {
        get { return _passengerEngine.PassengerProducer.MessageCounter; }
    }

    public bool EngineRunning
    {
        get { return _passengerEngine.IsRunning; }
    }

    public ulong FlightOutOfSequenceCount
    {
        get { return _passengerEngine.FlightNumberOutOfSync; }
    }

    public ulong FlightInfoMsgReceived
    {
        get { return _passengerEngine.FlightInfoMessagesConsumed; }
    }


    protected override void UpdateData()
    {
        int row = 0;
        _menuTable.Expand = true;

        _statsTable.UpdateCell(row, 1, MarkUp(EngineRunning));
        _statsTable.UpdateCell(++row, 1, MarkUp(LastFlightNumber));

        // FlightInfo Received
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightInfoMsgReceived));

        // Passengers Created
        _statsTable.UpdateCell(++row, 1, MarkUp(PassengerCreated));
        _statsTable.UpdateCell(row, 2, MarkUp(PassengerCreatedSuccess));
        _statsTable.UpdateCell(row, 3, MarkUp(PassengerCreatedError));
        _statsTable.UpdateCell(++row, 1, MarkUp(FlightOutOfSequenceCount, false));
    }
}