using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace hcitool
{
    class Program
    {
        static ManualResetEventSlim waitHandle = new ManualResetEventSlim(false);
        static string hex = "[0-9a-fA-F]";
        static string byteInHex = hex + hex;
        static string mac = string.Join(@"\:", byteInHex, byteInHex, byteInHex, byteInHex, byteInHex, byteInHex);
        static Regex btIdRegex = new Regex(@"^BluetoothLE\#BluetoothLE" + mac + "-(?<mac>" + mac + ")$", RegexOptions.Compiled);

        static int Main(string[] args)
        {
            if (args.Length != 1 || !args[0].Equals("lescan", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Only supports lescan option");
                return 1;
            }
            var deviceWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelector());
            deviceWatcher.Added += DeviceWatcher_Added;
            //deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            Trace.WriteLine("Enumerating devices");
            deviceWatcher.Start();
            waitHandle.Wait();
            Trace.WriteLine("Done");
            deviceWatcher.Stop();
            return 0;
        }

        private static void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            waitHandle.Set();
        }

        //private static void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        //{
        //    Console.WriteLine(System.Threading.Thread.CurrentThread.ManagedThreadId);
        //}

        private static void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            var match = btIdRegex.Match(args.Id);
            if (match.Success)
            {
                var mac = match.Groups["mac"].Value;
                Console.WriteLine(mac + " " + args.Name);
            }
        }
    }
}
