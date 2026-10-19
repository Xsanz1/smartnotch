namespace SmartNotch.App.Services;

public sealed class NotifItem
{
    public string App { get; init; } = "";
    public string PrimaryId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public DateTime Time { get; init; } = DateTime.Now;
}
