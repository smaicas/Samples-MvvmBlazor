using Microsoft.AspNetCore.SignalR;

namespace Nj.Samples.MvvmWebsocketServer.Server.Hub;
public class LoginHub : Microsoft.AspNetCore.SignalR.Hub
{
    public async Task Login(string user, string password) => await Clients.All.SendAsync("Login", user, password);
}