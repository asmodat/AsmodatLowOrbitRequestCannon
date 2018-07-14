using System;
using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Threading;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;

namespace AsmodatLowOrbitRequestCannon
{
    public class OpenVPN : IDisposable
    {
        public enum Signal
        {
            Hup,
            Term,
            Usr1,
            Usr2
        }

        private Socket socket;
        private const int bufferSize = 1024;
        private readonly Process prc = new Process();
        private readonly string openVpnExePath;

        private void RunOpenVpnProcess(string config, string eventName = "MyOpenVpnEvent")
        {
            var run = $"--config \"{config}\" --service {eventName} 0";
            prc.StartInfo.CreateNoWindow = true;
            prc.EnableRaisingEvents = true;
            prc.StartInfo.Arguments = run;
            prc.StartInfo.FileName = openVpnExePath;
            prc.Start();
        }

        public OpenVPN(string host, int port, string[] ovpn, string openVpnExeFileName)
        {
            if (ovpn.IsNullOrEmpty())
                throw new ArgumentNullException("ovpn");

            this.openVpnExePath = openVpnExeFileName;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "ovpn");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var stageFile = Path.Combine(dir, $"{host}-{port}.ovpn");

            if (!File.Exists(stageFile))
            {
                var idx = Array.FindIndex(ovpn, x => x.StartsWith("management"));
                if (idx >= 0)
                    ovpn[idx] = $"management {host} {port}";
                else
                {
                    var lastIdx = ovpn.Length - 1;
                    var lastLine = ovpn[lastIdx];
                    ovpn[lastIdx] = $"{lastLine}{Environment.NewLine}management {host} {port}";
                }

                File.WriteAllLines(stageFile, ovpn);
            }

            RunOpenVpnProcess(stageFile, $"LOCAL{port}");
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            SendGreeting();
        }

        public OpenVPN(string host, int port, string ovpnFileName, string userName = null, string password = null, string openVpnExeFileName = @"C:\Program Files\OpenVPN\bin\openvpn.exe")
        {
            this.openVpnExePath = openVpnExeFileName;
            if (!string.IsNullOrEmpty(ovpnFileName))
            {
                if (!File.Exists(ovpnFileName))
                    throw new Exception($"OpenVPN (ovpn) config file not found: {ovpnFileName}");

                var ovpnFileContent = File.ReadAllLines(ovpnFileName);

                //management
                var idx = Array.FindIndex(ovpnFileContent, x => x.StartsWith("management"));
                if (idx >= 0)
                {
                    ovpnFileContent[idx] = string.Format("management {0} {1}", host, port);
                }
                else
                {
                    var lastIdx = ovpnFileContent.Length - 1;
                    var lastLine = ovpnFileContent[lastIdx];
                    ovpnFileContent[lastIdx] = string.Format("{0}{1}management {2} {3}", lastLine, Environment.NewLine, host, port);
                }

                //auto login
                var idx2 = Array.FindIndex(ovpnFileContent, x => x.StartsWith("auth-user-pass"));
                if (idx2 >= 0)
                {
                    if (userName == null || password == null)
                    {
                        throw new ArgumentException("Username or password cannot be null");
                    }

                    // create a credentials file
                    var passFileName = Path.Combine(Path.GetTempPath(), "ovpnpass.txt").Replace(@"\", @"\\");
                    File.WriteAllLines(passFileName, new string[] { userName, password });

                    // add its path the ovpn file and write it back to the ovpn file
                    ovpnFileContent[idx2] = string.Format("auth-user-pass {0}", passFileName);
                }
                else
                {
                    if (userName != null || password != null)
                    {
                        throw new ArgumentException("Username or password are provided but the *.ovpn file doesn't have the line 'auth-user-pass'");
                    }
                }

                File.WriteAllLines(ovpnFileName, ovpnFileContent);
                RunOpenVpnProcess(ovpnFileName, "MyOpenVpnEvent");
            }

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            SendGreeting();
        }

        #region Commands
        public string GetStatus() => this.SendCommand("status");
        public string GetState() => this.SendCommand("state");
        public string GetState(int n = 1) => this.SendCommand(string.Format("state {0}", n));
        public string GetStateAll() => this.SendCommand("state all");
        public string SetStateOn() => this.SendCommand("state on");
        public string SetStateOnAll() => this.SendCommand("state on all");
        public string GetStateOff() => this.SendCommand("state off");
        public string GetVersion() => this.SendCommand("version");
        public string GetPid() => this.SendCommand("pid");
        public string SendSignal(Signal sgn) => this.SendCommand($"signal SIG{sgn.ToString().ToUpper()}");
        public string Mute() => this.SendCommand("pid");
        public string GetEcho() => this.SendCommand("echo");
        public string GetHelp() => this.SendCommand("help");
        public string Kill(string name) => this.SendCommand(string.Format("kill {0}", name));
        public string Kill(string host, int port) => this.SendCommand(string.Format("kill {0}:{1}", host, port));
        public string GetNet() => this.SendCommand("net");
        public string GetLogAll() => this.SendCommand("state off");
        public string SetLogOn() => this.SendCommand("log on");
        public string SetLogOnAll() => this.SendCommand("log on all");
        public string SetLogOff() => this.SendCommand("log off");
        public string GetLog(int n = 1) => this.SendCommand(string.Format("log {0}", n));
        public string SendMalCommand() => this.SendCommand("fdsfds");
        private static string TreamRetrievedString(string s) => s.Replace("\0", "");

        private void SendGreeting()
        {
            var bf = new byte[bufferSize];
            int rb = socket.Receive(bf, 0, bf.Length, SocketFlags.None);
            if (rb < 1)
            {
                throw new SocketException();
            }
        }
        #endregion

        private string SendCommand(String cmd)
        {
            socket.Send(Encoding.Default.GetBytes(cmd + "\r\n"));
            var bf = new byte[bufferSize];
            var sb = new System.Text.StringBuilder();
            int rb;
            string str = "";
            while (true)
            {
                Thread.Sleep(100);
                rb = socket.Receive(bf, 0, bf.Length, 0);
                str = Encoding.UTF8.GetString(bf).Replace("\0", "");
                if (rb < bf.Length)
                {
                    if (str.Contains("\r\nEND"))
                    {
                        var a = str.Substring(0, str.IndexOf("\r\nEND"));
                        sb.Append(a);
                    }
                    else if (str.Contains("SUCCESS: "))
                    {
                        var a = str.Replace("SUCCESS: ", "").Replace("\r\n", "");
                        sb.Append(a);
                    }
                    else if (str.Contains("ERROR: "))
                    {
                        var msg = str.Replace("ERROR: ", "").Replace("\r\n", "");
                        throw new ArgumentException(msg);
                    }
                    else
                    {
                        //todo
                        continue;
                    }

                    break;
                }
                else
                {
                    sb.Append(str);
                }
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            if (socket != null)
                SendSignal(Signal.Term);

            socket.Dispose();
            prc.Close();
        }
    }
}
