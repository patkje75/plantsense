using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlantSense.Extensions;
using PlantSense.Services;
using Serilog;
using Serilog.Formatting.Json;
using System;
using System.Text;

namespace PlantSense
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            Serilog.Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Logger(l =>
                {
                    l.WriteTo.File(
                        new JsonFormatter(renderMessage: true),
                        "applog-.json",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 21,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        shared: true,
                        encoding: Encoding.UTF8);
                    l.Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AppLog"));
                })
                .WriteTo.Logger(l =>
                {
                    l.WriteTo.File(
                        new JsonFormatter(renderMessage: true),
                        "syslog-.json",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 21,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        shared: true,
                        encoding: Encoding.UTF8);
                    l.Filter.ByExcluding(e => e.Properties.ContainsKey("AppLog"));
                })
                .CreateLogger();
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSwaggerGen();

            services.AddCronJob<MaintenanceSrvCronJob>(c =>
            {
                c.TimeZoneInfo = TimeZoneInfo.Local;
                // Every minute — time-triggered pumps can be scheduled for any HH:mm, not just fixed slots
                c.CronExpression = @"* * * * *";
            });

            // Singletons so controllers can trigger live resubscription after config changes
            services.AddSingleton<ZWaveMqttService>();
            services.AddHostedService(sp => sp.GetRequiredService<ZWaveMqttService>());
            services.AddSingleton<ZigbeeMqttService>();
            services.AddHostedService(sp => sp.GetRequiredService<ZigbeeMqttService>());

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseExceptionHandlerMiddleware();

            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging();

            // Add security response headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Add("X-Frame-Options", "DENY");
                context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                await next();
            });

            app.UseRouting();

            app.UseAuthorization();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlantSense API");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
