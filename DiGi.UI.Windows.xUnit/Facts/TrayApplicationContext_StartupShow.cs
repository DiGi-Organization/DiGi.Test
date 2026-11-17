using DiGi.UI.Windows.Classes;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DiGi.UI.Windows.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that with <see cref="TrayApplicationContextOptions.ShowWindowOnStartup"/> the window is not shown inside the base constructor - the derived constructor runs to its end with the window still hidden - and is shown once the dispatcher pumps (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#43).
        /// </summary>
        [Fact]
        public void TrayApplicationContext_StartupShowDeferred()
        {
            StartupShowResult startupShowResult = StartupShow(showWindowOnStartup: true);

            Assert.Null(startupShowResult.Exception);
            Assert.True(startupShowResult.ConstructorCompleted);
            Assert.False(startupShowResult.IsVisible_Constructor);
            Assert.True(startupShowResult.IsVisible_Pumped);
        }

        /// <summary>
        /// Verifies that without <see cref="TrayApplicationContextOptions.ShowWindowOnStartup"/> the window stays hidden after the dispatcher pumps: the application starts in the tray only.
        /// </summary>
        [Fact]
        public void TrayApplicationContext_NoStartupShow()
        {
            StartupShowResult startupShowResult = StartupShow(showWindowOnStartup: false);

            Assert.Null(startupShowResult.Exception);
            Assert.True(startupShowResult.ConstructorCompleted);
            Assert.False(startupShowResult.IsVisible_Constructor);
            Assert.False(startupShowResult.IsVisible_Pumped);
        }

        private sealed record StartupShowResult(Exception? Exception, bool ConstructorCompleted, bool IsVisible_Constructor, bool IsVisible_Pumped);

        private static StartupShowResult StartupShow(bool showWindowOnStartup)
        {
            Exception? exception = null;
            bool constructorCompleted = false;
            bool isVisible_Constructor = false;
            bool isVisible_Pumped = false;

            Thread thread = new(() =>
            {
                TestTrayApplicationContext? testTrayApplicationContext = null;
                try
                {
                    // The application's context: the deferred show is a dispatcher operation, as in the tray application.
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    testTrayApplicationContext = new TestTrayApplicationContext(new TrayApplicationContextOptions() { ShowWindowOnStartup = showWindowOnStartup });
                    constructorCompleted = testTrayApplicationContext.ConstructorCompleted;
                    isVisible_Constructor = testTrayApplicationContext.IsVisible_Constructor;

                    // Background runs after Loaded, so the deferred show has run once the frame exits.
                    DispatcherFrame dispatcherFrame = new();
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => dispatcherFrame.Continue = false));
                    Dispatcher.PushFrame(dispatcherFrame);

                    isVisible_Pumped = testTrayApplicationContext.IsWindowVisible;
                }
                catch (Exception exception_Thread)
                {
                    exception = exception_Thread;
                }
                finally
                {
                    testTrayApplicationContext?.Release();
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return new StartupShowResult(exception, constructorCompleted, isVisible_Constructor, isVisible_Pumped);
        }

        private sealed class TestTrayApplicationContext : TrayApplicationContext<Window>
        {
            public TestTrayApplicationContext(TrayApplicationContextOptions trayApplicationContextOptions)
                : base("DiGi.UI.Windows.xUnit", trayApplicationContextOptions)
            {
                IsVisible_Constructor = Window?.IsVisible == true;
                ConstructorCompleted = true;
            }

            public bool ConstructorCompleted { get; }

            public bool IsVisible_Constructor { get; }

            public bool IsWindowVisible => Window?.IsVisible == true;

            public void Release()
            {
                Window?.Hide();
                NotifyIcon?.Dispose();
            }

            protected override Window GetWindow()
            {
                // Off screen, out of the taskbar and not activated: the fact must not disturb the desktop it runs on.
                return new Window()
                {
                    Width = 100,
                    Height = 100,
                    Left = -10000,
                    Top = -10000,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                };
            }
        }
    }
}
