using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AppKit;
using Foundation;
using MissionPlanner.ArduPilot;
using Xamarin.Forms.Platform.MacOS;
using Xamarin.Forms;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using Xamarin.GCSViews;
using Device = Xamarin.Forms.Device;

namespace Xamarin.MacOS
{
    [Register("AppDelegate")]
    public class AppDelegate : FormsApplicationDelegate
    {
        public AppDelegate()
        {
            var style = NSWindowStyle.Closable | NSWindowStyle.Resizable | NSWindowStyle.Titled;
            var rect = new CoreGraphics.CGRect(200,200,1024,768);
            mainWindow = new NSWindow(rect, style, NSBackingStore.Buffered, false);
            mainWindow.Title = "Mission Planner on Mac!";
            mainWindow.TitleVisibility = NSWindowTitleVisibility.Hidden;
            mainWindow.DidResize += MainWindow_DidResize;
            mainWindow.WillClose += MainWindow_WillClose;
            NSEvent.AddLocalMonitorForEventsMatchingMask(
                NSEventMask.KeyDown | NSEventMask.KeyUp | NSEventMask.FlagsChanged, KeyboardEventHandler);
            WinForms.DirectKeyboard = true;
            // SKTouch reports neither hover moves nor the scroll wheel/trackpad, forward those ourselves
            mainWindow.AcceptsMouseMovedEvents = true;
            NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.MouseMoved | NSEventMask.ScrollWheel,
                MouseEventHandler);

            Test.BlueToothDevice = new BTDevice();
            Test.UsbDevices = new USBDevices();
            Test.Radio = new Radio();
            Test.GPS = new GPS();
            Test.SystemInfo = new SystemInfo();
            Test.Speech = new OSXSpeech();

            WinForms.OSX = true;

            Acr.UserDialogs.Infrastructure.Log.Out += (s, s1, arg3) =>
            {
                Console.WriteLine(s + ": " + s1);
            };

            new System.Drawing.android.android();
        }

        // command shortcuts left to macOS: quit, close, hide, minimise, preferences
        private const string SystemShortcuts = "qwhm,";

        /// <summary>
        /// Type straight into the focused WinForms control. Command works as Control does on Windows,
        /// and copy/cut/paste/select all go through the macOS pasteboard, as the driver has none
        /// </summary>
        private NSEvent KeyboardEventHandler(NSEvent keyEvent)
        {
            try
            {
                // leave native text fields and other windows alone
                if (keyEvent.Window != mainWindow || mainWindow.FirstResponder is NSText)
                    return keyEvent;

                var flags = keyEvent.ModifierFlags;
                var shift = flags.HasFlag(NSEventModifierMask.ShiftKeyMask);
                var control = flags.HasFlag(NSEventModifierMask.ControlKeyMask);
                var alt = flags.HasFlag(NSEventModifierMask.AlternateKeyMask);
                var command = flags.HasFlag(NSEventModifierMask.CommandKeyMask);

                WinForms.KeyModifiers(shift, control || command, alt);
                if (keyEvent.Type == NSEventType.FlagsChanged)
                    return keyEvent;

                var down = keyEvent.Type == NSEventType.KeyDown;
                var plain = keyEvent.CharactersIgnoringModifiers?.ToLowerInvariant() ?? "";
                var key = MacKeyToKeys(keyEvent.KeyCode, plain);

                if (command || control)
                {
                    if (command && plain.Length == 1 && SystemShortcuts.Contains(plain))
                        return keyEvent;

                    if (plain == "c" || plain == "x" || plain == "v" || plain == "a")
                    {
                        if (down)
                            Shortcut(plain);
                        return null;
                    }

                    // other shortcuts reach the app as Control+key, with nothing typed
                    WinForms.KeyEvent(key, null, down, keyEvent.IsARepeat);
                    return null;
                }

                WinForms.KeyEvent(key, KeyText(keyEvent), down, down && keyEvent.IsARepeat);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return keyEvent;
            }
        }

        private static void Shortcut(string letter)
        {
            var pasteboard = NSPasteboard.GeneralPasteboard;
            switch (letter)
            {
                case "c":
                case "x":
                    var selected = WinForms.FocusedSelectedText();
                    if (string.IsNullOrEmpty(selected))
                        return;
                    pasteboard.ClearContents();
                    pasteboard.SetStringForType(selected, NSPasteboard.NSPasteboardTypeString);
                    if (letter == "x")
                    {
                        // delete the selection
                        WinForms.KeyEvent(46, null, true, false);
                        WinForms.KeyEvent(46, null, false, false);
                    }
                    break;
                case "v":
                    var text = pasteboard.GetStringForType(NSPasteboard.NSPasteboardTypeString);
                    WinForms.TypeText(text?.Replace("\r", ""));
                    break;
                case "a":
                    WinForms.FocusedSelectAll();
                    break;
            }
        }

