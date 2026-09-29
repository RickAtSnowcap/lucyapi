using System.Text.Json;
using LucyAPI.Api;
using LucyAPI.Api.Auth;
using LucyAPI.Api.Endpoints;
using LucyAPI.Api.Endpoints.Admin;
using LucyAPI.Api.Mcp;
using LucyAPI.Api.Middleware;
using LucyAPI.Api.OAuth;
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
builder.Services.AddSingleton<ImageRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<AdminRepository>();
builder.Services.AddSingleton<NudgeRepository>();
builder.Services.AddSingleton<OAuthRepository>();

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
// SecretService registered below after encryptionKey is resolved
builder.Services.AddSingleton<IShareService, ShareService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<INudgeService, NudgeService>();

var encryptionKey = builder.Environment.IsDevelopment()
    ? Convert.FromBase64String(Environment.GetEnvironmentVariable("LUCYAPI_ENCRYPTION_KEY") ?? "")
    : SuitcaseCrypt.LoadKey();

// Secrets live TPM-sealed in the Suitcase section (preference #33); development reads plain env vars instead.
string SealedSetting(string suitcaseName, string devEnvVar) =>
    builder.Environment.IsDevelopment()
        ? Environment.GetEnvironmentVariable(devEnvVar)
            ?? throw new InvalidOperationException($"Set {devEnvVar} for development")
        : SuitcaseCrypt.Decrypt(builder.Configuration[$"Suitcase:{suitcaseName}"]
            ?? throw new InvalidOperationException($"Suitcase:{suitcaseName} not configured"), encryptionKey);

// --- JWT Auth ---
builder.Services.AddSingleton(new JwtTokenService(SealedSetting("JwtSigningKey", "LUCYAPI_JWT_SECRET")));

// --- Phase 5: Google Docs, Images, Save Notes ---

builder.Services.AddSingleton<ISecretService>(sp =>
    new SecretService(sp.GetRequiredService<SecretRepository>(), encryptionKey));

builder.Services.AddSingleton<IGoogleDocsService>(sp =>
    new GoogleDocsService(sp.GetRequiredService<ISecretService>()));

var geminiApiKey = SealedSetting("GeminiApiKey", "GEMINI_API_KEY");
builder.Services.AddSingleton<IGeminiService>(new GeminiService(geminiApiKey));

var imagesDir = builder.Configuration["Images:Directory"] ?? "/opt/lucyapi/output/images";
var baseUrl = builder.Configuration["Images:BaseUrl"] ?? "https://lucyapi.snowcapsystems.com";
builder.Services.AddSingleton<IImageService>(sp =>
    new ImageService(sp.GetRequiredService<ImageRepository>(), sp.GetRequiredService<IGeminiService>(),
        imagesDir, baseUrl));

// --- Signed project document links (24h, no credential in the URL) ---
var documentLinkKey = Convert.FromBase64String(SealedSetting("DocumentLinkKey", "LUCYAPI_DOCLINK_KEY"));
if (documentLinkKey.Length != 32)
    throw new InvalidOperationException("DocumentLinkKey must be 32 bytes");
builder.Services.AddSingleton(new DocumentLinkSigner(documentLinkKey, baseUrl));

builder.Services.AddSingleton<ISaveNotesService>(new SaveNotesService(
    smtpHost: builder.Configuration["SmtpHost"] ?? "smtp.forwardemail.net",
    smtpPort: int.TryParse(builder.Configuration["SmtpPort"], out var port) ? port : 587,   // STARTTLS — System.Net.Mail can't do implicit TLS on 465
    smtpUser: builder.Configuration["SmtpUser"] ?? "rick@snowcapsystems.com",
    smtpPass: SealedSetting("SmtpPassword", "LUCYAPI_SMTP_PASS"),
    sendTo: builder.Configuration["SmtpSendTo"] ?? "rick@snowcapsystems.com"));

// --- Phase 6: MCP Server ---
builder.Services.AddSingleton<McpToolDispatcher>();

// --- LucyAPI OAuth (project #62): authorization server for the MCP connector ---
builder.Services.AddSingleton<OAuthClientResolver>();

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
app.UseMiddleware<JwtAuthMiddleware>();
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
app.MapGoogleDocsEndpoints();
app.MapImageEndpoints();
app.MapNudgeEndpoints();
app.MapMcpEndpoints();
app.MapOAuthEndpoints();

// --- Admin Endpoints (JWT-scoped) ---
app.MapAdminAuthEndpoints();
app.MapAdminAgentsEndpoints();
app.MapAdminResourcesEndpoints();

app.Run();
