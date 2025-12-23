using System;
using System.IO;
using System.Text;
using Avalonia.Threading;

namespace UnderMine.Services.Logger;

public class Logger : TextWriter
{
    private readonly Action<string> _writeAction;

    public Logger(Action<string> writeAction)
    {
        _writeAction = writeAction;
    }

    public override void WriteLine(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Dispatcher.UIThread.Post(() => _writeAction(value));
        }
    }

    public override Encoding Encoding => Encoding.UTF8;
}