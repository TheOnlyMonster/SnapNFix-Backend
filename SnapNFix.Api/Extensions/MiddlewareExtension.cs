namespace SnapNFix.Api.Extensions;

public static class MiddlewareExtension
{
    public static WebApplication UseWebApiMiddleware(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsProduction())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SnapNFix API V1");
                c.OAuthClientId("swagger-ui");
                c.OAuthAppName("Swagger UI");
            });
        }
        
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            MinimumSameSitePolicy = SameSiteMode.Strict
        });
        app.UseHsts();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        
        // Add localization middleware here
        app.UseRequestLocalization();
        
        //app.UseMiddleware<IpRateLimitingMiddleware>();
        //app.UseRateLimiter();
        app.UseCors("DefaultPolicy");
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        // app.MapHealthChecks("/health");

        return app;
    }
}