namespace Fusion.Models;

public sealed class GuildItem
{
    public ulong Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}
