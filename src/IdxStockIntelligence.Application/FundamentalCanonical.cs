using System.Text.Json;

namespace IdxStockIntelligence.Application;

// E3: canonical payload bytes and hash binding. Reuses the existing V0.2 canonical
// writer (ordinal property order, compact, no BOM/newline) so fundamental payloads and
// screener evidence share one byte contract. Decimal and Instant literal spellings are
// preserved; nothing is re-rounded or defaulted.
public static class FundamentalCanonical
{
    public const string Integrity = "FUNDAMENTAL_INTEGRITY_FAILURE";

    /// <summary>Canonical UTF-8 JSON text for one payload element.</summary>
    public static string CanonicalPayload(JsonElement payload) =>
        ScreenerEvidenceJson.Canonicalize(payload.GetRawText());

    /// <summary>SHA-256 of the canonical payload bytes, lowercase hex.</summary>
    public static string PayloadHash(JsonElement payload) =>
        ScreenerEvidenceJson.Sha256(CanonicalPayload(payload));

    /// <summary>True when the declared envelope hash matches the canonical payload.</summary>
    public static bool Matches(JsonElement payload, string declaredSha256) =>
        string.Equals(PayloadHash(payload), declaredSha256, StringComparison.Ordinal);
}
