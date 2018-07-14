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

            if (nArgs["mode"] == "vpn")
                Run2(intensity, parallel, nArgs["ovpn"], nArgs["exe"]);
            else if (nArgs["mode"] == "swixer")
                Run(intensity, batch, parallel, verify);
            else
                throw new Exception("Unknown mode");

            Console.WriteLine("Done");
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

                Parallel.For(29170, 29998, new ParallelOptions() { MaxDegreeOfParallelism = intentisty3 }, i =>
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
