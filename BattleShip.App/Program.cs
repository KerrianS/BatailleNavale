using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using BattleShip.App;
using BattleShip.App.Services;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using BattleShip.API.Protos;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient("BattleShipAPI", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001");
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5001") });

builder.Services.AddSingleton(sp =>
{
    var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
    var channel = GrpcChannel.ForAddress("http://localhost:5001", new GrpcChannelOptions { HttpClient = httpClient });
    return new BattleshipService.BattleshipServiceClient(channel);
});

builder.Services.AddSingleton<GameState>();
builder.Services.AddScoped<MultiplayerGameClient>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var jsRuntime = sp.GetRequiredService<IJSRuntime>();
    return new MultiplayerGameClient(httpClient, jsRuntime);
});

await builder.Build().RunAsync();