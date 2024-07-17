// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Common.DevHomeAdaptiveCards.CardModels;
using DevHome.Common.Extensions;
using DevHome.Common.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DevHome.Common.Views.AdaptiveCardViews;

/// <summary>
/// Content dialog with non-interactive content within an adaptive card
/// </summary>
public sealed partial class ContentDialogWithNonInteractiveContent : ContentDialog
{
    public ContentDialogWithNonInteractiveContent(DevHomeContentDialogContent content)
    {
        this.InitializeComponent();

        // Since we use the renderer service to allow the card to receive theming updates, we need to ensure the UI thread is used.
        var dispatcherQueue = Application.Current.GetService<DispatcherQueue>();
        dispatcherQueue.TryEnqueue(async () =>
        {
            Title = content.Title;
            PrimaryButtonText = content.PrimaryButtonText;
            var rendererService = Application.Current.GetService<AdaptiveCardRenderingService>();
            var renderer = await rendererService.GetRendererAsync();
            renderer.HostConfig.ContainerStyles.Default.BackgroundColor = Microsoft.UI.Colors.Transparent;
            var card = renderer.RenderAdaptiveCardFromJsonString(content.ContentDialogInternalAdaptiveCardJson?.Stringify() ?? string.Empty);
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = card.FrameworkElement,
            };
            SecondaryButtonText = content.SecondaryButtonText;
            this.Focus(FocusState.Programmatic);
        });
    }
}
