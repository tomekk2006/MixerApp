﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.CompilerServices;
using System.Net.NetworkInformation;


namespace MixerApp
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            bool isRedirect = DecideRedirection();

            if (!isRedirect)
            {
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(
                        DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    _ = new App();
                });
            }

            return 0;
        }
        private static bool DecideRedirection()
        {
            bool isRedirect = false;
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            ExtendedActivationKind kind = args.Kind;
            AppInstance keyInstance = AppInstance.FindOrRegisterForKey("MySingleInstanceApp");

            if (keyInstance.IsCurrent)
            {
                keyInstance.Activated += OnActivated;
            }
            else
            {
                isRedirect = true;
                RedirectActivationTo(args, keyInstance);
            }

            return isRedirect;
        }
        private static void OnActivated(object sender, AppActivationArguments args)
        {
            ExtendedActivationKind kind = args.Kind;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEvent(
            IntPtr lpEventAttributes, bool bManualReset,
            bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        private static extern bool SetEvent(IntPtr hEvent);

        [DllImport("ole32.dll")]
        private static extern uint CoWaitForMultipleObjects(
            uint dwFlags, uint dwMilliseconds, ulong nHandles,
            IntPtr[] pHandles, out uint dwIndex);

        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        private static IntPtr redirectEventHandle = IntPtr.Zero;

        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        public static void RedirectActivationTo(AppActivationArguments args,
                                                AppInstance keyInstance)
        {
            redirectEventHandle = CreateEvent(IntPtr.Zero, true, false, null);
            Task.Run(() =>
            {
                keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
                SetEvent(redirectEventHandle);
            });

            uint CWMO_DEFAULT = 0;
            uint INFINITE = 0xFFFFFFFF;
            _ = CoWaitForMultipleObjects(
               CWMO_DEFAULT, INFINITE, 1,
               [redirectEventHandle], out uint handleIndex);

            // Bring the window to the foreground
            Process process = Process.GetProcessById((int)keyInstance.ProcessId);
            ShowWindowAsync(process.MainWindowHandle, 9);
            SetForegroundWindow(process.MainWindowHandle);

        }
    }

}
