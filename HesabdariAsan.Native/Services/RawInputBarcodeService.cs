using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace HesabdariAsan.Native.Services;

public sealed class RawInputBarcodeService : IDisposable
{
    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEKEYBOARD = 1;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_BACK = 0x08;
    private readonly Dictionary<IntPtr, DeviceBuffer> _buffers = new();
    private HwndSource? _source;

    public event EventHandler<string>? BarcodeScanned;

    public void Attach(Window window)
    {
        if (_source is not null) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) throw new InvalidOperationException("پنجره هنوز آماده دریافت ورودی نیست.");
        _source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("منبع پیام Windows پیدا نشد.");
        _source.AddHook(WndProc);

        var devices = new[]
        {
            new RAWINPUTDEVICE { usUsagePage = 0x01, usUsage = 0x06, dwFlags = 0, hwndTarget = handle }
        };
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_INPUT) return IntPtr.Zero;
        try { ProcessRawInput(lParam); } catch { }
        return IntPtr.Zero;
    }

    private void ProcessRawInput(IntPtr rawHandle)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        if (GetRawInputData(rawHandle, RID_INPUT, IntPtr.Zero, ref size, headerSize) == uint.MaxValue || size == 0) return;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(rawHandle, RID_INPUT, buffer, ref size, headerSize) == uint.MaxValue) return;
            var header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            if (header.dwType != RIM_TYPEKEYBOARD) return;
            var keyboard = Marshal.PtrToStructure<RAWKEYBOARD>(IntPtr.Add(buffer, Marshal.SizeOf<RAWINPUTHEADER>()));
            if (keyboard.Message is not (WM_KEYDOWN or WM_SYSKEYDOWN)) return;
            HandleKey(header.hDevice, keyboard.VKey);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void HandleKey(IntPtr device, ushort vKey)
    {
        var now = DateTime.UtcNow;
        if (!_buffers.TryGetValue(device, out var state))
        {
            state = new DeviceBuffer();
            _buffers[device] = state;
        }
        if (state.LastAt != default && (now - state.LastAt).TotalMilliseconds > 250) state.Reset();
        state.LastAt = now;

        if (vKey == VK_RETURN)
        {
            var value = state.Text.ToString().Trim();
            var elapsedMs = state.FirstAt == default ? 0 : (now - state.FirstAt).TotalMilliseconds;
            var scannerLike = value.Length >= 4 && elapsedMs <= Math.Max(600, value.Length * 120);
            state.Reset();
            if (scannerLike) BarcodeScanned?.Invoke(this, value);
            return;
        }
        if (vKey == VK_BACK)
        {
            if (state.Text.Length > 0) state.Text.Length--;
            return;
        }

        var ch = VirtualKeyToBarcodeChar(vKey);
        if (ch is null) return;
        if (state.Text.Length == 0) state.FirstAt = now;
        if (state.Text.Length < 128) state.Text.Append(ch.Value);
    }

    private static char? VirtualKeyToBarcodeChar(ushort key)
    {
        if (key is >= 0x30 and <= 0x39) return (char)key;
        if (key is >= 0x60 and <= 0x69) return (char)('0' + key - 0x60);
        if (key is >= 0x41 and <= 0x5A) return (char)key;
        return key switch
        {
            0x6A => '*', 0x6B => '+', 0x6D => '-', 0x6E => '.', 0x6F => '/',
            0xBD => '-', 0xBE => '.', 0xBF => '/', _ => null
        };
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
        _buffers.Clear();
    }

    private sealed class DeviceBuffer
    {
        public StringBuilder Text { get; } = new();
        public DateTime FirstAt { get; set; }
        public DateTime LastAt { get; set; }
        public void Reset() { Text.Clear(); FirstAt = default; LastAt = default; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
}
