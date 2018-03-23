using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Globalization;

namespace Aliens_Mouse
{
    public partial class Wnd_Main : Form
    {
        #region Variables declararion

        private static Wnd_Main _This = null;

        private static int _ScreenWidth = 0;
        private static int _ScreenHeight = 0;
        private static int _ClientWidth = 800;
        private static int _ClientHeight = 600;
        private static bool _AutoClientSize = true;

        private static Point _MouseScreenPosition;
        private static Point _MouseClientPosition;
        private static Point _MouseInGamePosition;
        private static IntPtr _MouseHookID = IntPtr.Zero;

        private static string Debug_MouseScreen;
        private static string Debug_MouseClient;
        private static string Debug_MouseJoystick;
        private static string Debug_ClientSize;

        private static string _BgwErrorMsg = string.Empty;
        private static string _BgwErrorSrc = string.Empty;
        private static string _BgwErrorStk = string.Empty;

        private static Process _TargetProcess = null;        
        private const string _TargetProcess_Name = "aliens dehasped";        
        private static IntPtr _TargetProcess_Handle;
        private static IntPtr _TargetProcess_MemoryBaseAddress;
        private static bool _TargetProcess_Hooked = false;
        private static Timer _tProcess;

        /*** Game Data for Memory Hack ***/
        protected const int _P1_X_Offset = 0x0556AEA8;
        protected const int _P1_Y_Offset = 0x0556AEAC;
        protected const int _P1_BTN_Offset = 0x0556ACBC;
        protected const int _P2_X_Offset = 0x0556B1C0;
        protected const int _P2_Y_Offset = 0x0556B1C4;
        protected const int _P2_BTN_Offset = 0x0556AFD4;
        protected const string _X_NOP_Offset = "0x0002EE9C|6";
        protected const string _Y_NOP_Offset = "0x0002EEA5|6";
        protected const string _BTN_NOP_Offset_1 = "0x0002EE75|6";
        protected const string _BTN_NOP_Offset_2 = "0x0002EE81|6";
        protected const string _BTN_NOP_Offset_3 = "0x0002EE8D|3";
        protected const string _BTN_NOP_Offset_4 = "0x00048825|2";

        #endregion

        #region WIN32

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);


