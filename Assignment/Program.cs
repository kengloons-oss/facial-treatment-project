

//var builder = WebApplication.CreateBuilder(args);
//var app = builder.Build();

//app.MapGet("/", () => "Hello World!");

//app.Run();

global using Assignment.Models;
global using Assignment.Services;
global using Assignment;
using Stripe;
using Assignment.Services.QRCodeGeneratorHelper;
using qr = Assignment.Services.QRCodeGeneratorHelper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<Helpers>();
builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddScoped<InvoiceRenderingService>();
builder.Services.AddScoped<IQRCodeGeneratorHelper, qr.QRCodeGeneratorHelper>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for GDPR compliance
});

builder.Services.AddSession(); // for apply session
builder.Services.AddDataProtection(); // for apply data protection
builder.Services.AddHttpContextAccessor(); // Required for Helpers.Configure

StripeConfiguration.ApiKey = builder.Configuration.GetSection("Stripe:SecretKey").Get<string>();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Login}/{id?}"
);
app.UseSession();
app.UseAuthorization();

// Configure Helpers with HttpContextAccessor
Helpers.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());

app.Run();
