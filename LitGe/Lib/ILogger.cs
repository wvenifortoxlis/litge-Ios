namespace LitGe.Lib;

public interface ILogger
{
    void Out(object arg);
}

public class DebugLogger : ILogger
{
    public void Out(object arg)
    {
        System.Diagnostics.Debug.WriteLine($"[Debugger] {arg}");
    }
}
