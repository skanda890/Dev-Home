﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevHome.SetupFlow.Models;
using DevHome.SetupFlow.ViewModels;
using DevHome.SetupFlow.ViewModels.Environments;

namespace DevHome.SetupFlow.TaskGroups;

public class SelectEnvironmentProviderTaskGroup : ISetupTaskGroup
{
    private readonly SelectEnvironmentProviderViewModel _selectEnvironmentProviderViewModel;

    public SelectEnvironmentProviderTaskGroup(SelectEnvironmentProviderViewModel selectEnvironmentProviderViewModel)
    {
        _selectEnvironmentProviderViewModel = selectEnvironmentProviderViewModel;
    }

    // No setup tasks needed for this task group.
    public IEnumerable<ISetupTask> SetupTasks => new List<ISetupTask>();

    public SetupPageViewModelBase GetSetupPageViewModel() => _selectEnvironmentProviderViewModel;

    // Review tab not needed for this task group.
    public ReviewTabViewModelBase GetReviewTabViewModel() => null;
}
