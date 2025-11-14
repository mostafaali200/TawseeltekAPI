using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TawseeltekAPI.Data;
using TawseeltekAPI.Hubs;
using TawseeltekAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// ✅ Controllers + Swagger
// =========================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tawseeltek API",
        Version = "v1",
        Description = "🚗 واجهة برمجية لتطبيق توصيلتك (Tawseeltek)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل التوكن بهذه الصيغة: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =========================================================
// ✅ Database
// =========================================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// =========================================================
// ✅ JWT Authentication
// =========================================================
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"]))
        };

        // 👇 دعم SignalR (token in WebSocket query)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/location") ||
                     path.StartsWithSegments("/hubs/ride")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// =========================================================
// ✅ Custom Services
// =========================================================
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<AppSettingsService>();
builder.Services.AddHttpClient<FirebaseV1Service>();
builder.Services.AddScoped<AzureBlobStorageService>();

// =========================================================
// ✅ SignalR
// =========================================================
builder.Services.AddSignalR();

// =========================================================
// ✅ CORS
// =========================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:5173",
                "https://tawseeltek.netlify.app",
                "https://mostafaalidragmeh.github.io",
                "https://tawseeltek.onrender.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// =========================================================
// ✅ Build App
// =========================================================
var app = builder.Build();

// =========================================================
// 🔥 Turbo Fix — HubContext BEFORE MapHub (مهم جدًا)
// =========================================================
LocationHub.HubContextRef = app.Services.GetRequiredService<IHubContext<LocationHub>>();
RideHub.HubContextRef = app.Services.GetRequiredService<IHubContext<RideHub>>();

// =========================================================
// Middleware
// =========================================================
app.UseHttpsRedirection();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")),
    RequestPath = "",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-store");
        ctx.Context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    }
});

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

// =========================================================
// ✅ SignalR Hubs
// =========================================================
app.MapHub<LocationHub>("/hubs/location");
app.MapHub<RideHub>("/hubs/ride");

// =========================================================
// Controllers
// =========================================================
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger"));

// =========================================================
// 🔐 Encrypt old passwords (one-time)
// =========================================================
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<TawseeltekAPI.Models.User>();

    var users = context.Users.ToList();
    bool updated = false;

    foreach (var user in users)
    {
        if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.Length < 60)
        {
            user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
            updated = true;
        }
    }

    if (updated)
    {
        context.SaveChanges();
        Console.WriteLine("✅ تم تحديث كلمات المرور القديمة.");
    }
    else
    {
        Console.WriteLine("ℹ️ لا يوجد كلمات مرور تحتاج التحديث.");
    }
}

// =========================================================
// 🚀 Run App
// =========================================================
app.Run();
