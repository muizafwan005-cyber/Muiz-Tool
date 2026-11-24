using System;
using System.Windows.Forms;

namespace RFTool;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Beberapa window karakter (tombol "+") berjalan di 1 aplikasi.
        // Aplikasi selesai setelah window terakhir ditutup.
        new MainForm().Show();
        Application.Run();
    }
}
