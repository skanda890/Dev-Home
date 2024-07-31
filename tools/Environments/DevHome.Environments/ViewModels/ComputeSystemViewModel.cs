﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using DevHome.Common.Environments.Helpers;
using DevHome.Common.Environments.Models;
using DevHome.Common.Environments.Services;
using DevHome.Common.Services;
using DevHome.Common.TelemetryEvents.Environments;
using DevHome.Common.TelemetryEvents.SetupFlow.Environments;
using DevHome.Environments.Helpers;
using DevHome.Environments.Models;
using DevHome.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.Windows.DevHome.SDK;
using Serilog;

namespace DevHome.Environments.ViewModels;

/// <summary>
/// View model for a compute system. Each 'card' in the UI represents a compute system.
/// Contains an instance of the compute system object as well.
/// </summary>
public partial class ComputeSystemViewModel : ComputeSystemCardBase, IRecipient<ComputeSystemOperationStartedMessage>, IRecipient<ComputeSystemOperationCompletedMessage>, IDisposable
{
    private readonly ILogger _log = Log.ForContext("SourceContext", nameof(ComputeSystemViewModel));
    private readonly StringResource _stringResource;
    private readonly Window _mainWindow;
    private readonly IComputeSystemManager _computeSystemManager;
    private readonly ComputeSystemProvider _provider;

    private readonly SemaphoreSlim _semaphoreSlimLock = new(1, 1);

    public ComputeSystemCache ComputeSystem { get; protected set; }

    // Launch button operations
    public ObservableCollection<OperationsViewModel> LaunchOperations { get; set; } = new();

    public ObservableCollection<CardProperty> Properties { get; set; } = new();

    public string PackageFullName { get; set; }

    private readonly Func<ComputeSystemCardBase, bool> _removalAction;

    [ObservableProperty]
    private bool _shouldShowDotOperations;

    [ObservableProperty]
    private bool _shouldShowSplitButton;

    private bool _disposedValue;

    private Action<ComputeSystemReviewItem>? _configurationAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputeSystemViewModel"/> class.
    /// This class requires a 3-step initialization:
    /// 1. Create the instance of the class. Constructor saves the parameters, but doesn't make
    ///    any OOP calls to IComputeSystem or initialize UX data which requires UI thread.
    /// 2. Call <see cref="InitializeCardDataAsync"/> to fetch the compute system data from the extension and cache it in ComputeSystem property.
    ///    This can be done on any thread and in parallel with other compute systems.
    /// 3. Call <see cref="InitializeUXData"/> to initialize the UX controls with the data we fetched in step 2.
    /// This allows us to avoid heavy calls on the UI thread and initialize data in parallel.
    /// </summary>
    public ComputeSystemViewModel(
        IComputeSystemManager manager,
        IComputeSystem system,
        ComputeSystemProvider provider,
        Func<ComputeSystemCardBase, bool> removalAction,
        Action<ComputeSystemReviewItem>? configurationAction,
        string packageFullName,
        Window window)
    {
        _computeSystemManager = manager;
        _provider = provider;
        _mainWindow = window;

        ComputeSystem = new(system);
        PackageFullName = packageFullName;
        _removalAction = removalAction;
        _configurationAction = configurationAction;
        _stringResource = new StringResource("DevHome.Environments.pri", "DevHome.Environments/Resources");
    }

    /// <summary>
    /// Initializes the UX data for the compute system card.
    /// UX controls that must be initialize on the UI thread are initialized here with
    /// the data that we fetched earlier from the compute system (in InitializeCardDataAsync).
    /// </summary>
    public async void InitializeUXData()
    {
        BodyImage = await ComputeSystemHelpers.GetBitmapImageAsync(ComputeSystem);
        HeaderImage = CardProperty.ConvertMsResourceToIcon(_provider.Icon, PackageFullName);
        SetupOperationProgressBasedOnState();
        SetPropertiesAsync();
        await InitializeOperationDataAsync();
    }

