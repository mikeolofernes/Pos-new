using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Pos.Api.Hubs;

[Authorize]
public class PosHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tid = Context.User?.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tid))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tid}");
        await base.OnConnectedAsync();
    }

    public async Task JoinShop(string shopId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shop-{shopId}");

    public async Task LeaveShop(string shopId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shop-{shopId}");
}
