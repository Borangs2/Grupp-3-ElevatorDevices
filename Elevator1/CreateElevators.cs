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
using System.Net;
using Newtonsoft.Json;

namespace Elevator1
{
    public  class CreateElevators
    {
        public string StatusMessage { get; set; }
        public ElevatorModel Elevator { get; set; }
        private readonly string _connectUrl;
        private readonly DeviceClient _deviceClient;



        public CreateElevators(ElevatorModel elevator, string connectUrl, int minLevel, int maxLevel)
        {
            Elevator = elevator;
            Elevator.MinLevel = minLevel;
            Elevator.MaxLevel = maxLevel;
            _connectUrl = connectUrl;
            _deviceClient = DeviceClient.CreateFromConnectionString(IoTHubConnectionString, Elevator.Id.ToString() );
        }

        string DbConnectionString =
            "Server=tcp:kyh-devops.database.windows.net,1433;Initial Catalog=Kyh-Agile Grupp 3;Persist Security Info=False;User ID=CloudSA37b586b4;Password=Andreas1!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        string IoTHubConnectionString =
            "HostName=Grup-3-Devops.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=cXHMcmESQtlUTvhMJ8q5aQvzf9aPcWQ9JAN6fCc2r2Q=";

        public  async Task CreateElevator()
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
                twinCollection["minLevel"] = Elevator.MinLevel;
                twinCollection["maxLevel"] = Elevator.MaxLevel;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

                var twin = await _deviceClient.GetTwinAsync();


                await _deviceClient.SetMethodHandlerAsync("ChangeLevel", ChangeLevelDM, null);
                await _deviceClient.SetMethodHandlerAsync("ResetElevator", ResetElevatorDM, null);
                await _deviceClient.SetMethodHandlerAsync("OpenDoors", OpenDoorsDM, null);
                await _deviceClient.SetMethodHandlerAsync("CloseDoors", CloseDoorsDM, null);
                await _deviceClient.SetMethodHandlerAsync("TurnOffElevator", TurnOffElevatorDM, null);
                await _deviceClient.SetMethodHandlerAsync("TurnOnElevator", TurnOnElevatorDM, null);

                Elevator.IsConnected = true;
                StatusMessage = "Device Connected:                  ██████████";

                KeepActive();
                ResetElevatorAfter10Minutes();

                while (true)
                {
                    StatusMessage = $"{Elevator.Name} ({Elevator.MinLevel} - {Elevator.MaxLevel}): Current level: {Elevator.CurrentLevel}, Target level: {Elevator.TargetLevel}, Status: {Elevator.Status}, doorStatus: {Elevator.DoorStatus}";
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                Elevator.Status = ElevatorStatus.Error;
                StatusMessage = $"Something went wrong: {ex.Message}";
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
                if (DateTime.Compare(Elevator.LastUsed, DateTime.Now.AddMinutes(-10)) < 0 && Elevator.Status != ElevatorStatus.Disabled)
                {
                    await GoToLowestFloorAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        private void ResetTimer()
        {
            Elevator.LastUsed = DateTime.Now;
        }

        public async Task<MethodResponse> ChangeLevelDM(MethodRequest methodRequest, object userContext)
        {
            var payload = JsonConvert.DeserializeObject<dynamic>(methodRequest.DataAsJson);
            HttpStatusCode result = HttpStatusCode.BadRequest;
            if(payload != null)
                result = await ChangeLevelAsync(Convert.ToInt32(payload));
            
            if(result != HttpStatusCode.OK)
                return new MethodResponse(new byte[0], 200);
            return new MethodResponse(new byte[0], 400);

        }

        public async Task<HttpStatusCode> ChangeLevelAsync(int newLevel)
        {
            if(Elevator.Status != ElevatorStatus.Disabled)
            {
                if (newLevel >= Elevator.MinLevel && newLevel <= Elevator.MaxLevel)
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
                            await Task.Delay(TimeSpan.FromSeconds(4));

                            if (Elevator.CurrentLevel > Elevator.TargetLevel)
                                Elevator.CurrentLevel -= 1;
                            if (Elevator.CurrentLevel < Elevator.TargetLevel)
                                Elevator.CurrentLevel += 1;

                            twinCollection["currentLevel"] = Elevator.CurrentLevel;
                            await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                        }

                        Elevator.DoorStatus = true;
                        Elevator.Status = ElevatorStatus.Idle;

                        twinCollection["doorStatus"] = Elevator.DoorStatus;
                        twinCollection["status"] = Elevator.Status;
                        twinCollection["currentLevel"] = Elevator.TargetLevel;
                        await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);


                        await Task.Delay(TimeSpan.FromSeconds(20));

                        await CloseDoorsAsync();
                        ResetTimer();
                        return HttpStatusCode.OK;
                    }
                }
            }
            ResetTimer();
            return HttpStatusCode.BadRequest;
        }

        private async Task ResetElevatorAsync()
        {
            await ChangeLevelAsync(0);
            Elevator.Status = ElevatorStatus.Disabled;

            var twinCollection = new TwinCollection();
            twinCollection["status"] = ElevatorStatus.Disabled;
            await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
        }

        public async Task GoToLowestFloorAsync()
        {
            await ChangeLevelAsync(0);
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

                var twinCollection = new TwinCollection();
                twinCollection["doorStatus"] = true;
                await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

                ResetTimer();
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

                ResetTimer();
            }
        }

        public Task<MethodResponse> CloseDoorsDM(MethodRequest methodRequest, object userContext)
        {
            CloseDoorsAsync().ConfigureAwait(false);

            return Task.FromResult(new MethodResponse(new byte[0], 200));
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

                ResetTimer();
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

            ResetTimer();
        }
    }
}
