using System;
using System.Windows;

namespace NetworkToggle
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            new Application().Run(new MainWindow());
        }
    }
}
