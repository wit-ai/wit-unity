using Meta.Voice.Logging;

namespace Lib.Wit.Runtime.Utilities.Logging
{
    /// <summary>
    /// This should be implemented by classes that will be writing logs to VLogger.
    /// </summary>
    public interface ILogSource
    {
        IVLogger Logger { get; }
    }
}
