using System.Text;

namespace MiniInfos.Providers.Antigravity;

/// <summary>
/// Antigravity 2: steps.metadata = CortexStepMetadata, field 9 = ModelUsageStats.
/// input/cache read/cache write are separate; output already includes thinking. Do not count gen_metadata again.
/// Field numbers were checked against descriptors shipped with the locally installed language server.
/// </summary>
internal static class AntigravityResponses
{
    public static LoggedResponse? Read(string session, long index, ReadOnlySpan<byte> bytes, string? project)
    {
        try
        {
            var metadata = new MetadataFields(bytes);
            var stats = metadata.Message(9);
            if (stats.IsEmpty) return null;
            var usage = new MetadataFields(stats);
            var timestamp = metadata.Message(7);
            if (timestamp.IsEmpty) timestamp = metadata.Message(1);
            var at = new MetadataFields(timestamp).Count(1);
            if (at <= 0 || at > 253402300799) return null;
            var input = usage.Count(2); var output = usage.Count(3); var cached = usage.Count(5); var written = usage.Count(4);
            if (new[] { input, output, cached, written }.Any(count => count > 1_000_000_000)) return null;
            var entry = new LedgerEntry(at, input, output, cached, written);
            if (entry.Count.Total <= 0) return null;
            // One generation may populate multiple steps. Its message id is the same in each copy.
            var key = usage.Text(7) ?? usage.Text(12) ?? usage.Text(11) ?? "step-" + index;
            return new LoggedResponse(session + ":" + key, entry, "Antigravity", session, project);
        }
        catch (Exception e) when (e is FormatException or DecoderFallbackException or OverflowException)
        {
            return null;
        }
    }

    public static string? Project(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var metadata = new MetadataFields(bytes);
            var workspace = new MetadataFields(metadata.Message(1)).Text(1) ?? metadata.Text(7);
            return workspace is not null && Uri.TryCreate(workspace, UriKind.Absolute, out var uri) && uri.IsFile
                ? Infrastructure.Storage.SourceFiles.LeafName(uri.LocalPath) : null;
        }
        catch (Exception e) when (e is FormatException or DecoderFallbackException) { return null; }
    }
}
