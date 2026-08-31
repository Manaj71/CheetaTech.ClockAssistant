using System.Text.Json;
using CheetaTech.ClockAssistant.Core.Security;
using Microsoft.Maui.Storage;

namespace CheetaTech.ClockAssistant.App.Services.Security;

/// <summary>
/// Stores provider credentials in MAUI SecureStorage using a staged two-slot
/// commit design.
///
/// This is not an ACID/database transaction. The design provides logical
/// commit, rollback, and recovery protection around SecureStorage operations.
///
/// Two payload slots are used so the currently committed credentials remain
/// available while a replacement candidate is staged in the inactive slot.
/// The candidate becomes authoritative only after:
/// 1. the inactive-slot payload is written;
/// 2. that payload is read back and verified; and
/// 3. the active-slot pointer is switched and verified.
///
/// The active-slot pointer is the logical commit point.
///
/// Read recovery order:
/// 1. active committed payload;
/// 2. alternate payload slot if the active payload is unreadable;
/// 3. legacy username/password keys from the earlier Phase 4 format.
///
/// Credential payloads contain secrets and must never be written to logs,
/// diagnostic evidence, exception messages, or telemetry.
/// </summary>
public sealed class MauiCredentialStore : ICredentialStore
{
    private const string ActiveSlotKey =
        "ClockAssistant.Provider.Credentials.ActiveSlot";

    private const string SlotAKey =
        "ClockAssistant.Provider.Credentials.SlotA";

    private const string SlotBKey =
        "ClockAssistant.Provider.Credentials.SlotB";

    // Legacy keys are read-only compatibility keys for the earlier Phase 4 format.
    // New successful saves use the two-slot payload format and remove these keys
    // only after the new committed payload has been verified.
    private const string LegacyUsernameKey =
        "ClockAssistant.Provider.Username";

    private const string LegacyPasswordKey =
        "ClockAssistant.Provider.Password";

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Stages and commits a replacement credential pair without overwriting the
    /// currently authoritative payload first.
    ///
    /// The candidate is written to the inactive slot, read back, and verified.
    /// Only after that verification does the active-slot pointer switch.
    ///
    /// If staging fails, the previous active credentials remain authoritative.
    /// If pointer commit fails, pointer rollback is attempted on a best-effort
    /// basis because SecureStorage does not provide a transaction across writes.
    /// The previous payload slot itself is deliberately retained for recovery.
    /// </summary>
    public async Task SaveCredentialsAsync(
        StoredCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            throw new ArgumentException(
                "Username is required.",
                nameof(credentials));
        }

        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new ArgumentException(
                "Password is required.",
                nameof(credentials));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var previousActiveSlot =
            await GetActiveSlotAsync(
                cancellationToken);

        // Always stage into the inactive slot so the currently committed payload
        // remains untouched until the candidate has been fully verified.
        var targetSlot =
            previousActiveSlot == SlotAKey
                ? SlotBKey
                : SlotAKey;

        var payload =
            new CredentialPayload(
                Username: credentials.Username,
                Password: credentials.Password);

        // SECURITY: 'serialized' contains the password. Never log this value.
        var serialized =
            JsonSerializer.Serialize(
                payload,
                SerializerOptions);

        await SecureStorage.Default.SetAsync(
            targetSlot,
            serialized);

        cancellationToken.ThrowIfCancellationRequested();

        // Read the staged payload back before commit. A successful SetAsync call
        // alone is not treated as proof that the candidate can be recovered.
        var stagedCredentials =
            await ReadSlotAsync(
                targetSlot,
                cancellationToken);

        if (!CredentialsMatch(
                credentials,
                stagedCredentials))
        {
            throw new InvalidOperationException(
                "Credential staging verification failed. " +
                "The previous active credentials were not replaced.");
        }

        try
        {
            // LOGICAL COMMIT POINT:
            // after this pointer switches, targetSlot becomes authoritative.
            await SecureStorage.Default.SetAsync(
                ActiveSlotKey,
                targetSlot);

            cancellationToken.ThrowIfCancellationRequested();

            var committedSlot =
                await GetActiveSlotAsync(
                    cancellationToken);

            if (committedSlot != targetSlot)
            {
                throw new InvalidOperationException(
                    "Credential active-slot verification failed.");
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(previousActiveSlot))
            {
                try
                {
                    await SecureStorage.Default.SetAsync(
                        ActiveSlotKey,
                        previousActiveSlot);
                }
                catch
                {
                    // Best-effort pointer rollback only. SecureStorage does not
                    // provide an ACID transaction across payload and pointer writes.
                    // The previous payload slot is deliberately kept intact so
                    // recovery remains possible even if pointer restoration fails.
                }
            }
            else
            {
                SecureStorage.Default.Remove(
                    ActiveSlotKey);
            }

            throw;
        }

