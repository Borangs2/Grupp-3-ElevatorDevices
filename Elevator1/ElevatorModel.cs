namespace Elevator1;

public class ElevatorModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public ElevatorStatus Status { get; set; }
    public bool DoorStatus { get; set; }
    public int CurrentLevel { get; set; }
    public int TargetLevel { get; set; }
    public bool IsConnected { get; set; } = false;
}