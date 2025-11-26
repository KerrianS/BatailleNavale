using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BattleShip.App;
using BattleShip.App.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurer le HttpClient avec l'adresse de l'API comme BaseAddress
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5290") });
builder.Services.AddScoped<GameState>();

await builder.Build().RunAsync();
