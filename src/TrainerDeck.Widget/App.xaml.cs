using System;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TrainerDeck.Widget
{
    sealed partial class App : Application
    {
        private XboxGameBarWidget _widget;

        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            XboxGameBarWidgetActivatedEventArgs widgetArgs = null;
            if (args.Kind == ActivationKind.Protocol && args is IProtocolActivatedEventArgs protocol
                && protocol.Uri.Scheme.Equals("ms-gamebarwidget"))
            {
                widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
            }

            if (widgetArgs == null || !widgetArgs.IsLaunchActivation)
            {
                return;
            }

            var rootFrame = new Frame();
            Window.Current.Content = rootFrame;
            _widget = new XboxGameBarWidget(widgetArgs, Window.Current.CoreWindow, rootFrame);
            rootFrame.Navigate(typeof(CheatsWidget), _widget);
            Window.Current.Closed += OnWidgetClosed;
            Window.Current.Activate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(InfoPage));
            }

            Window.Current.Activate();
        }

        private void OnWidgetClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            _widget = null;
            Window.Current.Closed -= OnWidgetClosed;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral deferral = e.SuspendingOperation.GetDeferral();
            _widget = null;
            deferral.Complete();
        }
    }
}
