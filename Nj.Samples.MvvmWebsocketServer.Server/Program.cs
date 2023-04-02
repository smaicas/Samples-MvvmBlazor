using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.ResponseCompression;
using Nj.Samples.MvvmWebsocketServer.Server;
using Nj.Samples.MvvmWebsocketServer.Server.Hub;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAccessTokenProvider, CustomAccessTokenProvider>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
//builder.Services.AddScoped<LoginViewModel>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAuthorizationCore();
//builder.Services.AddIdentity<AppUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
//    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<HttpInterceptor>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "YourApp.Cookie";
        options.Events = new CookieAuthenticationEvents
        {
            OnSigningIn = async context =>
            {
                ClaimsPrincipal userPrincipal = context.Principal;

                if (userPrincipal.HasClaim(c => c.Type == ClaimTypes.Name))
                {
                    string userName = userPrincipal.FindFirstValue(ClaimTypes.Name);

                    List<Claim> claims = new()
                    {
                        new Claim(ClaimTypes.NameIdentifier, userName),
                        new Claim(ClaimTypes.Name, userName)
                    };

                    //var appUser = await userManager.FindByNameAsync(userName);
                    //var roles = await userManager.GetRolesAsync(appUser);

                    //foreach (var role in roles)
                    //{
                    //    claims.Add(new Claim(ClaimTypes.Role, role));
                    //}

                    ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Principal = new ClaimsPrincipal(identity);
                }
            }
        };
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
    });
builder.Services.AddResponseCompression(opts => opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" }));

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();

    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseWebAssemblyDebugging();
}

app.UseHttpsRedirection();

app.Map("/wasm", app =>
{
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapBlazorHub();
        endpoints.MapHub<ChatHub>("/chathub");
        endpoints.MapHub<LoginHub>("/loginhub");
        endpoints.MapFallbackToFile("index.html");
    });
});

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapControllers();

app.MapBlazorHub();
app.MapHub<ChatHub>("/chathub");
app.MapHub<LoginHub>("/loginhub");
app.MapFallbackToPage("/_Host");

app.Run();

public class CustomAccessTokenProvider : IAccessTokenProvider
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CustomAccessTokenProvider(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async ValueTask<AccessTokenResult> RequestAccessToken()
    {
        try
        {
            string cookieValue = await _httpClientFactory.CreateClient().GetStringAsync(_configuration.GetValue<string>("YourApp.CookieUrl"));
            AccessToken token = new() { Value = cookieValue, Expires = DateTimeOffset.MaxValue };
            return new AccessTokenResult(AccessTokenResultStatus.Success, token, null, null);
        }
        catch (Exception ex)
        {
            return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, null, null, null);
        }
    }

    public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options) => throw new NotImplementedException();
}
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAccessTokenProvider _accessTokenProvider;

    public CustomAuthenticationStateProvider(IAccessTokenProvider accessTokenProvider) => _accessTokenProvider = accessTokenProvider;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        AccessTokenResult accessTokenResult = await _accessTokenProvider.RequestAccessToken();

        if (accessTokenResult.TryGetToken(out AccessToken? accessToken))
        {
            ClaimsIdentity identity = new(new[]
            {
                new Claim(ClaimTypes.Name, "username")
            }, "YourApp.AuthenticationType");

            ClaimsPrincipal user = new(identity);

            return new AuthenticationState(user);
        }

        return new AuthenticationState(new ClaimsPrincipal());
    }
}

public class HttpInterceptor : DelegatingHandler
{
    private readonly IAccessTokenProvider _accessTokenProvider;

    public HttpInterceptor(IAccessTokenProvider accessTokenProvider) => _accessTokenProvider = accessTokenProvider;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        AccessTokenResult accessTokenResult = await _accessTokenProvider.RequestAccessToken();
        if (accessTokenResult.TryGetToken(out AccessToken? token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
