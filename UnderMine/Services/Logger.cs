using System;
using System.IO;
using System.Text;
using Avalonia.Threading;

namespace UnderMine.Services;

public class Logger : TextWriter
{
    private readonly Action<string> _writeAction;

    public Logger(Action<string> writeAction)
    {
        _writeAction = writeAction;
    }

    public override void WriteLine(string? value)
    {
        if (value != null)
        {
            // Обновляем Label на UI-потоке
            Dispatcher.UIThread.Post(() => _writeAction(value));
        }
    }

    public override void Write(char value) { }

    public override Encoding Encoding => Encoding.UTF8;
}