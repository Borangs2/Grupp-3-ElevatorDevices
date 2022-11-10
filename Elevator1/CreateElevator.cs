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
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace Elevator1
{
    public  class CreateElevator
    {
        public string StatusMessage { get; set; }
        public ElevatorModel Elevator { get; set; }
        private readonly string _connectUrl;
        private readonly DeviceClient _deviceClient;

        public CreateElevator(ElevatorModel elevator, string connectUrl)
        {
            Elevator = elevator;
            _connectUrl = connectUrl;
            _deviceClient = DeviceClient.CreateFromConnectionString(IoTHubConnectionString);
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

                StatusMessage = "Updating twin properties           ████████--";
                var twinCollection = new TwinCollection();
                twinCollection["deviceName"] = Elevator.Name.ToLower();
                twinCollection["status"] = Elevator.Status.ToString().ToLower();
                twinCollection["doorStatus"] = Elevator.DoorStatus;
                twinCollection["currentLevel"] = Elevator.CurrentLevel;
                twinCollection["targetLevel"] = Elevator.TargetLevel;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

                var twin = await _deviceClient.GetTwinAsync();


                await _deviceClient.SetMethodHandlerAsync("ChangeLevel", ChangeLevelDM, null);
                await _deviceClient.SetMethodHandlerAsync("ResetElevatorDM", ResetElevatorDM, null);
                await _deviceClient.SetMethodHandlerAsync("OpenDoors", OpenDoorsDM, null);
                await _deviceClient.SetMethodHandlerAsync("CloseDoors", CloseDoorsDM, null);

                Elevator.IsConnected = true;
                StatusMessage = "Device Connected:                  ██████████";

                KeepActive();
                ResetElevatorAfter10Minutes();

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

        async Task KeepActive()
        {
            var twinCollection = new TwinCollection();
            while (true)
            {
                twinCollection["update"] = "jag existerar";
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private async Task ResetElevatorAfter10Minutes()
        {
            while (true)
            {
                if (DateTime.Compare(Elevator.LastUsed, DateTime.Now.AddMinutes(-10)) > 0)
                {
                    await GoToLowestFloorAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        public Task<MethodResponse> ChangeLevelDM(MethodRequest methodRequest, object userContext)
        {
            var payload = JsonConvert.DeserializeObject<dynamic>(methodRequest.DataAsJson);
            ChangeLevelAsync(payload!.newLevel);
            
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        private async Task ChangeLevelAsync(int newLevel)
        {
            if(Elevator.Status != ElevatorStatus.Disabled)
            {
                if (newLevel != Elevator.TargetLevel)
                {
                    Elevator.DoorStatus = false;
                    Elevator.TargetLevel = newLevel;
                    Elevator.Status = ElevatorStatus.Running;


                    var twinCollection = new TwinCollection();
                    twinCollection["doorStatus"] = Elevator.DoorStatus;
                    twinCollection["targetLevel"] = Elevator.TargetLevel;
                    twinCollection["status"] = Elevator.Status;

                    await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

                    while (Elevator.CurrentLevel != Elevator.TargetLevel)
                    {
                        if (Elevator.CurrentLevel > Elevator.TargetLevel)
                            Elevator.CurrentLevel -= 1;
                        if (Elevator.CurrentLevel < Elevator.TargetLevel)
                            Elevator.CurrentLevel += 1;

                        await Task.Delay(TimeSpan.FromSeconds(4));
                    }

                    Elevator.DoorStatus = true;
                    Elevator.Status = ElevatorStatus.DoorsOpen;

                    twinCollection["status"] = Elevator.Status;
                    twinCollection["doorStatus"] = Elevator.DoorStatus;
                    twinCollection["currentLevel"] = Elevator.TargetLevel;
                    await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);


                    await Task.Delay(TimeSpan.FromSeconds(20));

                    await CloseDoorsAsync();
                }
            }

           
        }

        private async Task ResetElevatorAsync()
        {
            await ChangeLevelAsync(0);
            await CloseDoorsAsync();
            Elevator.Status = ElevatorStatus.Disabled;

            var twinCollection = new TwinCollection();
            twinCollection["status"] = ElevatorStatus.Disabled;
            await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
        }

        public Task<MethodResponse> ResetElevatorDM(MethodRequest methodRequest, object userContext)
        {
            ResetElevatorAsync().ConfigureAwait(false);

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        private async Task OpenDoorsAsync()
        {
            if (Elevator.Status != ElevatorStatus.Disabled)
            {
                Elevator.DoorStatus = true;
                Elevator.Status = ElevatorStatus.DoorsOpen;

                var twinCollection = new TwinCollection();
                twinCollection["doorStatus"] = true;
                twinCollection["status"] = Elevator.Status;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
            }
        }

        public Task<MethodResponse> OpenDoorsDM(MethodRequest methodRequest, object userContext)
        {
            OpenDoorsAsync().ConfigureAwait(false);

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        private async Task CloseDoorsAsync()
        {
            if (Elevator.Status != ElevatorStatus.Disabled)
            {
                Elevator.DoorStatus = false;
                Elevator.Status = ElevatorStatus.Idle;

                var twinCollection = new TwinCollection();
                twinCollection["doorStatus"] = false;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
            }
        }

        public Task<MethodResponse> CloseDoorsDM(MethodRequest methodRequest, object userContext)
        {
            CloseDoorsAsync().ConfigureAwait(false);

            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public async Task GoToLowestFloorAsync()
        {
            await ChangeLevelAsync(0);
        }

        public Task<MethodResponse> TurnOffElevatorDM(MethodRequest methodRequest, object userContext)
        {
            TurnOffElevator().ConfigureAwait(false);
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public async Task TurnOffElevator()
        {
            if (Elevator.Status != ElevatorStatus.Disabled)
            {
                Elevator.Status = ElevatorStatus.Disabled;

                var twinCollection = new TwinCollection();
                twinCollection["status"] = Elevator.Status;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
            }
        }

        public Task<MethodResponse> TurnOnElevatorDM(MethodRequest methodRequest, object userContext)
        {
            TurnOnElevator().ConfigureAwait(false);
            return Task.FromResult(new MethodResponse(new byte[0], 200));
        }

        public async Task TurnOnElevator()
        {
            Elevator.Status = ElevatorStatus.Idle;

            var twinCollection = new TwinCollection();
            twinCollection["status"] = Elevator.Status;
            await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
        }
    }
}
