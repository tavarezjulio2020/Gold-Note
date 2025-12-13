using GoldNote.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using GoldNote.Models.Student;
using GoldNote.Models.Teacher;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
//builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.AddSingleton<GoldNoteDbContext>();
builder.Services.AddScoped<StudentModel>(); 
builder.Services.AddScoped<GoldNote.Models.LeaderBoard.LeaderBoardRepository>();
builder.Services.AddScoped<GoldNote.Models.Teacher.Teacher>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // ***FIX 1: This MUST point to your Login *ACTION* in AccountController***
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(180);
        options.SlidingExpiration = true;
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ***FIX 2: Fix Middleware Order***
// Authentication and Authorization must come AFTER UseRouting
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();