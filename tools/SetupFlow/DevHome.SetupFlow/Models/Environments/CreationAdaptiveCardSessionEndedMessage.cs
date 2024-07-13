﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace DevHome.SetupFlow.Models.Environments;

public class CreationAdaptiveCardSessionEndedMessage : ValueChangedMessage<CreationAdaptiveCardSessionEndedData>
{
    public CreationAdaptiveCardSessionEndedMessage(CreationAdaptiveCardSessionEndedData value)
        : base(value)
    {
    }
}