        // Legacy migration cleanup is deliberately last. The old two-key pair is
        // removed only after payload staging, read-back verification, pointer commit,
        // and pointer verification all succeed.
        SecureStorage.Default.Remove(
            LegacyUsernameKey);

        SecureStorage.Default.Remove(
            LegacyPasswordKey);
    }

    /// <summary>
    /// Reads credentials using the recovery order:
    /// active slot, alternate slot, then legacy keys.
    ///
    /// The alternate slot is retained intentionally so a previous complete
    /// payload can still be recovered if the active payload becomes unreadable.
    /// </summary>
    public async Task<StoredCredentials?> GetCredentialsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var activeSlot =
            await GetActiveSlotAsync(
                cancellationToken);

        if (!string.IsNullOrWhiteSpace(activeSlot))
        {
            var activeCredentials =
                await ReadSlotAsync(
                    activeSlot,
                    cancellationToken);

            if (activeCredentials is not null)
            {
                return activeCredentials;
            }

            var fallbackSlot =
                activeSlot == SlotAKey
                    ? SlotBKey
                    : SlotAKey;

            var fallbackCredentials =
                await ReadSlotAsync(
                    fallbackSlot,
                    cancellationToken);

            if (fallbackCredentials is not null)
            {
                return fallbackCredentials;
            }
        }

        return await ReadLegacyCredentialsAsync(
            cancellationToken);
    }

    /// <summary>
    /// Deletes every credential generation owned by this application:
    /// the active-slot pointer, both payload slots, and both legacy keys.
    ///
    /// This intentionally removes rollback/recovery material as well as the
    /// currently active credentials.
    /// </summary>
    public Task DeleteCredentialsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SecureStorage.Default.Remove(
            ActiveSlotKey);

        SecureStorage.Default.Remove(
            SlotAKey);

        SecureStorage.Default.Remove(
            SlotBKey);

        SecureStorage.Default.Remove(
            LegacyUsernameKey);

        SecureStorage.Default.Remove(
            LegacyPasswordKey);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns only a recognized slot identifier. Any missing or unexpected
    /// active-pointer value is treated as having no committed slot.
    /// </summary>
    private static async Task<string?> GetActiveSlotAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var activeSlot =
            await SecureStorage.Default.GetAsync(
                ActiveSlotKey);

        if (activeSlot == SlotAKey ||
            activeSlot == SlotBKey)
        {
            return activeSlot;
        }

        return null;
    }

    /// <summary>
    /// Reads and validates one serialized payload slot.
    ///
    /// Invalid, missing, or malformed payloads are treated as unavailable rather
    /// than exposing their raw contents. Serialized credential data must never be logged.
    /// </summary>
    private static async Task<StoredCredentials?> ReadSlotAsync(
        string slotKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var serialized =
            await SecureStorage.Default.GetAsync(
                slotKey);

        if (string.IsNullOrWhiteSpace(serialized))
        {
            return null;
        }

        CredentialPayload? payload;

        try
        {
            payload =
                JsonSerializer.Deserialize<CredentialPayload>(
                    serialized,
                    SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.Username) ||
            string.IsNullOrWhiteSpace(payload.Password))
        {
            return null;
        }

        return new StoredCredentials(
            payload.Username,
            payload.Password);
    }

    /// <summary>
    /// Reads the earlier Phase 4 username/password key pair for migration
    /// compatibility. These legacy keys are never used for new writes.
    /// </summary>
    private static async Task<StoredCredentials?> ReadLegacyCredentialsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var username =
            await SecureStorage.Default.GetAsync(
                LegacyUsernameKey);

        cancellationToken.ThrowIfCancellationRequested();

        var password =
            await SecureStorage.Default.GetAsync(
                LegacyPasswordKey);

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return new StoredCredentials(
            username,
            password);
    }

    private static bool CredentialsMatch(
        StoredCredentials expected,
        StoredCredentials? actual)
    {
        return actual is not null
            && string.Equals(
                expected.Username,
                actual.Username,
                StringComparison.Ordinal)
            && string.Equals(
                expected.Password,
                actual.Password,
                StringComparison.Ordinal);
    }

    // Internal serialization shape only. Never expose or log an instance.
    private sealed record CredentialPayload(
        string Username,
        string Password);
}
