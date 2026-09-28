namespace DisplayPilot;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool preview = args.Contains("--smoke-test");
        using var mutex = new Mutex(true, preview ? @"Local\DisplayPilot.Preview" : @"Local\DisplayPilot", out bool first);
        if (!first)
        {
            MessageBox.Show("DisplayPilot is already running. Open it from the system tray.", "DisplayPilot");
            return;
        }
        Application.Run(new MainWindow(args.Contains("--minimized"), preview));
    }
}
