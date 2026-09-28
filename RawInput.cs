using System.Runtime.InteropServices;
using System.Text;
namespace DisplayPilot;

internal static class RawInput
{
    [StructLayout(LayoutKind.Sequential)] internal struct Device { public ushort Page, Usage; public uint Flags; public IntPtr Window; }
    [StructLayout(LayoutKind.Sequential)] internal struct Header { public uint Type, Size; public IntPtr Device, Param; }
    [StructLayout(LayoutKind.Sequential)] internal struct Entry { public IntPtr Device; public uint Type; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder? data, ref uint size);
    [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] Entry[]? devices, ref uint count, uint size);
    internal static void Register(IntPtr window)
    {
        Device[] devices = [new() { Page = 1, Usage = 2, Flags = 0x2100, Window = window }, new() { Page = 1, Usage = 6, Flags = 0x2100, Window = window }];
        if (!RegisterRawInputDevices(devices, 2, (uint)Marshal.SizeOf<Device>())) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static string Name(IntPtr device)
    {
        uint size = 0; GetRawInputDeviceInfo(device, 0x20000007, null, ref size);
        if (size == 0 || size > 32768) return "";
        var name = new StringBuilder((int)size); return GetRawInputDeviceInfo(device, 0x20000007, name, ref size) == uint.MaxValue ? "" : name.ToString();
    }
    internal static string Read(IntPtr input)
    {
        uint size = 0, header = (uint)Marshal.SizeOf<Header>();
        if (GetRawInputData(input, 0x10000003, IntPtr.Zero, ref size, header) == uint.MaxValue || size < header) return "";
        var buffer = Marshal.AllocHGlobal((int)size);
        try { if (GetRawInputData(input, 0x10000003, buffer, ref size, header) != size) return ""; var h = Marshal.PtrToStructure<Header>(buffer); return h.Type <= 1 ? Name(h.Device) : ""; }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    internal static List<(string Path, string Kind, string Name)> Enumerate()
    {
        uint count = 0, size = (uint)Marshal.SizeOf<Entry>(); var result = new List<(string, string, string)>();
        if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) return result;
        var entries = new Entry[count]; var actual = GetRawInputDeviceList(entries, ref count, size); if (actual == uint.MaxValue) return result;
        foreach (var e in entries.Take((int)actual)) { var p = Name(e.Device); if (p.Length == 0) continue; var kind = e.Type == 0 ? "Mouse" : e.Type == 1 ? "Keyboard" : "HID (detection only)"; var name = p.Contains("VID_054C", StringComparison.OrdinalIgnoreCase) ? "Sony controller / HID" : p.Contains("VID_045E", StringComparison.OrdinalIgnoreCase) ? "Microsoft / Xbox HID" : kind; result.Add((p, kind, name)); }
        return result;
    }
}
