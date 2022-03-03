using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using APIApplication.Services;
using Microsoft.AspNetCore.HttpOverrides;
using EncryptTransactionKey.DataContext;
using EncryptTransactionKey.Model;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using System;

namespace APIApplication
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            List<API> apis = new List<API>();
            Configuration.GetSection("APIs").Bind(apis);
            services.AddControllers(option => option.EnableEndpointRouting = false);
            services.AddSingleton<IDapper, Services.Dapper>();
            services.AddSingleton<List<API>>(apis);
            services.AddSingleton<IDbContext, DbContext>();
            services.AddSwaggerGen(option =>
            {
                option.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title="Netwrok APIs",
                    Version = "v1.1",
                    Description="Apis to perform encrypt network keys and genrate wallet addresses",
                    TermsOfService = new Uri("https://github.com/np4652"),
                    Contact = new OpenApiContact
                    {
                        Name = "Amit Singh",
                        Email = "np4652@gmail.com",
                        Url = new Uri("https://github.com/np4652"),
                    },
                    License = new OpenApiLicense
                    {
                        Name = "API LICX",
                        Url = new Uri("https://github.com/np4652"),
                    }
                });
                option.SwaggerDoc("v2", new OpenApiInfo
                {
                    Title = "",
                    Version = "v1.2",
                    Description = "Network APIs 2.1"
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseHttpsRedirection();
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });
            app.UseRouting();
            app.UseAuthorization();
             app.UseSwagger();
            app.UseSwaggerUI(c=> {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
                c.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
           
        }
    }
}
