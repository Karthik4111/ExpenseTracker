using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

//Dependency Injection
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//Sync Fusion Licence Key
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF5cXGRCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXZcdHRXRmhcVkRxXUVWYEg=");

var app = builder.Build();

// Configure QuestPDF license before handling requests or generating PDFs
// Use LicenseType.Community only if your organization qualifies for the Community license.
// See https://www.questpdf.com/license/ for details.
QuestPDF.Settings.License = LicenseType.Community;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
