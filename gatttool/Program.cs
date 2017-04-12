using Fclp;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace gatttool
{
    class Program
    {
        static ManualResetEventSlim waitHandle = new ManualResetEventSlim(false);
        static List<string> deviceIds = new List<string>();
        static string hex = "[0-9a-fA-F]";
        static string byteInHex = hex + hex;
        static string mac = string.Join(@"\:", byteInHex, byteInHex, byteInHex, byteInHex, byteInHex, byteInHex);
        static Regex btIdRegex = new Regex(@"^BluetoothLE\#BluetoothLE" + mac + "-(?<mac>" + mac +")$", RegexOptions.Compiled);
        static Regex macRegex = new Regex(@"^" + mac + "$", RegexOptions.Compiled);
        static Regex handleRegex = new Regex(@"^0x" + hex + "+$", RegexOptions.Compiled);
        static Regex hexRegex = new Regex(@"^(" + byteInHex + ")+$", RegexOptions.Compiled);
        static Parameters parameters;

        static int Main(string[] args)
        {
            if (!ParseCommandLine(args))
                return 1;
            if (!AsyncContext.Run(() => MainAsync()))
            {
                return 1;
            }
            return 0;
        }

        static bool ParseCommandLine(string[] args)
        {
            var p = new FluentCommandLineParser<Parameters>();
            p.Setup(arg => arg.DeviceMac).As('b', "device").Required();
            p.Setup(arg => arg.CharacteristicHandle).As('a', "handle").Required();
            p.Setup(arg => arg.Read).As("char-read").SetDefault(false);
            p.Setup(arg => arg.Write).As("char-write-req").SetDefault(false);
            p.Setup(arg => arg.WriteValue).As('n', "value").SetDefault(string.Empty);

            var result = p.Parse(args);

            if (result.Errors.Any())
            {
                Console.WriteLine(result.ErrorText);
                return false;
            }

            parameters = p.Object;
            if (!macRegex.IsMatch(parameters.DeviceMac))
            {
                Console.WriteLine("Invalid mac address provided");
                return false;
            }

            if (!handleRegex.IsMatch(parameters.CharacteristicHandle))
            {
                Console.WriteLine("Invalid characteristic handle provided");
                return false;
            }

            if (!parameters.Write && !parameters.Read)
            {
                Console.WriteLine("Must either set read or write mode!");
                return false;
            }

            if (parameters.Write && parameters.Read)
            {
                Console.WriteLine("Can't set both read and write mode!");
                return false;
            }

            if (parameters.Write && string.IsNullOrWhiteSpace(parameters.WriteValue))
            {
                Console.WriteLine("When in write mode write value must be provided!");
                return false;
            }

            if (parameters.Write && !hexRegex.IsMatch(parameters.WriteValue))
            {
                Console.WriteLine("Write value is invalid must be hex!");
                return false;
            }

            return true;
        }

        static async Task<bool> MainAsync()
        {
            try
            {
                var deviceWatcher = DeviceInformation.CreateWatcher(BluetoothLEDevice.GetDeviceSelector());
                deviceWatcher.Added += DeviceWatcher_Added;
                //deviceWatcher.Updated += DeviceWatcher_Updated;
                deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
                Trace.WriteLine("Enumerating devices");
                deviceWatcher.Start();
                waitHandle.Wait();
                Trace.WriteLine("Done");
                deviceWatcher.Stop();

                if (deviceIds.Count == 0)
                {
                    Console.WriteLine("Failed to find device with corret mac address");
                    return false;
                }
                if (deviceIds.Count > 1)
                {
                    Console.WriteLine("Found multiple devices with mac address");
                    return false;
                }
                var device = await BluetoothLEDevice.FromIdAsync(deviceIds.First());

                //Look up uuid
                var characteristicHandle = int.Parse(parameters.CharacteristicHandle.Substring(2), NumberStyles.HexNumber);
                var characteristic = device.GattServices.
                                            SelectMany(s => s.GetAllCharacteristics()).
                                            FirstOrDefault(c => c.AttributeHandle == characteristicHandle);

                if (characteristic == null)
                {
                    Console.WriteLine("Failed to find characteristic");
                    return false;
                }
                if (parameters.Read)
                {
                    return await ReadCharacteristic(characteristic);
                }
                else
                {
                    //Parse write value
                    var data = StringToByteArray(parameters.WriteValue);
                    return await WriteCharacteristic(characteristic, data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
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
                if (mac.Equals(parameters.DeviceMac, StringComparison.OrdinalIgnoreCase))
                    deviceIds.Add(args.Id);
            }
        }

        public static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private static async Task<bool> ReadCharacteristic(GattCharacteristic characteristic)
        {
            var value = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (value.Status == GattCommunicationStatus.Success)
            {
                var reader = Windows.Storage.Streams.DataReader.FromBuffer(value.Value);
                var buffer = new byte[value.Value.Length];
                reader.ReadBytes(buffer);
                Console.WriteLine($"Characteristic value/descriptor: {BitConverter.ToString(buffer).Replace("-", " ")}");
                return true;
            }
            else
            {
                Console.WriteLine("connect error: Transport endpoint is not connected (107)");
                return false;
            }
        }
        private static async Task<bool> WriteCharacteristic(GattCharacteristic characteristic, byte[] data)
        {
            var writer = new Windows.Storage.Streams.DataWriter();
            writer.WriteBytes(data);
            var result = await characteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
            if (result == GattCommunicationStatus.Success)
            {
                Console.WriteLine("Characteristic value was written successfully");
                return true;
            }
            else
            {
                Console.WriteLine("connect error: Transport endpoint is not connected (107)");
                return false;
            }
        }
    }
}
