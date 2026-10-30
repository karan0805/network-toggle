using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NetworkToggle
{
    public enum LinkKind { Ethernet, WiFi, Tethering }

    public class Link
    {
        public string Id;
        public string Alias;
        public string Title;
        public LinkKind Kind;
        public bool Connected;
        public long Speed;
        public NetworkInterface Adapter;
    }

    static class Network
    {
        const string TestAddress = "1.1.1.1";
        const int PreferredMetric = 1;
        const int StandbyMetric = 50;
        const int MaxLinks = 5;

        static readonly string[] SkippedAdapters = { "Virtual", "Bluetooth", "Hyper-V", "TAP", "VPN", "Loopback" };
        static readonly string[] PhoneAdapters = { "Apple Mobile Device", "iPhone", "Remote NDIS", "RNDIS", "Internet Sharing", "Tether", "Android" };

        static bool Mentions(string text, IEnumerable<string> words)
        {
            return words.Any(word => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static bool IsSkipped(string description)
        {
            return Mentions(description, SkippedAdapters);
        }

        static bool IsListed(NetworkInterface adapter)
        {
            return (adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                && !IsSkipped(adapter.Description);
        }

        internal static LinkKind Classify(NetworkInterfaceType type, string description)
        {
            if (type == NetworkInterfaceType.Wireless80211) return LinkKind.WiFi;
            return Mentions(description, PhoneAdapters) ? LinkKind.Tethering : LinkKind.Ethernet;
        }

        internal static string TitleFor(LinkKind kind, string description)
        {
            if (kind == LinkKind.WiFi) return "Wi-Fi";
            if (kind == LinkKind.Tethering) return "USB Tethering";
            return Mentions(description, new[] { "USB" }) ? "USB Ethernet" : "Ethernet";
        }

        static bool HasGateway(NetworkInterface adapter)
        {
            return adapter != null
                && adapter.OperationalStatus == OperationalStatus.Up
                && adapter.GetIPProperties().GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
        }

        static Link ToLink(NetworkInterface adapter)
        {
            LinkKind kind = Classify(adapter.NetworkInterfaceType, adapter.Description);
            return new Link
            {
                Id = adapter.Id,
                Alias = adapter.Name,
                Title = TitleFor(kind, adapter.Description),
                Kind = kind,
                Connected = HasGateway(adapter),
                Speed = adapter.Speed,
                Adapter = adapter
            };
        }

        public static List<Link> Links()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsListed)
                .Select(ToLink)
                .Where(link => link.Connected || link.Kind == LinkKind.WiFi)
                .OrderBy(link => link.Kind)
                .ThenBy(link => link.Alias)
                .Take(MaxLinks)
                .ToList();
        }

        public static string ActiveId()
        {
            IPAddress local;
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(IPAddress.Parse(TestAddress), 53);
                    local = ((IPEndPoint)socket.LocalEndPoint).Address;
                }
            }
            catch (SocketException)
            {
                return null;
            }

            NetworkInterface active = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
                n.GetIPProperties().UnicastAddresses.Any(u => u.Address.Equals(local)));
            return active == null ? null : active.Id;
        }

        public static string Use(Link link)
        {
            List<string> others = ListedAliases().Where(alias => alias != link.Alias).ToList();
            RunPowerShell(PriorityScript(link.Alias, others));
            return WaitFor(() => ActiveId() == link.Id, 5) ? null : "Windows did not switch. Please try again.";
        }

        public static void RestoreDefaults()
        {
            RunPowerShell(RestoreScript(ListedAliases()));
        }

        public static string OpenWifiList()
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-availablenetworks:") { UseShellExecute = true });
                return "Pick a network in the Windows Wi-Fi list.";
            }
            catch (Win32Exception)
            {
                return "Connect to Wi-Fi from the Windows taskbar.";
            }
        }

        static IEnumerable<string> ListedAliases()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(IsListed).Select(n => n.Name);
        }

        internal static string PriorityScript(string preferred, IEnumerable<string> others)
        {
            StringBuilder script = new StringBuilder(SetMetric(preferred, PreferredMetric));
            foreach (string alias in others)
            {
                script.Append(SetMetric(alias, StandbyMetric));
            }
            return script.ToString();
        }

        internal static string RestoreScript(IEnumerable<string> aliases)
        {
            StringBuilder script = new StringBuilder();
            foreach (string alias in aliases)
            {
                script.Append("Set-NetIPInterface -InterfaceAlias " + Quote(alias)
                    + " -AutomaticMetric Enabled -ErrorAction SilentlyContinue;");
            }
            return script.ToString();
        }

        static string SetMetric(string alias, int metric)
        {
            return "Set-NetIPInterface -InterfaceAlias " + Quote(alias)
                + " -AutomaticMetric Disabled -InterfaceMetric " + metric + " -ErrorAction SilentlyContinue;";
        }

        static string Quote(string alias)
        {
            return "'" + alias.Replace("'", "''") + "'";
        }

        public static string WifiName()
        {
            ProcessStartInfo info = new ProcessStartInfo("netsh.exe", "wlan show interfaces")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                Match match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+?)\s*$", RegexOptions.Multiline);
                return match.Success ? match.Groups[1].Value : null;
            }
        }

        static bool WaitFor(Func<bool> condition, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                if (condition()) return true;
                Thread.Sleep(1000);
            }
            return condition();
        }

        static void RunPowerShell(string script)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded);
        }

        static void Run(string file, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process process = Process.Start(info))
            {
                process.WaitForExit(30000);
            }
        }
    }
}
