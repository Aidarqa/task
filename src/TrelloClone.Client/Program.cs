using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MudBlazor;
using Blazored.LocalStorage;
using TrelloClone.Client;
using TrelloClone.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// ── API HttpClient ──────────────────────────────────────
var configuredUrl = builder.Configuration["ApiBaseUrl"];
var apiBase = string.IsNullOrWhiteSpace(configuredUrl)
    ? builder.HostEnvironment.BaseAddress
    : configuredUrl;

builder.Services.AddScoped<AuthTokenHandler>();
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBase);
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

// ── Auth ────────────────────────────────────────────────
builder.Services.AddAuthorizationCore(options =>
{
    foreach (var perm in TrelloClone.Shared.Models.Permissions.All)
    {
        options.AddPolicy(perm.Code, p => p
            .RequireAuthenticatedUser()
            .RequireClaim("perm", perm.Code));
    }
});
builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();

// ── Board Services ──────────────────────────────────────
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<BoardHubService>();

// ── Localization & Theme ────────────────────────────────
builder.Services.AddSingleton<LocalizationService>();
builder.Services.AddSingleton<ThemeService>();

// ── Corporate Services ──────────────────────────────────
builder.Services.AddScoped<WorkTaskService>();
builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<CalendarService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ChatClientService>();
builder.Services.AddScoped<VisitService>();
builder.Services.AddScoped<NotificationClientService>();
builder.Services.AddScoped<FileDownloadService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<RoleService>();

// ── MudBlazor ───────────────────────────────────────────
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.HideTransitionDuration = 200;
});

// ── Local Storage ───────────────────────────────────────
builder.Services.AddBlazoredLocalStorage();

await builder.Build().RunAsync();
