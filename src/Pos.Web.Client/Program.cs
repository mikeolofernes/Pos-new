using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Pos.Web.Client;
using Pos.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddBlazoredLocalStorage();
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
