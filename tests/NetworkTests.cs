using System;
using System.Net.NetworkInformation;

namespace NetworkToggle
{
    static class NetworkTests
    {
        static int failures;

        static void Check(string name, object expected, object actual)
        {
            if (Equals(expected, actual)) return;
            failures++;
            Console.WriteLine("FAILED: " + name);
            Console.WriteLine("  expected: " + expected);
            Console.WriteLine("  actual:   " + actual);
        }

        static void CheckAdapter(string description, NetworkInterfaceType type, LinkKind kind, string title)
        {
            LinkKind actual = Network.Classify(type, description);
            Check(description + " kind", kind, actual);
            Check(description + " title", title, Network.TitleFor(actual, description));
        }

        static int Main()
        {
            CheckAdapter("Intel(R) I211 Gigabit Network Connection", NetworkInterfaceType.Ethernet, LinkKind.Ethernet, "Ethernet");
            CheckAdapter("Realtek USB GbE Family Controller #2", NetworkInterfaceType.Ethernet, LinkKind.Ethernet, "USB Ethernet");
            CheckAdapter("Intel(R) Dual Band Wireless-AC 3165", NetworkInterfaceType.Wireless80211, LinkKind.WiFi, "Wi-Fi");
            CheckAdapter("Apple Mobile Device Ethernet", NetworkInterfaceType.Ethernet, LinkKind.Tethering, "USB Tethering");
            CheckAdapter("Remote NDIS based Internet Sharing Device", NetworkInterfaceType.Ethernet, LinkKind.Tethering, "USB Tethering");

            Check("Hyper-V adapter is skipped", true, Network.IsSkipped("Hyper-V Virtual Ethernet Adapter"));
            Check("Wi-Fi Direct adapter is skipped", true, Network.IsSkipped("Microsoft Wi-Fi Direct Virtual Adapter"));
            Check("Bluetooth adapter is skipped", true, Network.IsSkipped("Bluetooth Device (Personal Area Network)"));
            Check("Real adapter is kept", false, Network.IsSkipped("Intel(R) I211 Gigabit Network Connection"));

            Check("Chosen adapter gets the top priority and others go on standby",
                "Set-NetIPInterface -InterfaceAlias 'Wi-Fi' -AutomaticMetric Disabled -InterfaceMetric 1 -ErrorAction SilentlyContinue;"
                + "Set-NetIPInterface -InterfaceAlias 'Ethernet' -AutomaticMetric Disabled -InterfaceMetric 50 -ErrorAction SilentlyContinue;",
                Network.PriorityScript("Wi-Fi", new[] { "Ethernet" }));

            Check("Apostrophes in adapter names are escaped",
                "Set-NetIPInterface -InterfaceAlias 'Sam''s LAN' -AutomaticMetric Disabled -InterfaceMetric 1 -ErrorAction SilentlyContinue;",
                Network.PriorityScript("Sam's LAN", new string[0]));

            Check("Restore turns automatic metrics back on",
                "Set-NetIPInterface -InterfaceAlias 'Ethernet' -AutomaticMetric Enabled -ErrorAction SilentlyContinue;",
                Network.RestoreScript(new[] { "Ethernet" }));

            Console.WriteLine(failures == 0 ? "All tests passed." : failures + " test(s) failed.");
            return failures == 0 ? 0 : 1;
        }
    }
}
