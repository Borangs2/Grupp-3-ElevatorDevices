using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Json;
using System.Net.Mime;
using Dapper;
using Elevator1;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;





string connectUrl = "http://localhost:*port*/api/devices/connect";
string IoTHubConnectionString =
    "HostName=Grup-3-Devops.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=cXHMcmESQtlUTvhMJ8q5aQvzf9aPcWQ9JAN6fCc2r2Q=";
string DbConnectionString =
    "Server=tcp:kyh-devops.database.windows.net,1433;Initial Catalog=Kyh-Agile Grupp 3;Persist Security Info=False;User ID=CloudSA37b586b4;Password=Andreas1!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
bool poweredState = false;
TimeSpan interval = TimeSpan.FromHours(1);
bool connected = false;


Guid deviceId = Guid.Parse("04ddc77d-d1c3-41dd-a3e8-14896f1d9b63");
string deviceName = "Optima 563";
ElevatorStatus status = ElevatorStatus.Disabled;
bool doorStatus = false;
int currentLevel = 0;
int targetLevel = 0;




Console.WriteLine("Initializing Device. Please wait ...");

//Replace port
Console.Write("What port is being used: ");
connectUrl = connectUrl.Replace("*port*", Console.ReadLine());

using IDbConnection connection = new SqlConnection(DbConnectionString);

if (await connection.QueryFirstOrDefaultAsync<Guid>("SELECT Id FROM Elevators WHERE Id = @DeviceId", new { DeviceId = deviceId }) == Guid.Empty)
{
    Console.WriteLine("New device detected. Inserting device to database ...");

    await connection.ExecuteAsync(
        "INSERT INTO Elevators (id, ConnectionString) VALUES (@deviceId, @connectionString)",
        new {deviceId, ConnectionString = ""});
}
else
{
    Console.WriteLine("Device already exists. Getting connection string");
}


var deviceConnectionString = await connection.QueryFirstOrDefaultAsync<string>("SELECT ConnectionString FROM Elevators WHERE Id = @DeviceId", new { DeviceId = deviceId });
if (string.IsNullOrEmpty(deviceConnectionString))
{
    Console.WriteLine("Initializing Connection string ...");

    using var http = new HttpClient();
    var result = await http.PostAsJsonAsync($"{connectUrl}?DeviceId={deviceId}", new { DeviceId = deviceId });

    deviceConnectionString = await result.Content.ReadAsStringAsync();
    await connection.ExecuteAsync("UPDATE Elevators SET ConnectionString = @ConnectionString WHERE Id = @DeviceId",
         new { DeviceId = deviceId, ConnectionString = deviceConnectionString });
}

DeviceClient _deviceClient = DeviceClient.CreateFromConnectionString(IoTHubConnectionString, deviceId.ToString());

Console.WriteLine("Updating twin properties ...");
var twinCollection = new TwinCollection();
twinCollection["deviceName"] = deviceName.ToLower();
twinCollection["status"] = status.ToString().ToLower();
twinCollection["doorStatus"] = doorStatus;
twinCollection["currentLevel"] = currentLevel;
twinCollection["targetLevel"] = targetLevel;
await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

var twin = await _deviceClient.GetTwinAsync();


await _deviceClient.SetMethodHandlerAsync("ChangeLevel", ChangeLevel, null);

connected = true;
Console.WriteLine("Device Connected");

while (true)
{
    twinCollection["update"] = "jag existerar";
    await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

    Console.WriteLine("Message sent");
    await Task.Delay(interval);
}


Task<MethodResponse> ChangeLevel(MethodRequest methodRequest, object userContext)
{
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

    return Task.FromResult(new MethodResponse(new byte[0], 200));
}