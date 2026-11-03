using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SistemaDeChaves.Components;
using SistemaDeChaves.DAO;
using SistemaDeChaves.Models;
using SistemaDeChaves.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "sistema_chaves";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<DatabaseConnection>();
builder.Services.AddScoped<CategoriasDAO>();
builder.Services.AddScoped<UsuariosDAO>();
builder.Services.AddScoped<ChavesDAO>();
builder.Services.AddScoped<MovimentacoesDAO>();
builder.Services.AddScoped<PainelDAO>();
builder.Services.AddScoped<AlertasDAO>();
builder.Services.AddScoped<ConfiguracoesDAO>();
builder.Services.AddScoped<RelatoriosDAO>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddHostedService<AtrasoMonitorService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/");

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/entrar", async (HttpContext context, AuthService auth) =>
{
    var form = await context.Request.ReadFormAsync();
    var usuario = await auth.EntrarAsync(
        form["codigo"].ToString(),
        form["senha"].ToString(),
        context.RequestAborted);

    if (usuario is null)
        return Results.Redirect("/login?erro=1");

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
        new Claim(ClaimTypes.Name, usuario.Nome),
        new Claim("codigo", usuario.Codigo),
        new Claim(ClaimTypes.Role, usuario.NivelAcesso)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false });
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapPost("/auth/sair", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).RequireAuthorization();

app.MapPost("/auth/recuperar", async (
    HttpContext context,
    UsuariosDAO usuarios,
    IPasswordHasher<Usuario> passwordHasher) =>
{
    var form = await context.Request.ReadFormAsync();
    var codigo = form["codigo"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    var senha = form["senha"].ToString();
    var confirmacao = form["confirmacao"].ToString();
    if (senha.Length < 8 || senha != confirmacao)
        return Results.Redirect("/recuperar-senha?erro=1");

    var usuario = await usuarios.ObterPorCodigoAsync(codigo, context.RequestAborted);
    if (usuario is null || !string.Equals(usuario.Email, email, StringComparison.OrdinalIgnoreCase))
        return Results.Redirect("/recuperar-senha?erro=1");

    var hash = passwordHasher.HashPassword(usuario, senha);
    var atualizado = await usuarios.AlterarSenhaAsync(codigo, email, hash, context.RequestAborted);
    return Results.Redirect(atualizado ? "/login?senha=ok" : "/recuperar-senha?erro=1");
}).AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

await app.RunAsync();
