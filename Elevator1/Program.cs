﻿using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using Dapper;
using Elevator1;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;



string connectUrl = "http://localhost:*port*/api/devices/connect";




//Replace port
Console.Write("What port is being used: ");
connectUrl = connectUrl.Replace("*port*", Console.ReadLine());
Console.Clear();

List<CreateElevator> elevators = new List<CreateElevator>
{
    new CreateElevator(new ElevatorModel{Id = Guid.Parse("0e3c55f3-ea28-4551-8b06-c9dd9b6b3711"), Name = "test1"}, connectUrl),
    new CreateElevator(new ElevatorModel{Id = Guid.Parse("25967951-1de4-4608-a42c-0938e1c6de83"), Name = "test2"}, connectUrl),
};

Console.CursorVisible = false;
string statusBoard = "";
int amountOfElevator = 0;
foreach (var elevator in elevators)
{
    amountOfElevator += 1;
    elevator.CreteElevator();
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
