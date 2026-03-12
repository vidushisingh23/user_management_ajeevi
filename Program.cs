using DemoAuth.Data;
using DemoAuth.Auth;
using DemoAuth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using DemoAuth.Models;

var builder = WebApplication.CreateBuilder(args);

// helper to avoid exceptions during design-time when service collection is read-only
static void TryServiceRegistration(Action register, string name)
{
    try { register(); }
    catch (InvalidOperationException ex) when (ex.Message?.Contains("read-only", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine($"[DesignTime] Skipping service registration: {name} ({ex.Message})");
    }
}

// ----------------------------
// 1) determine connection string & provider (env first, fallback to appsettings)
// ----------------------------
GlobalModel.ConnectionString = Environment.GetEnvironmentVariable("ConnectionString");

if (!string.IsNullOrEmpty(GlobalModel.ConnectionString))
{
    GlobalModel.IsMSSQL = Convert.ToBoolean(Environment.GetEnvironmentVariable("IsMSSQL"));
    GlobalModel.ConnectionString = Environment.GetEnvironmentVariable("ConnectionString");
    GlobalModel.ProjectKey = Environment.GetEnvironmentVariable("ProjectKey");
    GlobalModel.LicenseKey = Environment.GetEnvironmentVariable("LicenseKey");
}
else
{
    GlobalModel.IsMSSQL = Convert.ToBoolean(builder.Configuration.GetConnectionString("IsMSSQL"));
    GlobalModel.ConnectionString = builder.Configuration.GetConnectionString("ConnectionString");
    GlobalModel.ProjectKey = builder.Configuration.GetConnectionString("ProjectKey");
    GlobalModel.LicenseKey = builder.Configuration.GetConnectionString("LicenseKey");
}



if (string.IsNullOrEmpty(GlobalModel.ConnectionString))
    throw new Exception("No valid database connection string found in ENV or appsettings.");

// ----------------------------
// 2) register DbContext with proper provider
// ----------------------------
TryServiceRegistration(() =>
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        if (GlobalModel.IsMSSQL)
        {
            options.UseSqlServer(GlobalModel.ConnectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            Console.WriteLine("Using SQL Server Database Provider");
        }
        else
        {
            var mysqlVersion = new MySqlServerVersion(new Version(8, 0, 33));

            options.UseMySql(GlobalModel.ConnectionString, mysqlVersion,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            Console.WriteLine("Using MySQL Database Provider");
        }
    });
}, "AddDbContext<ApplicationDbContext>");

// ----------------------------
// 3) Identity & rest of app
// ----------------------------
TryServiceRegistration(() =>
{
    builder.Services.AddIdentity<IdentityUser, IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
}, "Identity");

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var jwt = builder.Configuration.GetSection("Jwt");
TryServiceRegistration(() =>
{
    builder.Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });
}, "Authentication/JWT");

TryServiceRegistration(() =>
{
    builder.Services.AddAuthorization();
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
}, "Authorization");

TryServiceRegistration(() =>
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });
}, "CORS");

TryServiceRegistration(() =>
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "DemoAuth API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste only the JWT (no 'Bearer ' prefix)."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });
}, "Swagger");

TryServiceRegistration(() =>
{
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddControllers().AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.WriteIndented = true;
    });
    builder.Services.AddEndpointsApiExplorer();
}, "AppServices+Controllers");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://127.0.0.1:5500")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ----------------------------
// 4) Apply migrations & seed default admin
// ----------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Run pending migrations (if any)
        try
        {
            Console.WriteLine("Checking and applying migrations...");
            dbContext.Database.Migrate(); // This will create DB if missing
            Console.WriteLine("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Migration Error] {ex.Message}");
        }

        // Seed default Admin role + user
        await SeedDefaultAdminAsync(userManager, roleManager);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Seed Error] {ex.Message}");
    }
}

app.Run();

// ----------------------------
// 5) Helper: Seed default admin user
// ----------------------------
static async Task SeedDefaultAdminAsync(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
{
    var adminEmail = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_EMAIL") ?? "admin@demo.com";
    var adminPassword = Environment.GetEnvironmentVariable("DEFAULT_ADMIN_PASSWORD") ?? "Admin@123";
    const string adminRole = "Admin";

    // 1? Ensure role exists
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRole));
        Console.WriteLine($"[Seed] Role '{adminRole}' created.");
    }

    // 2? Ensure user exists
    var existingUser = await userManager.FindByEmailAsync(adminEmail);
    if (existingUser == null)
    {
        var user = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, adminRole);
            Console.WriteLine($"[Seed] Admin user '{adminEmail}' created and assigned to '{adminRole}' role.");
        }
        else
        {
            Console.WriteLine($"[Seed Error] Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        // Ensure admin user is in Admin role
        if (!await userManager.IsInRoleAsync(existingUser, adminRole))
        {
            await userManager.AddToRoleAsync(existingUser, adminRole);
            Console.WriteLine($"[Seed] Existing admin '{adminEmail}' assigned to '{adminRole}' role.");
        }
        else
        {
            Console.WriteLine($"[Seed] Admin user '{adminEmail}' already exists with '{adminRole}' role.");
        }
    }
}