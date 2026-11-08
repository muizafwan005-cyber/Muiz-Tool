using System.Diagnostics;

namespace RFTool.Core;

public sealed class ProcessManager
{
    public Process? Process { get; private set; }

    public bool Attach(string processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName);
        Process = Process.GetProcessesByName(name).FirstOrDefault();
        return Process is not null;
    }

    public void Detach() => Process = null;
}
