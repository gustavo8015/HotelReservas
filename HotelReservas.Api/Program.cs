using System.Text;
using HotelReservas.Api.Asistente;
using HotelReservas.Api.Data;
using HotelReservas.Api.Seguridad;
using HotelReservas.Api.Servicios;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Hotel Reservas - API REST (ASP.NET Core 8). Capa de lógica de negocio y de acceso a datos de la
// arquitectura en capas (sección 4): la app .NET MAUI solo habla con esta API por HTTPS + JWT y la
// API es el único punto que toca la base de datos SQL Server.
var builder = WebApplication.CreateBuilder(args);

// Capa de acceso a datos
builder.Services.AddDbContext<HotelReservasContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("HotelReservas")));

// Seguridad (RNF01): JWT firmado con clave simétrica, autorización por perfil
var clave = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)),
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<ServicioToken>();

// Lógica de negocio
builder.Services.AddScoped<ServicioReservas>();

// Servicio de IA (sección 13): catálogo de funciones de lectura, intérpretes y orquestador
builder.Services.AddScoped<CatalogoFunciones>();
builder.Services.AddScoped<InterpreteReglas>();
builder.Services.AddHttpClient<InterpreteAzureOpenAI>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddScoped<OrquestadorAsistente>();

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Hotel Reservas API", Version = "v0.3", Description = "Sistema transaccional de reservas de habitaciones. Incluye el asistente de consultas en lenguaje natural (solo lectura)." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Pegue aquí el token devuelto por POST /api/auth/login" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok", fecha = DateTime.Now }));

app.Run();
