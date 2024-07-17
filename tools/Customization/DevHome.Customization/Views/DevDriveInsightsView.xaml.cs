// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Common.Extensions;
using DevHome.Common.Views;
using DevHome.Customization.ViewModels;
using Microsoft.UI.Xaml;

namespace DevHome.Customization.Views;

public sealed partial class DevDriveInsightsView : DevHomeUserControl
{
    public DevDriveInsightsViewModel ViewModel
    {
        get;
    }

    public DevDriveInsightsView()
    {
        ViewModel = Application.Current.GetService<DevDriveInsightsViewModel>();
        this.InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.OnFirstNavigateTo();
    }
}
