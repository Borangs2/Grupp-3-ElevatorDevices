using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Net.Http.Json;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using static Microsoft.Azure.Amqp.Serialization.SerializableType;
using System.ComponentModel.DataAnnotations.Schema;

namespace Elevator1
{
    public  class CreateElevator
    {
        public string StatusMessage { get; set; }
        public ElevatorModel Elevator { get; set; }
        private readonly string _connectUrl;
        public CreateElevator(ElevatorModel elevator, string connectUrl)
        {
            Elevator = elevator;
            _connectUrl = connectUrl;

        }

        string DbConnectionString =
            "Server=tcp:kyh-devops.database.windows.net,1433;Initial Catalog=Kyh-Agile Grupp 3;Persist Security Info=False;User ID=CloudSA37b586b4;Password=Andreas1!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        string IoTHubConnectionString =
            "HostName=Grup-3-Devops.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=cXHMcmESQtlUTvhMJ8q5aQvzf9aPcWQ9JAN6fCc2r2Q=";

        public  async Task CreteElevator()
        {
            try
            {
                StatusMessage = "Initializing device:               ----------";


                using IDbConnection connection = new SqlConnection(DbConnectionString);

                if (await connection.QueryFirstOrDefaultAsync<Guid>("SELECT Id FROM Elevators WHERE Id = @DeviceId", new { DeviceId = Elevator.Id }) == Guid.Empty)
                {
                    StatusMessage = "New device detected:               ██--------";

                    await connection.ExecuteAsync(
                        "INSERT INTO Elevators (id, ConnectionString) VALUES (@deviceId, @ConnectionString)",
                        new { deviceId = Elevator.Id, ConnectionString = "" });
                }
                else
                {
                    StatusMessage = "Device detected:                   ██--------";
                }


                var deviceConnectionString = await connection.QueryFirstOrDefaultAsync<string>("SELECT ConnectionString FROM Elevators WHERE Id = @DeviceId", new { DeviceId = Elevator.Id });
                if (string.IsNullOrEmpty(deviceConnectionString))
                {
                    StatusMessage = "Initializing Connection string:    ████------";

                    using var http = new HttpClient();
                    var result = await http.PostAsJsonAsync($"{_connectUrl}?DeviceId={Elevator.Id}", new { DeviceId = Elevator.Id });
                    StatusMessage = "Connecting device:                 ██████----";

                    deviceConnectionString = await result.Content.ReadAsStringAsync();
                    await connection.ExecuteAsync("UPDATE Elevators SET ConnectionString = @ConnectionString WHERE Id = @DeviceId",
                        new { DeviceId = Elevator.Id, ConnectionString = deviceConnectionString });
                }

                DeviceClient _deviceClient = DeviceClient.CreateFromConnectionString(IoTHubConnectionString, Elevator.Id.ToString());

                StatusMessage = "Updating twin properties           ████████--";
                var twinCollection = new TwinCollection();
                twinCollection["deviceName"] = Elevator.Name.ToLower();
                twinCollection["status"] = Elevator.Status.ToString().ToLower();
                twinCollection["doorStatus"] = Elevator.DoorStatus;
                twinCollection["currentLevel"] = Elevator.CurrentLevel;
                twinCollection["targetLevel"] = Elevator.TargetLevel;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

                var twin = await _deviceClient.GetTwinAsync();


                await _deviceClient.SetMethodHandlerAsync("ChangeLevel", ChangeLevel, null);
                await _deviceClient.SetMethodHandlerAsync("ResetElevator", ResetElevator, null);
                await _deviceClient.SetMethodHandlerAsync("OpenDoors", OpenDoors, null);
                await _deviceClient.SetMethodHandlerAsync("CloseDoors", CloseDoors, null);

                Elevator.IsConnected = true;
                StatusMessage = "Device Connected:                  ██████████";

                KeepActive(_deviceClient);


                while (true)
                {
                    StatusMessage = $"{Elevator.Name}: Current level: {Elevator.CurrentLevel}, Target level: {Elevator.TargetLevel}                      ";
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
            catch
            {
                StatusMessage = "Something went wrong";

            }
            

        }

        async Task KeepActive(DeviceClient deviceClient)
        {
            var twinCollection = new TwinCollection();
            while (true)
            {
                twinCollection["update"] = "jag existerar";
                await deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                await Task.Delay(TimeSpan.FromHours(1));
            }

        }

        Task<MethodResponse> ChangeLevel(MethodRequest methodRequest, object userContext)
        {
            throw new NotImplementedException();

            //var newLevel = userContext.
            //doorStatus = false;
            //targetLevel = newLevel;
            //if (newLevel != currentLevel)
            //{
            //    twinCollection["doorStatus"] = doorStatus;
            //    twinCollection["targetLevel"] = targetLevel;
            //    _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

            //    while (currentLevel != targetLevel)
            //    {
            //        if (currentLevel > targetLevel)
            //            currentLevel -= 1;
            //        if (currentLevel < targetLevel)
            //            currentLevel += 1;

            //        Task.Delay(TimeSpan.FromSeconds(4));
            //    }
            //}

            //return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        
        public Task<MethodResponse> ResetElevator(MethodRequest methodRequest, object userContext)
        {
            Elevator.CurrentLevel = 0;
            Elevator.TargetLevel = 0;
            Elevator.Status = ElevatorStatus.Disabled;
            Elevator.DoorStatus = false;

            var twinCollection = new TwinCollection();
            twinCollection["currentLevel"] = 0;
            twinCollection["targetLevel"] = 0;
            twinCollection["status"] = ElevatorStatus.Disabled;
            twinCollection["doorStatus"] = false;

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public Task<MethodResponse> OpenDoors(MethodRequest methodRequest, object userContext)
        {
            Elevator.DoorStatus = true;

            var twinCollection = new TwinCollection();
            twinCollection["doorStatus"] = true;

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public Task<MethodResponse> CloseDoors(MethodRequest methodRequest, object userContext)
        {
            Elevator.DoorStatus = false;

            var twinCollection = new TwinCollection();
            twinCollection["doorStatus"] = false;

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public void GoToLowestFloor()
        {
            Elevator.TargetLevel = 0;
            var twinColection = new TwinCollection();


        }
    }
}
