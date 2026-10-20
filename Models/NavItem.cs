namespace SpeedMeterPro.Models;

public enum UserTier
{
    Free = 0,
    Trial = 1,
    Subscribed = 2,
}

public sealed class NavItem
{
    public required string Label { get; init; }
    public required string Icon { get; init; }
    public IconChar Glyph { get; init; } = IconChar.None;
    public Type? ControlType { get; set; }
    public Action? ShowModal { get; init; }
    public bool SubscriberOnly { get; init; }
    public bool AlwaysFree { get; init; }
}
