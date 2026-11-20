using System.Runtime.InteropServices;
using TrainerDeck.Core.Diagnostics;

namespace TrainerDeck.UI.Input;


public static class TouchKeyboard
{
    public static void Show()
    {
        try
        {
            var tip = (ITipInvocation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("4ce576fa-83dc-4f88-951c-9d0782b4e376"))!)!;
            tip.Toggle(GetDesktopWindow());
            Marshal.ReleaseComObject(tip);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentNullException)
        {
            string tabTip = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "microsoft shared", "ink", "TabTip.exe");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tabTip) { UseShellExecute = true });
            }
            catch (Exception startEx)
            {
                Log.Info($"Touch keyboard unavailable: {ex.Message}; {startEx.Message}");
            }
        }
    }

    [ComImport]
    [Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}
