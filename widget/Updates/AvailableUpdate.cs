namespace MiniInfos.Updates;

/// <summary>A newer MiniInfos, ready to download: its installer and the SHA-256 the file must have.</summary>
public sealed record AvailableUpdate(Version Version, Uri Installer, string Sha256);
