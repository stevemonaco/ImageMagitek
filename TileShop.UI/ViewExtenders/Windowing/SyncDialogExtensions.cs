using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace TileShop.UI.Windowing;

/// <summary>
/// Code obtained from grokys https://github.com/grokys/ShowDialogSyncAvalonia
/// </summary>
public static class SyncDialogExtensions
{
    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr GetHandle(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern long Int64_objc_msgSend_IntPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend(
        IntPtr receiver,
        IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    public static void ShowDialogSync(this Window window, Window owner)
    {
        window.ShowDialogSync<object>(owner);
    }

    [return: MaybeNull]
    public static T? ShowDialogSync<T>(this Window window, Window owner)
    {
        var handle = window.TryGetPlatformHandle();

        if (handle is null)
            return default;

        if (handle is IMacOSTopLevelPlatformHandle macHandle)
        {
            var nsAppStaticClass = objc_getClass("NSApplication");
            var sharedApplicationSelector = GetHandle("sharedApplication");
            var sharedApplication = IntPtr_objc_msgSend(nsAppStaticClass, sharedApplicationSelector);
            var runModalForSelector = GetHandle("runModalForWindow:");
            var stopModalSelector = GetHandle("stopModal");

            void DialogClosed(object? sender, EventArgs e)
            {
                Void_objc_msgSend(sharedApplication, stopModalSelector);
                window.Closed -= DialogClosed;
            }

            window.Closed += DialogClosed;
            var task = window.ShowDialog<T>(owner);
            Int64_objc_msgSend_IntPtr(sharedApplication, runModalForSelector, macHandle.NSWindow);
            return task.Result;
        }
        else
        {
            using var source = new CancellationTokenSource();
            var result = default(T);
            window.ShowDialog<T>(owner).ContinueWith(
                t =>
                {
                    if (t.IsCompletedSuccessfully)
                        result = t.Result;
                    source.Cancel();
                },
                TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(source.Token);
            return result;
        }
    }
}
