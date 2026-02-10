namespace ACI.Tests.Common.TestData;

public static class TestIds
{
    public static string Session(int index = 1) => $"session-{index:D3}";

    public static string Window(int index = 1) => $"window-{index:D3}";

    public static string Action(int index = 1) => $"action-{index:D3}";
}

