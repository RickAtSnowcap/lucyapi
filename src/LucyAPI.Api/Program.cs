using System.Text.Json;
using LucyAPI.Api;
using LucyAPI.Api.Endpoints;
using LucyAPI.Api.Middleware;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Implementations;
using LucyAPI.Services.Interfaces;
using Npgsql;
using Snowcap.TCrypt;

var builder = WebApplication.CreateBuilder(args);

// --- Connection string ---
string connectionString;
if (builder.Environment.IsDevelopment())
{
    connectionString = Environment.GetEnvironmentVariable("LUCYAPI_CONNECTION_STRING")
        ?? throw new InvalidOperationException("Set LUCYAPI_CONNECTION_STRING for development");
}
else
{
    var suitcaseKey = SuitcaseCrypt.LoadKey();
    var encrypted = builder.Configuration["Suitcase:DbConnection"]
        ?? throw new InvalidOperationException("Suitcase:DbConnection not configured");
    connectionString = SuitcaseCrypt.Decrypt(encrypted, suitcaseKey);
}

// --- Npgsql (AOT-safe) ---
var dataSourceBuilder = new NpgsqlSlimDataSourceBuilder(connectionString);
dataSourceBuilder.EnableArrays();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

// --- Repositories ---
builder.Services.AddSingleton<AgentRepository>();
builder.Services.AddSingleton<AlwaysLoadRepository>();
builder.Services.AddSingleton<MemoryRepository>();
builder.Services.AddSingleton<PreferenceRepository>();
builder.Services.AddSingleton<ProjectRepository>();
builder.Services.AddSingleton<SectionRepository>();
builder.Services.AddSingleton<WikiRepository>();
builder.Services.AddSingleton<WikiSectionRepository>();
builder.Services.AddSingleton<WikiTagRepository>();
builder.Services.AddSingleton<HandoffRepository>();
builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<ContextRepository>();
builder.Services.AddSingleton<HintRepository>();
builder.Services.AddSingleton<SecretRepository>();
builder.Services.AddSingleton<ShareRepository>();

// --- Services ---
builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddSingleton<IAlwaysLoadService, AlwaysLoadService>();
builder.Services.AddSingleton<IMemoryService, MemoryService>();
builder.Services.AddSingleton<IPreferenceService, PreferenceService>();
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<ISectionService, SectionService>();
builder.Services.AddSingleton<IWikiService, WikiService>();
builder.Services.AddSingleton<IWikiSectionService, WikiSectionService>();
builder.Services.AddSingleton<IWikiTagService, WikiTagService>();
builder.Services.AddSingleton<IHandoffService, HandoffService>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<IContextService, ContextService>();
builder.Services.AddSingleton<IHintService, HintService>();
builder.Services.AddSingleton<ISecretService, SecretService>();
builder.Services.AddSingleton<IShareService, ShareService>();

// --- JSON (AOT source-generated, snake_case) ---
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// --- Middleware ---
app.UseCors();
app.UseMiddleware<ApiKeyAuthMiddleware>();

// --- Endpoints ---
app.MapHealthEndpoints();
app.MapTimeEndpoints();
app.MapContextEndpoints();
app.MapAlwaysLoadEndpoints();
app.MapMemoryEndpoints();
app.MapPreferenceEndpoints();
app.MapHandoffEndpoints();
app.MapSessionEndpoints();
app.MapProjectEndpoints();
app.MapSectionEndpoints();
app.MapWikiEndpoints();
app.MapWikiSectionEndpoints();
app.MapWikiTagEndpoints();
app.MapHintEndpoints();
app.MapSecretEndpoints();
app.MapShareEndpoints();

app.Run();
