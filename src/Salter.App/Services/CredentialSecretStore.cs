using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Salter.App.Interop;
using Salter.Core.Settings;

namespace Salter.App.Services;

/// <summary>
/// Keeps secrets in the Windows Credential Manager as generic credentials named <c>Salt.er/&lt;name&gt;</c>, persisted
/// for the current user on this machine. The secret is stored as UTF-16 text and is never logged.
/// </summary>
internal sealed partial class CredentialSecretStore : ISecretStore
{
    private const string TargetPrefix = "Salt.er/";
    private const string UserName = "Salt.er";

    private readonly ILogger<CredentialSecretStore> _logger;

    /// <summary>Creates the store.</summary>
    /// <param name="logger">Receives read failures; never the secret itself.</param>
    public CredentialSecretStore(ILogger<CredentialSecretStore> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Never fails for platform reasons: an unexpected Credential Manager error is logged (without any secret content)
    /// and reported as <see langword="null"/>, as is an empty credential.
    /// </remarks>
    public string? Get(string name)
    {
        var target = Target(name);
        if (!NativeMethods.CredRead(target, NativeMethods.CredentialTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != NativeMethods.ErrorNotFound)
            {
                LogReadFailed(target, error);
            }

            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            return credential.CredentialBlob == 0 || credential.CredentialBlobSize < sizeof(char)
                ? null
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)(credential.CredentialBlobSize / sizeof(char)));
        }
        finally
        {
            NativeMethods.CredFree(pointer);
        }
    }

    /// <inheritdoc/>
    /// <remarks>A <see langword="null"/>, empty or whitespace value removes the secret.</remarks>
    public void Set(string name, string? value)
    {
        var target = Target(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            Delete(target);
            return;
        }

        Write(target, value);
    }

    private static string Target(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TargetPrefix + name;
    }

    private static unsafe void Write(string target, string value)
    {
        var blob = Encoding.Unicode.GetBytes(value);
        try
        {
            fixed (char* targetName = target)
            fixed (char* userName = UserName)
            fixed (byte* data = blob)
            {
                var credential = new NativeCredential
                {
                    Type = NativeMethods.CredentialTypeGeneric,
                    TargetName = (nint)targetName,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = (nint)data,
                    Persist = NativeMethods.CredentialPersistLocalMachine,
                    UserName = (nint)userName,
                };

                if (!NativeMethods.CredWrite(credential, 0))
                {
                    throw Failure("Writing", target, Marshal.GetLastPInvokeError());
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    private static void Delete(string target)
    {
        if (!NativeMethods.CredDelete(target, NativeMethods.CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastPInvokeError();
            if (error != NativeMethods.ErrorNotFound)
            {
                throw Failure("Deleting", target, error);
            }
        }
    }

    private static InvalidOperationException Failure(string operation, string target, int error) =>
        new($"{operation} the credential '{target}' failed (Win32 error {error}).");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the credential {Target} failed (Win32 error {Error}).")]
    private partial void LogReadFailed(string target, int error);
}