    /// <summary>
    /// Fetch the compute system data from the extension and cache it in ComputeSystem property.
    /// This can be done on any thread and in parallel with other compute systems. After that we can initialize the UX
    /// controls with the data we fetched (using InitializeUXData).
    /// </summary>
    /// <returns>async Task</returns>
    public async Task InitializeCardDataAsync()
    {
        ProviderDisplayName = _provider.DisplayName;
        Name = ComputeSystem.DisplayName.Value;
        AssociatedProviderId = ComputeSystem.AssociatedProviderId.Value!;
        ComputeSystemId = ComputeSystem.Id.Value!;

        if (!string.IsNullOrEmpty(ComputeSystem.SupplementalDisplayName.Value))
        {
            AlternativeName = new string("(" + ComputeSystem.SupplementalDisplayName + ")");
        }

        await ComputeSystem.FetchDataAsync();
        await InitializeStateAsync();

        ComputeSystem.StateChanged += _computeSystemManager.OnComputeSystemStateChanged;
        _computeSystemManager.ComputeSystemStateChanged += OnComputeSystemStateChanged;
    }

    private async Task InitializeOperationDataAsync()
    {
        await _semaphoreSlimLock.WaitAsync();
        try
        {
            ShouldShowDotOperations = false;
            ShouldShowSplitButton = false;

            RegisterForAllOperationMessages(DataExtractor.FillDotButtonOperations(ComputeSystem, _mainWindow), DataExtractor.FillLaunchButtonOperations(_provider, ComputeSystem, _configurationAction));

            _ = Task.Run(async () =>
            {
                var start = DateTime.Now;
                List<OperationsViewModel> validData = new();
                foreach (var data in await DataExtractor.FillDotButtonPinOperationsAsync(ComputeSystem))
                {
                    if ((!data.WasPinnedStatusSuccessful) || (data.ViewModel == null))
                    {
                        _log.Error($"Pinned status check failed: for '{Name}': {data?.PinnedStatusDisplayMessage}. DiagnosticText: {data?.PinnedStatusDiagnosticText}");
                        continue;
                    }

                    validData.Add(data.ViewModel);
                    WeakReferenceMessenger.Default.Register<ComputeSystemOperationStartedMessage, OperationsViewModel>(this, data.ViewModel);
                    WeakReferenceMessenger.Default.Register<ComputeSystemOperationCompletedMessage, OperationsViewModel>(this, data.ViewModel);
                }

                _log.Information($"Registering pin operations for {Name} in background took {DateTime.Now - start}");

                // Add valid data to the DotOperations collection
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    foreach (var data in validData)
                    {
                        DotOperations.Add(data);
                    }

                    // Only show dot operations when there are items in the list.
                    ShouldShowDotOperations = DotOperations.Count > 0;

                    // Only show Launch split button with operations when there are items in the list.
                    ShouldShowSplitButton = LaunchOperations.Count > 0;
                });
            });

