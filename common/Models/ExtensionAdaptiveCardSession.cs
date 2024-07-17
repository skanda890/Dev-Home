﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHome.Common.Models;

/// <summary>
/// Wrapper class for the IExtensionAdaptiveCardSession and IExtensionAdaptiveCardSession2 interfaces.
/// </summary>
public class ExtensionAdaptiveCardSession
{
    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(ExtensionAdaptiveCardSession));

    public IExtensionAdaptiveCardSession Session { get; private set; }

    public event TypedEventHandler<ExtensionAdaptiveCardSession, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

    public ExtensionAdaptiveCardSession(IExtensionAdaptiveCardSession cardSession)
    {
        Session = cardSession;

        if (Session is IExtensionAdaptiveCardSession2 cardSession2)
        {
            cardSession2.Stopped += OnSessionStopped;
        }
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        try
        {
            return Session.Initialize(extensionUI);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Initialize failed due to exception");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Session is IExtensionAdaptiveCardSession2 cardSession2)
            {
                cardSession2.Stopped -= OnSessionStopped;
            }

            Session.Dispose();
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Dispose failed due to exception");
        }
    }

    public async Task<ProviderOperationResult> OnAction(string action, string inputs)
    {
        try
        {
            return await Session.OnAction(action, inputs);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"OnAction failed due to exception");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
        }
    }

    public void OnSessionStopped(IExtensionAdaptiveCardSession2 sender, ExtensionAdaptiveCardSessionStoppedEventArgs args)
    {
        Stopped?.Invoke(this, args);
    }
}
