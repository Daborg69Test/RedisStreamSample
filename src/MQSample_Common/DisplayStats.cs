using Spectre.Console;


namespace MQSample_Common;

public class DisplayStats
{
    protected Layout _layout;
    protected Table  _statsTable;
    protected Table  _menuTable;

    private int _columnCount;

    private Dictionary<string, string> _menuItems = new();


    public DisplayStats()
    {
        _layout = new Layout("FlightInfo")
            .SplitColumns(
                          new Layout("Menu"),
                          new Layout("Stats"));

        _statsTable             = new Table().Centered();
        _statsTable.ShowHeaders = true;
        _columnCount            = 0;

        _menuTable = new Table();
        _menuTable.AddColumn("Menu Item");

        // Add Table to Stats Panel
        _layout["Stats"].Update(_statsTable);
        _layout["menu"].Update(_menuTable);
    }


    public void AddMenuItem(string key, string name) { _menuItems.Add(key, name); }


    public void DisplayMenu()
    {
        foreach (KeyValuePair<string, string> menuItem in _menuItems)
        {
            _menuTable.AddRow(MarkUpValue($" ( {menuItem.Key} )  {menuItem.Value}", "yellow"));
        }


        _layout["menu"].Update(_menuTable);
    }


    public void AddRow(string rowTitle) { _statsTable.AddRow(MarkUpValue(rowTitle, "green")); }


    public void AddColumn(string columnTitle, int padding = 0)
    {
        _statsTable.AddColumn(columnTitle);
        if (padding > 0)
            _statsTable.Columns[_columnCount].PadRight(padding);
    }


    /// <summary>
    /// Must Override - Is used to update the Stats Display
    /// </summary>
    protected virtual void UpdateData() { }



    protected string MarkUpValue(string value, string colorName, bool bold = false, bool underline = false,
                                 bool italic = false)
    {
        string val = "[" + colorName + "]";
        if (bold)
            val += "[bold]";


        val += value + "[/]";
        return val;
    }


    protected string MarkUp(ulong value, bool positiveGreen = true)
    {
        string color = "green";
        if (!positiveGreen)
        {
            if (value > 0)
                color = "red";
        }

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;
    }


    protected string MarkUp(int value, bool positiveGreen = true)
    {
        string color = "green";
        if (!positiveGreen)
        {
            if (value > 0)
                color = "red";
        }

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;
    }


    protected string MarkUp(bool value)
    {
        string color = "";
        if (value)
            color = "green";
        else
            color = "red";

        string val = "[" + color + "]";
        val += value + "[/]";
        return val;
    }


    public void Refresh()
    {
        UpdateData();
        System.Console.Clear();
        AnsiConsole.Write(_layout);
    }
}