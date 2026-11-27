using DocumentManagement.Api.Data;
using DocumentManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

var app = builder.Build();

// Apply pending migrations on startup so the database is created automatically.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    // Generates the API description at /openapi/v1.json
    app.MapOpenApi();

    // Shows the Swagger page at /swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Document Management API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();