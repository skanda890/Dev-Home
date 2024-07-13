﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using COM;
using Serilog;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace FileExplorerGitIntegration.Models;

public sealed class GitLocalRepositoryProviderServer : IDisposable
{
    private readonly HashSet<uint> registrationCookies = new();
    private readonly Serilog.ILogger log = Log.ForContext("SourceContext", nameof(GitLocalRepositoryProviderServer));

    [UnconditionalSuppressMessage(
        "ReflectionAnalysis",
        "IL2050:COMCorrectness",
        Justification = "GitLocalRepositoryProviderFactory and all the interfaces it implements are defined in an assembly that is not marked trimmable which means the relevant interfaces won't be trimmed.")]
    public void RegisterGitRepositoryProviderServer<T>(Func<T> createGitRepositoryProviderServer)
        where T : GitLocalRepositoryProviderFactory
    {
        log.Debug($"Registering class object:");
        log.Debug($"CLSID: {typeof(T).GUID:B}");
        log.Debug($"Type: {typeof(T)}");

        uint cookie;
        var clsid = typeof(T).GUID;
        var hr = PInvoke.CoRegisterClassObject(
            clsid,
            new GitLocalRepositoryProviderFactory<T>(createGitRepositoryProviderServer),
            CLSCTX.CLSCTX_LOCAL_SERVER,
            Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED,
            out cookie);

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        registrationCookies.Add(cookie);
        log.Debug($"Cookie: {cookie}");
        hr = PInvoke.CoResumeClassObjects();
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    public void Run()
    {
        // TODO : We need to handle lifetime management of the server.
        // For details around ref counting and locking of out-of-proc COM servers, see
        // https://docs.microsoft.com/windows/win32/com/out-of-process-server-implementation-helpers
        // https://github.com/microsoft/devhome/issues/645
        Console.ReadLine();
        var disposedEvent = new ManualResetEvent(false);
        disposedEvent.WaitOne();
    }

    public void Dispose()
    {
        log.Debug($"Revoking class object registrations:");
        foreach (var cookie in registrationCookies)
        {
            log.Debug($"Cookie: {cookie}");
            var hr = PInvoke.CoRevokeClassObject(cookie);
            Debug.Assert(hr >= 0, $"CoRevokeClassObject failed ({hr:x}). Cookie: {cookie}");
        }
    }

    private sealed class Ole32
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        // https://docs.microsoft.com/windows/win32/api/combaseapi/ne-combaseapi-regcls
        public const REGCLS REGCLS_MULTIPLEUSE = (REGCLS)1;
        public const REGCLS REGCLS_SUSPENDED = (REGCLS)4;
#pragma warning restore SA1310 // Field names should not contain underscore
    }
}