        /// <summary>
        /// What the key types, as WM_CHAR wants it. Return, backspace and escape type their control
        /// characters, as on Windows, the arrows and function keys type nothing
        /// </summary>
        private static string KeyText(NSEvent keyEvent)
        {
            switch (keyEvent.KeyCode)
            {
                case 36: // return
                case 76: // keypad enter
                    return "\r";
                case 51: // backspace
                    return "\b";
                case 53: // escape
                    return "\u001b";
                case 48: // tab
                    return null;
            }

            var text = keyEvent.Characters ?? "";
            return new string(text.Where(c => c >= 32 && c != 127 && (c < 0xF700 || c > 0xF8FF)).ToArray());
        }

        /// <summary>
        /// macOS virtual key code to the Windows Forms Keys code, without modifiers
        /// </summary>
        private static int MacKeyToKeys(ushort keyCode, string plain)
        {
            switch (keyCode)
            {
                case 36: case 76: return 13; // Enter
                case 48: return 9; // Tab
                case 51: return 8; // Back
                case 117: return 46; // Delete
                case 53: return 27; // Escape
                case 49: return 32; // Space
                case 123: return 37; // Left
                case 126: return 38; // Up
                case 124: return 39; // Right
                case 125: return 40; // Down
                case 116: return 33; // PageUp
                case 121: return 34; // PageDown
                case 119: return 35; // End
                case 115: return 36; // Home
                case 122: return 112; // F1
                case 120: return 113;
                case 99: return 114;
                case 118: return 115;
                case 96: return 116;
                case 97: return 117;
                case 98: return 118;
                case 100: return 119;
                case 101: return 120;
                case 109: return 121;
                case 103: return 122;
                case 111: return 123; // F12
            }

            if (plain.Length == 0)
                return 0;

            var c = char.ToUpperInvariant(plain[0]);
            if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')
                return c;

            switch (c)
            {
                case ';': return 186; // OemSemicolon
                case '=': return 187; // Oemplus
                case ',': return 188; // Oemcomma
                case '-': return 189; // OemMinus
                case '.': return 190; // OemPeriod
                case '/': return 191; // OemQuestion
                case '`': return 192; // Oemtilde
                case '[': return 219; // OemOpenBrackets
                case '\\': return 220; // OemPipe
                case ']': return 221; // OemCloseBrackets
                case '\'': return 222; // OemQuotes
            }

            return 0;
        }

        // trackpad scrolling left over from the previous event, in points
        private double wheelRemainder;

