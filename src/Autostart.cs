using System.Diagnostics;
using System.Security;
using System.Text;

namespace KbLight;

// A Task Scheduler logon task instead of the Run key: Windows delays Run-key apps after logon,
// a logon task starts right away. Creating it for the current user needs no admin rights.
static class Autostart
{
    const string TaskName = "KbLight";

    public static bool IsEnabled() => Schtasks($"/Query /TN \"{TaskName}\"", out _) == 0;

    public static void Enable(string exePath)
    {
        string file = Path.Combine(Path.GetTempPath(), $"kblight-task-{Environment.ProcessId}.xml");
        File.WriteAllText(file, TaskXml($@"{Environment.UserDomainName}\{Environment.UserName}", exePath), Encoding.Unicode);
        try
        {
            if (Schtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F", out string output) != 0)
                throw new InvalidOperationException("schtasks: " + output.Trim());
        }
        finally
        {
            File.Delete(file);
        }
    }

    public static void Disable()
    {
        if (IsEnabled() && Schtasks($"/Delete /TN \"{TaskName}\" /F", out string output) != 0)
            throw new InvalidOperationException("schtasks: " + output.Trim());
    }

    static int Schtasks(string arguments, out string output)
    {
        var psi = new ProcessStartInfo("schtasks.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi)!;
        var stderr = process.StandardError.ReadToEndAsync();
        output = process.StandardOutput.ReadToEnd() + stderr.Result;
        process.WaitForExit();
        return process.ExitCode;
    }

    // ASCII only: schtasks prints the XML back in the console code page.
    static string TaskXml(string user, string exePath) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>KbLight: applies keyboard lighting at logon</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
              <UserId>{SecurityElement.Escape(user)}</UserId>
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{SecurityElement.Escape(user)}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <AllowHardTerminate>true</AllowHardTerminate>
            <StartWhenAvailable>false</StartWhenAvailable>
            <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
            <IdleSettings>
              <StopOnIdleEnd>false</StopOnIdleEnd>
              <RestartOnIdle>false</RestartOnIdle>
            </IdleSettings>
            <AllowStartOnDemand>true</AllowStartOnDemand>
            <Enabled>true</Enabled>
            <Hidden>false</Hidden>
            <RunOnlyIfIdle>false</RunOnlyIfIdle>
            <WakeToRun>false</WakeToRun>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Priority>4</Priority>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(exePath)}</Command>
              <Arguments>--tray</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
}
