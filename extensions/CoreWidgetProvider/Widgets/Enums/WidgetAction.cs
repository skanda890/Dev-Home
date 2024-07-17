﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace CoreWidgetProvider.Widgets.Enums;

public enum WidgetAction
{
    /// <summary>
    /// Error condition where the action cannot be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Action to validate the path provided by the user.
    /// </summary>
    CheckPath,

    /// <summary>
    /// Action to connect to host.
    /// </summary>
    Connect,

    /// <summary>
    /// Action to show previous widget item.
    /// </summary>
    PrevItem,

    /// <summary>
    /// Action to show next widget item.
    /// </summary>
    NextItem,

    /// <summary>
    /// Kill process #1.
    /// </summary>
    CpuKill1,

    /// <summary>
    /// Kill process #2.
    /// </summary>
    CpuKill2,

    /// <summary>
    /// Kill process #3.
    /// </summary>
    CpuKill3,

    Save,

    Cancel,

    ChooseFile,
}
