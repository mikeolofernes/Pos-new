using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Pos.Web.Client;
using Pos.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMudServices(cfg =>
{
    cfg.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    cfg.SnackbarConfiguration.PreventDuplicates = true;
    cfg.SnackbarConfiguration.VisibleStateDuration = 3500;
    cfg.SnackbarConfiguration.ShowCloseIcon = true;
});
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<AuthHeaderHandler>();
builder.Services.AddScoped<OfflineOutbox>();
builder.Services.AddScoped<IndexedDbInterop>();

builder.Services.AddHttpClient("auth-raw", c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}).AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped<SignalRConnection>();

await builder.Build().RunAsync();
