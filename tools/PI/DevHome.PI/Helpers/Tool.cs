﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevHome.PI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Win32.Foundation;

namespace DevHome.PI.Helpers;

public abstract partial class Tool : ObservableObject
{
    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    [property: JsonIgnore]
    private string _pinGlyph;

    [ObservableProperty]
    [property: JsonIgnore]
    private ImageSource? _toolIconSource;

    public string Name { get; private set; }

    public Tool(string name, bool isPinned)
    {
        Name = name;
        IsPinned = isPinned;
        PinGlyph = IsPinned ? CommonHelper.UnpinGlyph : CommonHelper.PinGlyph;
    }

    public abstract IconElement GetIcon();

    partial void OnIsPinnedChanged(bool oldValue, bool newValue)
    {
        PinGlyph = newValue ? CommonHelper.UnpinGlyph : CommonHelper.PinGlyph;
        OnIsPinnedChange(newValue);
    }

    protected virtual void OnIsPinnedChange(bool newValue)
    {
    }

    [RelayCommand]
    public void TogglePinnedState()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    public void Invoke()
    {
        InvokeTool(null, TargetAppData.Instance.TargetProcess?.Id, TargetAppData.Instance.HWnd);
    }

    [RelayCommand]
    public void InvokeWithParent(Window parent)
    {
        InvokeTool(parent, TargetAppData.Instance.TargetProcess?.Id, TargetAppData.Instance.HWnd);
    }

    internal virtual void InvokeTool(Window? parentWindow, int? targetProcessId, HWND hWnd) => throw new NotImplementedException();

    [RelayCommand]
    public abstract void UnregisterTool();
}