        private NSEvent MouseEventHandler(NSEvent mouseEvent)
        {
            try
            {
                var view = WinForms.CanvasView == null
                    ? null
                    : global::Xamarin.Forms.Platform.MacOS.Platform.GetRenderer(WinForms.CanvasView)?.NativeView;
                if (view == null || mouseEvent.Window != view.Window)
                    return mouseEvent;

                var point = view.ConvertPointFromView(mouseEvent.LocationInWindow, null);
                var bounds = view.Bounds;
                if (!bounds.Contains(point) || bounds.Width <= 0 || bounds.Height <= 0)
                    return mouseEvent;

                var fx = (double) ((point.X - bounds.X) / bounds.Width);
                var fy = view.IsFlipped
                    ? (double) ((point.Y - bounds.Y) / bounds.Height)
                    : (double) ((bounds.Bottom - point.Y) / bounds.Height);

                if (mouseEvent.Type == NSEventType.MouseMoved)
                {
                    WinForms.MouseHover(fx, fy);
                }
                else if (mouseEvent.Type == NSEventType.ScrollWheel)
                {
                    // a wheel reports lines, one notch each. a trackpad reports points, so gather those up
                    // into notches, as the WinForms controls step a fixed amount per message
                    int notches;
                    var dy = (double) mouseEvent.ScrollingDeltaY;
                    if (mouseEvent.HasPreciseScrollingDeltas)
                    {
                        if (mouseEvent.Phase == NSEventPhase.Began)
                            wheelRemainder = 0;
                        wheelRemainder += dy;
                        notches = (int) (wheelRemainder / 30);
                        wheelRemainder -= notches * 30;
                    }
                    else
                    {
                        notches = dy == 0 ? 0 : Math.Sign(dy) * Math.Max(1, (int) Math.Round(Math.Abs(dy)));
                    }

                    notches = Math.Max(-10, Math.Min(10, notches));
                    for (var i = 0; i < Math.Abs(notches); i++)
                        WinForms.MouseWheel(fx, fy, Math.Sign(notches) * 120);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return mouseEvent;
        }

        private void MainWindow_WillClose(object sender, EventArgs e)
        {
            Console.WriteLine("MainWindow_WillClose");
            WinForms.Exit();
            System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow();
        }

        private void MainWindow_DidResize(object sender, EventArgs e)
        {
            Console.WriteLine("MainWindow_DidResize " + mainWindow.Frame.Size);
            WinForms.Resize((int)mainWindow.Frame.Size.Width, (int)mainWindow.Frame.Size.Height);
        }

        private  NSWindow mainWindow;

        public override NSWindow MainWindow { get => mainWindow; }

        public override void DidFinishLaunching(NSNotification notification)
        {
            // Insert code here to initialize your application
            Forms.Forms.Init();
            LoadApplication(new Xamarin.App());// new App());
            base.DidFinishLaunching(notification);
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
        }

        
        public class Radio : IRadio
        {
            public void Toggle()
            {
          
            }
        }

        public class BTDevice : IBlueToothDevice
        {
            public async Task<List<DeviceInfo>> GetDeviceInfoList()
            {
                return new List<DeviceInfo>();
            }

            public Task<ICommsSerial> GetBT(DeviceInfo first)
            {
                throw new NotImplementedException();
            }
        }

        public class USBDevices : IUSBDevices
        {
            public event EventHandler<DeviceInfo> USBEvent;

            public DeviceInfo GetDeviceInfo(object devicein)
            {
                throw new NotImplementedException();
            }

            public void USBEventCallBack(object usbDeviceReceiver, object device)
            {
                throw new NotImplementedException();
            }

            public async Task<ICommsSerial> GetUSB(DeviceInfo di)
            {
                var data = GetUSBList();

                var devs = GetDevList().Split(new[] { '\r', '\n' });

                var regex = new Regex(@"-o\s+([\w\s]+@.{4}).*\n.*idProduct""\s+=\s+(.*)\n.*\n.*idVendor""\s+=\s+(.*)");

                Console.WriteLine("Looking for " + di.board);

                foreach (Match match in regex.Matches(data))
                {
                    var name = match.Groups[1].Value;
                    var loc = name.Split("@").Last();
                    var pid = int.Parse(match.Groups[2].Value);
                    var vid = int.Parse(match.Groups[3].Value);
                    
                    foreach (var dev in devs)
                    {
                        Console.WriteLine("usb location {0} == {1} && {2}.Contains({3})", di.board, name, dev.ToLower(),
                            loc.ToLower());
                        if (di.board == name && dev.ToLower().Contains(loc.ToLower()))
                        {
                            Console.WriteLine("GetUSB: Port: " + dev.TrimEnd());
                            return new SerialPort(dev.TrimEnd());
                        }
                    }
                }

                return null;
            }

            async Task<List<DeviceInfo>> IUSBDevices.GetDeviceInfoList()
            {
                var ans =  new List<DeviceInfo>();

                var data = GetUSBList();

                var devs = GetDevList().Split(new[] { '\r', '\n' });

                var regex = new Regex(@"-o\s+([\w\s]+@.{4}).*\n.*idProduct""\s+=\s+(.*)\n.*\n.*idVendor""\s+=\s+(.*)");

                foreach (Match match in regex.Matches(data))
                {
                    var name = match.Groups[1].Value;
                    var loc = name.Split("@").Last();
                    var pid = int.Parse(match.Groups[2].Value);
                    var vid = int.Parse(match.Groups[3].Value);

                    var deviceInfo = new DeviceInfo()
                    {
                        board = name,
                        description = name,
                        hardwareid = String.Format("USB\\VID_{0:X4}&PID_{1:X4}&", vid, pid),
                        name = name
                    };

                    foreach (var dev in devs)
                    {
                        // only add ports where we find a matching tty dev
                        if (deviceInfo.board == name && dev.ToLower().Contains(loc.ToLower()))
                            ans.Add(deviceInfo);
                    }
                }

                return ans;
            }

            private static string GetDevList()
            {
                var proc = System.Diagnostics.Process.Start("bash",
                    @"-c ""ls /dev/tty.* > /tmp/dev.list""");
                proc.WaitForExit();

                var data = File.ReadAllText("/tmp/dev.list");
                return data;
            }

        private static string GetUSBList()
            {
                var proc = System.Diagnostics.Process.Start("bash",
                    @"-c ""ioreg -p IOUSB -w0 -l | grep -E '@|idVendor|idProduct|bcdDevice' > /tmp/usb.list""");
                proc.WaitForExit();

                var data = File.ReadAllText("/tmp/usb.list");
                return data;
            }
        }
    }

    public class OSXSpeech : ISpeech
    {
        private NSSpeechSynthesizer speechSynthesizer = new NSSpeechSynthesizer();

        public bool speechEnable { get; set; }
        public bool IsReady { get; set; }
        public void SpeakAsync(string text)
        {
            if (!speechEnable)
                return;
            
            _ = Task.Run(() =>
            {
                IsReady = false;
                try
                {
                    //NSRunLoop.Main.BeginInvokeOnMainThread();
                    Device.InvokeOnMainThreadAsync(() => speechSynthesizer.StartSpeakingString(text));
                }
                catch
                {
                }
                IsReady = true;
            });
        }

        public void SpeakAsyncCancelAll()
        {
            Device.InvokeOnMainThreadAsync(() => speechSynthesizer.StopSpeaking());
        }
    }
}