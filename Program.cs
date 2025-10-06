using Vonage.Extensions;
using Vonage.Request;
using Vonage_App_POC;
using Vonage_App_POC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<WorkshopOptions>(
    builder.Configuration.GetSection("workshopOptions"));

var vonageSection = builder.Configuration.GetSection("vonage");
var appId = vonageSection["Application.Id"];
var privateKeyPath = vonageSection["Application.Key"];

if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(privateKeyPath))
    throw new InvalidOperationException("Vonage Application.Id and Application.Key must be configured");

var credentials = Credentials.FromAppIdAndPrivateKeyPath(appId, privateKeyPath);
builder.Services.AddVonageClientScoped(credentials);

builder.Services.AddScoped<CallService>();
builder.Services.AddScoped<RecordingService>();
builder.Services.AddScoped<TranscriptionService>();
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<IClaudeAiService, ClaudeAiService>();
builder.Services.AddScoped<GoogleDriveService>();
builder.Services.AddSingleton<CallDataService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
