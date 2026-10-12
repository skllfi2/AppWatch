namespace AppWatch.Core.Domain.Models;

public enum UpdateStage
{
    Idle,
    Checking,
    Downloading,
    Replacing,
    Restarting,
    Done,
    Error,
}
