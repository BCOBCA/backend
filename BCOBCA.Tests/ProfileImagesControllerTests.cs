using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BCOBCA.API.Controllers;
using BCOBCA.API.Services;
using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.DAL.Context;
using BCOBCA.DAL.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace BCOBCA.Tests;

public sealed class ProfileImagesControllerTests
{
    [Fact]
    public async Task OnlyActiveAccountsCanUploadProfileImagesWithoutAdminRole()
    {
        EnvironmentBootstrap.Load();
        var supplied = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Configure isolated PostgreSQL.");
        var target = new NpgsqlConnectionStringBuilder(supplied) { Database = "bcobca_profile_image_" + Guid.NewGuid().ToString("N") };
        await using var admin = new NpgsqlConnection(supplied); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(target.ConnectionString).Options);
            await db.Database.MigrateAsync();
            var account = new Account { Email = "profile-upload@example.test", Status = 1, Role = "Customer" };
            db.Accounts.Add(account); await db.SaveChangesAsync();
            var handler = new ImageProvider();
            using var client = new HttpClient(handler);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Cloudinary:CloudName"]="profile-test", ["Cloudinary:ApiKey"]="test-key", ["Cloudinary:ApiSecret"]="test-secret" }).Build();
            var controller = new ProfileImagesController(new CloudinaryImageService(client, config), db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            using var stream = new MemoryStream(bytes);
            var file = new FormFile(stream,0,bytes.Length,"file","avatar.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
            Assert.IsType<UnauthorizedResult>(await controller.Upload(file,default));
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub",account.Id.ToString())],"Test"));
            var response = Assert.IsType<OkObjectResult>(await controller.Upload(file,default));
            Assert.StartsWith("https://res.cloudinary.com/profile-test/image/upload/", Assert.IsType<ApiResponse<ImageUploadDto>>(response.Value).Data!.Url);
            Assert.Equal(1,handler.Calls);
            account.Status = 0; await db.SaveChangesAsync();
            Assert.IsType<UnauthorizedResult>(await controller.Upload(file,default));
            Assert.Equal(1,handler.Calls);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{target.Database}\" WITH (FORCE)",admin); await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class ImageProvider : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var id = await Assert.Single(form,c=>c.Headers.ContentDisposition?.Name?.Trim('"')=="public_id").ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { secure_url="https://res.cloudinary.com/profile-test/image/upload/"+id+".png",public_id=id,resource_type="image",format="png" })) };
        }
    }
}