            SetPropertiesAsync();
        }
        finally
        {
            _semaphoreSlimLock.Release();
        }
    }

    private async Task RefreshOperationDataAsync()
    {
        ComputeSystem.ResetSupportedOperations();
        var supportedOperations = ComputeSystem.SupportedOperations?.Value ?? ComputeSystemOperations.None;

        if (supportedOperations.HasFlag(ComputeSystemOperations.PinToStartMenu))
        {
            ComputeSystem.ResetPinnedToStartMenu();
        }

        if (supportedOperations.HasFlag(ComputeSystemOperations.PinToTaskbar))
        {
            ComputeSystem.ResetPinnedToTaskbar();
        }

        ComputeSystem.ResetComputeSystemProperties();
        await InitializeOperationDataAsync();
    }

    private async Task InitializeStateAsync()
    {
        var result = await ComputeSystem.GetStateAsync();
        if (result.Result.Status == ProviderOperationStatus.Failure)
        {
            _log.Error($"Failed to get state for {ComputeSystem.DisplayName} due to {result.Result.DiagnosticText}");
        }

        State = result.State;
        StateColor = ComputeSystemHelpers.GetColorBasedOnState(State);
    }

    private async void SetPropertiesAsync()
    {
        if (State == ComputeSystemState.Deleting || State == ComputeSystemState.Deleted)
        {
            return;
        }

        var properties = await ComputeSystemHelpers.GetComputeSystemCardPropertiesAsync(ComputeSystem!, PackageFullName);
        if (!ComputeSystemHelpers.RemoveAllItemsAndReplace(Properties, properties))
        {
            Properties = new(properties);
        }
    }

    public void OnComputeSystemStateChanged(ComputeSystem sender, ComputeSystemState newState)
    {
        _mainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            if (sender.Id == ComputeSystem.Id.Value &&
                sender.AssociatedProviderId.Equals(ComputeSystem.AssociatedProviderId.Value, StringComparison.OrdinalIgnoreCase))
            {
                State = newState;
                StateColor = ComputeSystemHelpers.GetColorBasedOnState(newState);
                SetupOperationProgressBasedOnState();

                // The supported operations for a compute system can change based on the current state of the compute system.
                // So we need to rebuild the dot and launch operations that appear in the UI based on the current
                // supported operations of the compute system. InitializeOperationDataAsync will take care of this for us, by using
                // the DataExtractor helper.
                await RefreshOperationDataAsync();
            }
        });
    }

    public void RemoveStateChangedHandler()
    {
        ComputeSystem.StateChanged -= _computeSystemManager.OnComputeSystemStateChanged;
        _computeSystemManager.ComputeSystemStateChanged -= OnComputeSystemStateChanged;

        // Unregister from all operation messages
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    [RelayCommand]
    public void LaunchAction()
    {
        LastConnected = DateTime.Now;

        // We'll need to disable the card UI while the operation is in progress and handle failures.
        Task.Run(async () =>
        {
            _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                UiMessageToDisplay = _stringResource.GetLocalized("LaunchingEnvironmentText");
                IsOperationInProgress = true;
            });

            TelemetryFactory.Get<ITelemetry>().Log(
                "Environment_Launch_Event",
                LogLevel.Critical,
                new EnvironmentLaunchEvent(ComputeSystem.AssociatedProviderId.Value, EnvironmentsTelemetryStatus.Started));

            var operationResult = await ComputeSystem.ConnectAsync(string.Empty);

            var (displayMessage, diagnosticText, telemetryStatus) = ComputeSystemHelpers.LogResult(operationResult?.Result, _log);
            TelemetryFactory.Get<ITelemetry>().Log(
                "Environment_Launch_Event",
                LogLevel.Critical,
                new EnvironmentLaunchEvent(ComputeSystem.AssociatedProviderId.Value, telemetryStatus, displayMessage, diagnosticText));

            if (telemetryStatus == EnvironmentsTelemetryStatus.Failed)
            {
                // Show the error notification to tell the user the operation failed
                OnErrorReceived(displayMessage);
            }

            _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                IsOperationInProgress = false;
                UiMessageToDisplay = string.Empty;
            });
        });
    }

    private void RemoveComputeSystem()
    {
        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            _log.Information($"Removing Compute system with Name: {ComputeSystem.DisplayName} from UI");
            _removalAction(this);
            RemoveStateChangedHandler();
        });
    }

    /// <summary>
    /// Implements the Receive method from the IRecipient<ComputeSystemOperationStartedMessage> interface. When this message
    /// is received we fire the first telemetry event to capture which operation and provider is starting.
    /// </summary>
    /// <param name="message">The object that holds the data needed to capture the operationInvoked telemetry data</param>
    public void Receive(ComputeSystemOperationStartedMessage message)
    {
        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            var data = message.Value;
            IsOperationInProgress = true;
            ShouldShowLaunchOperation = false;
            var providerId = ComputeSystem.AssociatedProviderId.Value;

            _log.Information($"operation '{data.ComputeSystemOperation}' starting for Compute System: {Name}");

            TelemetryFactory.Get<ITelemetry>().Log(
                "Environment_OperationInvoked_Event",
                LogLevel.Measure,
                new EnvironmentOperationEvent(EnvironmentsTelemetryStatus.Started, data.ComputeSystemOperation, providerId, data.AdditionalContext),
                relatedActivityId: message.Value.ActivityId);
        });
    }

    /// <summary>
    /// Implements the Receive method from the IRecipient<ComputeSystemOperationCompletedMessage> interface. When this message
    /// is received the operation is completed and we can log the result of the operation.
    /// </summary>
    /// <param name="message">The object that holds the data needed to capture the operationInvoked telemetry data</param>
    public void Receive(ComputeSystemOperationCompletedMessage message)
    {
        _mainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            var data = message.Value;
            _log.Information($"operation '{data.ComputeSystemOperation}' completed for Compute System: {Name}");

            var providerId = ComputeSystem.AssociatedProviderId.Value;
            var (displayMessage, diagnosticText, telemetryStatus) = ComputeSystemHelpers.LogResult(data.OperationResult.Result, _log);
            TelemetryFactory.Get<ITelemetry>().Log(
                "Environment_OperationInvoked_Event",
                LogLevel.Measure,
                new EnvironmentOperationEvent(telemetryStatus, data.ComputeSystemOperation, providerId, data.AdditionalContext, displayMessage, diagnosticText),
                relatedActivityId: message.Value.ActivityId);
        });
    }

    /// <summary>
    /// Register the ViewModel to receive messages for the start and completion of operations from all view models within the
    /// DotOperation and LaunchOperation lists. When there is an operation this ViewModel will receive the started and
    /// the completed messages.
    /// </summary>
    private void RegisterForAllOperationMessages(List<OperationsViewModel> dotOperations, List<OperationsViewModel> launchOperations)
    {
        _log.Information($"Registering ComputeSystemViewModel '{Name}' from provider '{ProviderDisplayName}' with WeakReferenceMessenger");

        LaunchOperations.Clear();
        DotOperations!.Clear();

        foreach (var dotOperation in dotOperations)
        {
            DotOperations.Add(dotOperation);
            WeakReferenceMessenger.Default.Register<ComputeSystemOperationStartedMessage, OperationsViewModel>(this, dotOperation);
            WeakReferenceMessenger.Default.Register<ComputeSystemOperationCompletedMessage, OperationsViewModel>(this, dotOperation);
        }

        foreach (var launchOperation in launchOperations)
        {
            LaunchOperations.Add(launchOperation);
            WeakReferenceMessenger.Default.Register<ComputeSystemOperationStartedMessage, OperationsViewModel>(this, launchOperation);
            WeakReferenceMessenger.Default.Register<ComputeSystemOperationCompletedMessage, OperationsViewModel>(this, launchOperation);
        }
    }

    private bool IsComputeSystemStateTransitioning(ComputeSystemState state)
    {
        switch (state)
        {
            case ComputeSystemState.Starting:
            case ComputeSystemState.Saving:
            case ComputeSystemState.Stopping:
            case ComputeSystemState.Pausing:
            case ComputeSystemState.Restarting:
            case ComputeSystemState.Creating:
            case ComputeSystemState.Deleting:
                return true;
            default:
                return false;
        }
    }

    private void SetupOperationProgressBasedOnState()
    {
        if (IsComputeSystemStateTransitioning(State))
        {
            ShouldShowLaunchOperation = false;
            IsOperationInProgress = true;
        }
        else
        {
            IsOperationInProgress = false;
            ShouldShowLaunchOperation = true;
        }

        if (State == ComputeSystemState.Deleted)
        {
            RemoveComputeSystem();
        }

        if (State == ComputeSystemState.Creating)
        {
            IsCardCreating = true;
        }
        else
        {
            IsCardCreating = false;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _semaphoreSlimLock.Dispose();
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
