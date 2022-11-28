namespace Elevator1;

public class ElevatorModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ElevatorStatus Status { get; set; } = ElevatorStatus.Disabled;
    public bool DoorStatus { get; set; }
    public int CurrentLevel { get; set; } = 0;
    public int TargetLevel { get; set; } = 0;
    public bool IsConnected { get; set; } = false;
    public DateTime LastUsed { get; set; }
    public int MinLevel { get; set; } = 0;
    public int MaxLevel { get; set; } = 0;
}