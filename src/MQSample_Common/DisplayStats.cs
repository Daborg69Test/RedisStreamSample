using Spectre.Console;

using Spectre.Console;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MQSample_Common;


namespace MQSample_Common;

public class DisplayStats
{
    private Table _table;
    private Stats _stats;


    public DisplayStats(Stats stats)
    {
        _stats = stats;

        _table = new Table().Centered();

        BuildColumns();
        AddRows();

        System.Console.Clear();
        AnsiConsole.Write(_table);
    }



    private void BuildColumns()
    {
        // Columns
        _table.ShowHeaders = true;
        _table.AddColumn("Title");
        _table.Columns[0].PadRight(6);
        _table.AddColumn("Primary");
    }

    private void AddRows()
    {
        _table.AddRow(MarkUpValue("Msg Sent", "green"));
        _table.AddRow(MarkUpValue("Created Msg", "green"));
        _table.AddRow(MarkUpValue("Success Msg", "green"));
        _table.AddRow(MarkUpValue("Failure Msg", "green"));
        _table.AddRow(MarkUpValue("CB Tripped", "green"));
        _table.AddRow(MarkUpValue("Consumed Msg", "green"));
        _table.AddRow(MarkUpValue("Last BatchRecv", "green"));
        _table.AddRow(MarkUpValue("Last CheckPt", "green"));
        _table.AddRow(MarkUpValue("Await CheckPt", "green"));
    }

    private void AddColumnsForStats()
    {
        int col = 0;
            col++;
            int row = 0;
            _table.UpdateCell(row++, col, MarkUp(_stats.SuccessMessages));
            _table.UpdateCell(row++, col, MarkUp(_stats.CreatedMessages));
            _table.UpdateCell(row++, col, MarkUp(_stats.SuccessMessages));
            _table.UpdateCell(row++, col, MarkUp(_stats.FailureMessages, false));
            _table.UpdateCell(row++, col, MarkUp(_stats.CircuitBreakerTripped));
            _table.UpdateCell(row++, col, MarkUp(_stats.ConsumedMessages));
            _table.UpdateCell(row++, col, MarkUpValue(_stats.ConsumeLastBatchReceived, "grey"));
            _table.UpdateCell(row++, col, MarkUp(_stats.ConsumeLastCheckpoint));
            _table.UpdateCell(row++, col, MarkUp(_stats.ConsumeCurrentAwaitingCheckpoint));
    }

    private string MarkUpValue(string value, string colorName, bool bold = false, bool underline = false,
        bool italic = false)
    {
        string val = "[" + colorName + "]";
        if (bold) val += "[bold]";


        val += value + "[/]";
        return val;
    }


    private string MarkUp(ulong value, bool positiveGreen = true)
    {
        string color = "green";
        if (!positiveGreen)
        {
            if (value > 0) color = "red";
        }

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;

    }

    private string MarkUp(int value, bool positiveGreen = true)
    {
        string color = "green";
        if (!positiveGreen)
        {
            if (value > 0) color = "red";
        }

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;

    }

    private string MarkUp(bool value, bool trueGreen = true)
    {
        string color = "";
        if (trueGreen) color = "green";
        else color = "red";

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;

    }


    public void Refresh()
    {
        AddColumnsForStats();
        System.Console.Clear();
        AnsiConsole.Write(_table);

    }
}
