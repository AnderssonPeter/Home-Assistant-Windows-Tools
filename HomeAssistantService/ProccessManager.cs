using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;
using YamlDotNet.RepresentationModel;

namespace HomeAssistantService
{
    class ProccessManager
    {
        readonly object Locker = new object();
        int serverPort = 8123;
        string apiPassword = null;
        Process process = null;
        Timer timer;

        public void Start()
        {
            Console.WriteLine("Start");
            lock (Locker)
            {
                try
                {
                    var configurationFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".homeassistant", "configuration.yaml");
                    using (var reader = new StreamReader(configurationFile, Encoding.UTF8))
                    {
                        var yaml = new YamlStream();
                        yaml.Load(reader);
                        var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                        var httpNode = (YamlMappingNode)rootNode.Children[new YamlScalarNode("http")];

                        if (httpNode.Children.TryGetValue(new YamlScalarNode("server_port"), out YamlNode serverPortNode))
                            serverPort = int.Parse(((YamlScalarNode)serverPortNode).Value);
                        if (httpNode.Children.TryGetValue(new YamlScalarNode("api_password"), out YamlNode apiPasswordNode))
                            apiPassword = ((YamlScalarNode)apiPasswordNode).Value;
                    }
                    var pythonPath = GetPythonExecutable();
                    if (string.IsNullOrEmpty(pythonPath))
                        throw new InvalidOperationException("Could not find python");
                    var startInfo = new ProcessStartInfo() { FileName = pythonPath,
                                                             Arguments = "-m homeassistant",
                                                             UseShellExecute = false,
                                                             RedirectStandardInput = true,
                                                             RedirectStandardOutput = true,
                                                             RedirectStandardError = true };
                    var applcationDirectory = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                    var toolsDirectory = Path.Combine(applcationDirectory, "Tools");
                    var gatttoolDirectory = Path.Combine(toolsDirectory, "gatttool");
                    var hcitoolDirectory = Path.Combine(toolsDirectory, "hcitool");
                    startInfo.EnvironmentVariables["path"] += ";" + gatttoolDirectory + ";" + hcitoolDirectory + ";";
                    process = Process.Start(startInfo);

                    if (process == null || process.HasExited)
                        throw new ArgumentException("Failed to start home assistant");
                    //give it 5 seconds to boot
                    if (process.WaitForExit(5 * 1000))
                        throw new ArgumentException("Failed to start home assistant");
                    if (!CheckIfAlive())
                        throw new ArgumentException("Home assistant is not responding");
                    timer = new Timer(CheckState, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    //We failed to start the process properly
                    if (process != null)
                    {
                        try
                        {
                            Stop();
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine(ex2.ToString());
                            //Ignore errors!
                        }
                    }
                    throw;
                }
            }
            Console.WriteLine("Started");
        }

        private void CheckState(object state)
        {
            Console.WriteLine("CheckState");
            bool needToRestart = false;
            lock (Locker)
            {
                if (!CheckIfAlive())
                    needToRestart = true;
            }
            if (needToRestart)
            {
                Console.WriteLine("Restarting");
                Stop();
                Start();
            }

        }

        public void Stop()
        {
            Console.WriteLine("Stop");
            lock (Locker)
            {
                Console.WriteLine("Stopping");
                if (timer != null)
                {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    timer.Dispose();
                    timer = null;
                }

                if (process == null)
                    return;
                if (process.HasExited)
                {
                    process.Dispose();
                    process = null;
                    return;
                }

                //Send shutdown signal with api!
                var result = MakeApiRequest("services/homeassistant/stop", HttpMethod.Post).GetAwaiter().GetResult();
                //give it 3 seconds to shut down on its own.
                if (!process.WaitForExit(3000))
                {
                    process.CloseMainWindow();
                    //Give it 2 more seconds to shutdown on its own.
                    if (!process.WaitForExit(2000))
                        process.Kill();
                }
                process.Dispose();
                process = null;
            }
        }

        private bool CheckIfAlive()
        {
            try
            {
                Console.WriteLine("CheckIfAlive");
                var result = MakeApiRequest(string.Empty, HttpMethod.Get).GetAwaiter().GetResult();
                if (result == null)
                    return false;
                //Do a api call here later
                return result.IndexOf("API running.", StringComparison.OrdinalIgnoreCase) > 0;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            catch(System.Net.Sockets.SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        private async Task<string> MakeApiRequest(string path, HttpMethod method)
        {
            try
            {
                Console.WriteLine($"MakeApiRequest({path})");
                var url = $"http://localhost:{serverPort}/api/" + path;
                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri(url),
                        Method = method
                    };
                    if (!string.IsNullOrEmpty(apiPassword))
                        request.Headers.Add("x-ha-access", apiPassword);
                    var result = await client.SendAsync(request);
                    return await result.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
            catch (System.Net.WebException ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        string GetPythonExecutable(int major = 3)
        {
            Console.WriteLine("GetPythonExecutable");
            var software = "SOFTWARE";
            var key = Registry.CurrentUser.OpenSubKey(software);
            if (key == null)
                key = Registry.LocalMachine.OpenSubKey(software);
            if (key == null)
                return null;

            var pythonCoreKey = key.OpenSubKey(@"Python\PythonCore");
            if (pythonCoreKey == null)
                pythonCoreKey = key.OpenSubKey(@"Wow6432Node\Python\PythonCore");
            if (pythonCoreKey == null)
                return null;

            var pythonVersionRegex = new Regex("^" + major + @"\.(\d+)-(\d+)$");
            var targetVersion = pythonCoreKey.GetSubKeyNames().
                                                Select(n => pythonVersionRegex.Match(n)).
                                                Where(m => m.Success).
                                                OrderByDescending(m => int.Parse(m.Groups[1].Value)).
                                                ThenByDescending(m => int.Parse(m.Groups[2].Value)).
                                                Select(m => m.Groups[0].Value).First();

            var installPathKey = pythonCoreKey.OpenSubKey(targetVersion + @"\InstallPath");
            if (installPathKey == null)
                return null;

            return (string)installPathKey.GetValue("ExecutablePath");
        }
    }
}
