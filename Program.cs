using System.Threading;
using SpeedMeterPro.Services;
using SpeedMeterPro.UI;

namespace SpeedMeterPro;

internal static class Program
{
    private const string MutexName = "Global\\SpeedMeterPro_SingleInstance";

    [STAThread]
    private static void Main()
    {
        Logger.Init();
        bool createdNew = false;
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                Logger.Log("Speed Meter Pro already running. Exiting duplicate instance.");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            Logger.Log("Speed Meter Pro starting...");
            Application.Run(new WidgetForm());
        }
        catch (Exception ex)
        {
            Logger.Log($"FATAL: {ex}");
            MessageBox.Show($"Startup Error: {ex.Message}", "Speed Meter Pro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (mutex != null)
            {
                if (createdNew)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                mutex.Dispose();
            }
        }
    }
}