        /// <summary>
        /// Struct representing a point.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point)
            {
                return new Point(point.X, point.Y);
            }
        }

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int MouseData;
            public int flags;
            public int time;
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// Retrieves the cursor's position, in screen coordinates.
        /// </summary>
        /// <see>See MSDN documentation for further information.</see>
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, ref Rect rectangle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr ProcessId);
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        
        #endregion
        
        public Wnd_Main()
        {
            InitializeComponent();

            _This = this;

            /*** getting optionnal args ***/
            string[] args = Environment.GetCommandLineArgs();
            bool resX = false;
            bool resY = false;
            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i].ToLower().StartsWith("-resx="))
                    {
                        if (!int.TryParse(args[i].Substring(6), out _ClientWidth))
                        {
                            WriteLog("Error parsing -resX client size argument, value set to default (800)");
                        }
                        resX = true;
                    }

                    if (args[i].ToLower().StartsWith("-resy="))
                    {
                        if (!int.TryParse(args[i].Substring(6), out _ClientHeight))
                        {
                            WriteLog("Error parsing -resY client size argument, value set to default (600)");
                        }
                        resY = true;
                    }                    
                }
                if (resX && resY)
                {
                    WriteLog("Game's window client size manually set to " + _ClientWidth + "x" + _ClientHeight);
                    _AutoClientSize = false;
                }
            }
            else
            {
                WriteLog("Game's window client size calculation : Automatic mode");
            }
            
            //Install system-wide mouse hook to intercep movements and buttons
            ApplyMouseHook();

            //Starting ProcessHooking Timer
            _tProcess = new Timer();
            _tProcess.Interval = 500;
            _tProcess.Tick += new EventHandler(tProcess_Tick);
            _tProcess.Enabled = true;
            _tProcess.Start();
            WriteLog("Waiting for \"" + _TargetProcess_Name + ".exe\" to hook .....");
        }

        #region TimerProcessHook

        /// <summary>
        /// Timer event when looking for Game's Process (auto-Hook and auto-close)
        /// </summary>
        private void tProcess_Tick(Object Sender, EventArgs e)
        {
            if (!_TargetProcess_Hooked)
            {
                try
                {
                    Process[] processes = Process.GetProcessesByName(_TargetProcess_Name);
                    if (processes.Length > 0)
                    {
                        _TargetProcess = processes[0];
                        _TargetProcess_Handle = _TargetProcess.Handle;
                        _TargetProcess_MemoryBaseAddress = _TargetProcess.MainModule.BaseAddress;

                        //Waiting for the game's MainWidowHandle to be created too
                        //If not, we can't get client size automatically
                        if (_TargetProcess_MemoryBaseAddress != IntPtr.Zero && _TargetProcess.MainWindowHandle != IntPtr.Zero)
                        {
                            _TargetProcess_Hooked = true;
                            WriteLog("Attached to Process " + _TargetProcess_Name + ".exe, ProcessHandle = " + _TargetProcess_Handle);
                            SetHack();
                            Bgw_Mouse.RunWorkerAsync();
                        }
                    }
                }
                catch
                {
                    //WriteLog("Error trying to hook " + _Target_Process_Name + ".exe");
                }
            }
            else
            {
                Process[] processes = Process.GetProcessesByName(_TargetProcess_Name);
                if (processes.Length <= 0)
                {
                    _TargetProcess_Hooked = false;
                    _TargetProcess = null;
                    _TargetProcess_Handle = IntPtr.Zero;
                    _TargetProcess_MemoryBaseAddress = IntPtr.Zero;
                    WriteLog(_TargetProcess_Name + ".exe closed");
                }
            }
        }

        #endregion

        #region BackgroundWorker

        private void BgwMouse_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                _BgwErrorMsg = string.Empty;
                Debug_MouseScreen = string.Empty;
                Debug_MouseClient = string.Empty;
                Debug_MouseJoystick = string.Empty;
                Debug_ClientSize = string.Empty;
                
                //Getting On-screen Position
                Debug_MouseScreen = "X=" + _MouseScreenPosition.X.ToString() + ", Y=" + _MouseScreenPosition.Y.ToString();
                Bgw_Mouse.ReportProgress(0);
                                   
                if (_TargetProcess_Hooked)
                {
                    //Getting game's client size 
                    if (_AutoClientSize)
                    {
                        try
                        {
                            Rect TotalRes = new Rect();
                            GetClientRect(_TargetProcess.MainWindowHandle, ref TotalRes);
                            _ClientWidth = TotalRes.Right - TotalRes.Left;
                            _ClientHeight = TotalRes.Bottom - TotalRes.Top;
                        }
                        catch (Exception Ex)
                        {
                            _BgwErrorMsg = Ex.Message.ToString();
                            _BgwErrorSrc = Ex.Source.ToString();
                            _BgwErrorStk = Ex.StackTrace.ToString();
                        }
                        Debug_ClientSize = _ClientWidth + "x" + _ClientHeight + "  [Automatic Mode]";
                    }
                    else
                    {
                        Debug_ClientSize = _ClientWidth + "x" + _ClientHeight + "  [Manual Mode]";
                    }                    
                    Bgw_Mouse.ReportProgress(1);                    
                    
                    
                    //Getting On-client cursor position
                    _BgwErrorMsg = string.Empty;
                    try
                    {
                        _MouseClientPosition.X = _MouseScreenPosition.X;
                        _MouseClientPosition.Y = _MouseScreenPosition.Y;
                        ScreenToClient(_TargetProcess.MainWindowHandle, ref _MouseClientPosition);
                        Debug_MouseClient = "X=" + _MouseClientPosition.X.ToString() + ", Y=" + _MouseClientPosition.Y.ToString();
                    }
                    catch (Exception Ex)
                    {
                        _BgwErrorMsg = Ex.Message.ToString();
                        _BgwErrorSrc = Ex.Source.ToString();
                        _BgwErrorStk = Ex.StackTrace.ToString();
                    }
                    Bgw_Mouse.ReportProgress(2);  

                    //Convert to game compatible Axis value
                    _BgwErrorMsg = string.Empty;
                    try
                    {    
                        //X => [0-FFFF] = 65535
                        //Y => [0-FFFF] = 65535
                        double dMaxX = 65535.0;
                        double dMaxY = 65535.0;
                        _MouseInGamePosition.X = Convert.ToInt32(Math.Round(dMaxX * _MouseClientPosition.X / _ClientWidth));
                        _MouseInGamePosition.Y = Convert.ToInt32(Math.Round(dMaxY * _MouseClientPosition.Y / _ClientHeight));

                        if (_MouseInGamePosition.X < 0)
                            _MouseInGamePosition.X = 0;
                        if (_MouseInGamePosition.Y < 0)
                            _MouseInGamePosition.Y = 0;
                        if (_MouseInGamePosition.X > (int)dMaxX)
                            _MouseInGamePosition.X = (int)dMaxX;
                        if (_MouseInGamePosition.Y > (int)dMaxY)
                            _MouseInGamePosition.Y = (int)dMaxY;

                        Debug_MouseJoystick = "X=" + _MouseInGamePosition.X.ToString() + ", Y=" + _MouseInGamePosition.Y.ToString();

                        byte[] bufferX = { (byte)(_MouseInGamePosition.X & 0xFF), (byte)(_MouseInGamePosition.X >> 8) };
                        byte[] bufferY = { (byte)(_MouseInGamePosition.Y & 0xFF), (byte)(_MouseInGamePosition.Y >> 8) };

                        //Write Axis value to memory
                        WriteBytes((int)_TargetProcess_MemoryBaseAddress + _P1_X_Offset, bufferX);
                        WriteBytes((int)_TargetProcess_MemoryBaseAddress + _P1_Y_Offset, bufferY);
                    }
                    catch (Exception Ex)
                    {
                        _BgwErrorMsg = Ex.Message.ToString();
                        _BgwErrorSrc = Ex.Source.ToString();
                        _BgwErrorStk = Ex.StackTrace.ToString();
                    }
                    Bgw_Mouse.ReportProgress(3);                    
                }

                System.Threading.Thread.Sleep(10);
            }
        }

        private void BgwMouse_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                Lbl_Screen.Text = Debug_MouseScreen;
            }
            else if (e.ProgressPercentage == 1)
            {
                Lbl_ClientSize.Text = Debug_ClientSize;
                /*if (_BgwErrorMsg != string.Empty)
                {
                    WriteLog("-----GetClientRect API error--------");
                    WriteLog(_BgwErrorMsg);
                    WriteLog(_BgwErrorStk);
                    WriteLog("");
                }*/
            }  
            
            else if (e.ProgressPercentage == 2)
            {
                Lbl_Client.Text = Debug_MouseClient;
                /*if (_BgwErrorMsg != string.Empty)
                {
                    WriteLog("-----ScreenToClient API error-----");
                    WriteLog(_BgwErrorMsg);
                    WriteLog(_BgwErrorStk);
                    WriteLog("");
                }*/
            }
            else if (e.ProgressPercentage == 3)
            {
                
                Lbl_Joystick.Text = Debug_MouseJoystick;
                /*if (_BgwErrorMsg != string.Empty)
                {
                    WriteLog("-----Game coordinate calculation error--------");
                    WriteLog(_BgwErrorMsg);
                    WriteLog(_BgwErrorStk);
                    WriteLog("");
                }*/
            }   
        }

        private void BgwMouse_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //Never happening
        }

        #endregion
        
        #region MemoryHack

        /*** 
         * This memory hack will just NOP game's instruction modifying: 
         * - Axis values for lightgun 
         * - Buttons states
        ***/
        private void SetHack()
        {
            SetNops((int)_TargetProcess_MemoryBaseAddress, _X_NOP_Offset);
            SetNops((int)_TargetProcess_MemoryBaseAddress, _Y_NOP_Offset);
            SetNops((int)_TargetProcess_MemoryBaseAddress, _BTN_NOP_Offset_1);
            SetNops((int)_TargetProcess_MemoryBaseAddress, _BTN_NOP_Offset_2);
            SetNops((int)_TargetProcess_MemoryBaseAddress, _BTN_NOP_Offset_3);
            SetNops((int)_TargetProcess_MemoryBaseAddress, _BTN_NOP_Offset_4);
            WriteLog("Memory Hack complete !");
            WriteLog("-");
        }

        /// <summary>
        /// Replace given instruction by NOP
        /// </summary>
        /// <param name="BaseAddress">Process memory base address</param>
        /// <param name="OffsetAndNumber">Process memory offset to NOP and it's length. Format: "OFFSET|LENGTH"</param>
        private void SetNops(int BaseAddress, string OffsetAndNumber)
        {
            if (OffsetAndNumber != null)
            {
                try
                {
                    int n = int.Parse((OffsetAndNumber.Split('|'))[1]);
                    int address = int.Parse((OffsetAndNumber.Split('|'))[0].Substring(3).Trim(), NumberStyles.HexNumber);
                    for (int i = 0; i < n; i++)
                    {
                        WriteByte(BaseAddress + address + i, 0x90);
                    }
                }
                catch
                {
                    WriteLog("Can't apply NOP : " + OffsetAndNumber);
                }
            }
        }

        /*** Memory Bytes manipulation ****/
        private static void Apply_OR_ByteMask(int MemoryAddress, byte Mask)
        {
            byte b = ReadByte(MemoryAddress);
            b |= Mask;
            WriteByte(MemoryAddress, b);
        }
        private static void Apply_AND_ByteMask(int MemoryAddress, byte Mask)
        {
            byte b = ReadByte(MemoryAddress);
            b &= Mask;
            WriteByte(MemoryAddress, b);
        }
        private static Byte ReadByte(int Address)
        {
            byte[] Buffer = { 0 };
            int bytesRead = 0;
            if (!ReadProcessMemory((int)_TargetProcess_Handle, Address, Buffer, 1, ref bytesRead))
            {
                WriteLog("Cannot read memory at address 0x" + Address.ToString("X8"));
            }
            return Buffer[0];
        }
        private static Byte[] ReadBytes(int Address, int Bytes)
        {
            byte[] Buffer = new byte[Bytes];
            int bytesRead = 0;
            if (!ReadProcessMemory((int)_TargetProcess_Handle, Address, Buffer, Buffer.Length, ref bytesRead))
            {
                WriteLog("Cannot read memory at address 0x" + Address.ToString("X8"));
            }
            return Buffer;
        }
        private static bool WriteByte(int Address, byte Value)
        {
            int bytesWritten = 0;
            Byte[] Buffer = { Value };
            if (WriteProcessMemory((int)_TargetProcess_Handle, Address, Buffer, 1, ref bytesWritten))
            {
                if (bytesWritten == 1)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }
        private static bool WriteBytes(int Address, byte[] Buffer)
        {
            int bytesWritten = 0;
            if (WriteProcessMemory((int)_TargetProcess_Handle, Address, Buffer, Buffer.Length, ref bytesWritten))
            {
                if (bytesWritten == Buffer.Length)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        #endregion

        #region Screen

        public void GetScreenResolution()
        {
            _ScreenWidth = Screen.PrimaryScreen.Bounds.Width;
            _ScreenHeight = Screen.PrimaryScreen.Bounds.Height;
        }

        #endregion
        
        #region MouseHook

        private static void ApplyMouseHook()
        {
            _MouseHookID = SetHook(ll_mouse_proc, _TargetProcess);
        }
        private static IntPtr SetHook(LowLevelMouseProc proc, Process TargetProcess)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                //System Wide Hook
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelMouseProc ll_mouse_proc = new LowLevelMouseProc(MouseHook_HookCallback);
        private static IntPtr MouseHook_HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0) 
            {
                if ((UInt32)wParam == WM_MOUSEMOVE)
                {
                    MSLLHOOKSTRUCT s = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    _MouseScreenPosition.X = s.pt.X;
                    _MouseScreenPosition.Y = s.pt.Y;
                }
                else if ((UInt32)wParam == WM_LBUTTONDOWN)
                {
                    if (_TargetProcess_Hooked)
                        Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0x10);
                }
                else if ((UInt32)wParam == WM_LBUTTONUP)
                {
                    if (_TargetProcess_Hooked)
                        Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0xEF);
                }
                else if ((UInt32)wParam == WM_MBUTTONDOWN)
                {
                    if (_TargetProcess_Hooked)
                        Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0x20);
                }
                else if ((UInt32)wParam == WM_MBUTTONUP)
                {
                    if (_TargetProcess_Hooked)
                        Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0xDF);
                }
                else if ((UInt32)wParam == WM_RBUTTONDOWN)
                {
                    if (_TargetProcess_Hooked)
                        Apply_OR_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0x40);
                }
                else if ((UInt32)wParam == WM_RBUTTONUP)
                {
                    if (_TargetProcess_Hooked)
                        Apply_AND_ByteMask((int)_TargetProcess_MemoryBaseAddress + _P1_BTN_Offset, 0xBF);
                }
            }
            return CallNextHookEx(_MouseHookID, nCode, wParam, lParam);
        }

        #endregion

        #region Logger

        private static void WriteLog(String Data)
        {
            _This.Txt_Log.Text += DateTime.Now.ToString("[HH:mm:ss] ") + Data + "\n";
        }

        #endregion        

    }

}
