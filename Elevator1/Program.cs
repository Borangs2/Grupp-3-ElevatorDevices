using System;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using Dapper;
using Elevator1;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;



string connectUrl = "https://grupp3azurefunctions.azurewebsites.net/api/devices/connect?";


List<CreateElevators> elevators = new List<CreateElevators>
{
    new CreateElevators(new ElevatorModel{Id = Guid.Parse("0e3c55f3-ea28-4551-8b06-c9dd9b6b3711"), Name = "Optima 583-25"}, connectUrl, 0, 5),
    new CreateElevators(new ElevatorModel{Id = Guid.Parse("25967951-1de4-4608-a42c-0938e1c6de83"), Name = "Orco Lightning-673 82"}, connectUrl, 0, 7),
    new CreateElevators(new ElevatorModel{Id = Guid.Parse("3dfdb699-27a5-4778-a34d-a34784c9076a"), Name = "Motioncoms ver.3 (183)"}, connectUrl, 0, 4),
    new CreateElevators(new ElevatorModel{Id = Guid.Parse("419b0776-83b3-4aa1-91e3-cf02bae8c1dd"), Name = "Optima 884"}, connectUrl, 0, 9),
    new CreateElevators(new ElevatorModel{Id = Guid.Parse("64248f21-f9cf-4e99-a2f8-5b3c30ff307f"), Name = "Globeworks 1035.92"}, connectUrl, -2, 5),
};

Console.CursorVisible = false;
string statusBoard = "";
int amountOfElevator = 0;
foreach (var elevator in elevators)
{
    amountOfElevator += 1;
    elevator.CreateElevator();
    elevator.Elevator.LastUsed = DateTime.Now;
    statusBoard += $"{amountOfElevator}: {elevator.StatusMessage} \n";
}


while (true)
{
    Console.SetCursorPosition(0, 0);
    statusBoard = "";
    int i = 0;
    foreach (var elevator in elevators)
    {
        i++;
        statusBoard += $"{i}: {elevator.StatusMessage} \n";
    }
    Console.Write(statusBoard);
}
