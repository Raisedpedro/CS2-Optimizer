using System.Runtime.InteropServices;
using System.Text;

namespace CS2Optimizer.Core.Cli;

public static class ConsoleHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    /// <summary>Garante um console para o modo CLI de um executável WinExe.</summary>
    public static void EnsureConsole()
    {
        try
        {
            if (!AttachConsole(-1)) AllocConsole();
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        }
        catch { }
    }

    public static void Write(string text, ConsoleColor color = ConsoleColor.Gray)
    {
        var old = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }
        finally
        {
            Console.ForegroundColor = old;
        }
    }
}
