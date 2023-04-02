using Microsoft.AspNetCore.SignalR;

namespace Nj.Samples.MvvmWebsocketServer.Server.Hub;
public class ChatHub : Microsoft.AspNetCore.SignalR.Hub
{
    public async Task SendMessage(string user, string message) => await Clients.All.SendAsync("ReceiveMessage", user, message);
}