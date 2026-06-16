using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RenewalOps.Application.DTOs.Auth;
using RenewalOps.Application.DTOs.Documents;
using RenewalOps.Domain.Enums;
using RenewalOps.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RenewalOps.IntegrationTests;

public class DocumentUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DocumentUploadTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetTokenViaRegisterAsync(string email = "testuser@test.com", string password = "TestPass123!")
    {
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password,
            ConfirmPassword = password
        };
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        if (regResponse.StatusCode == HttpStatusCode.BadRequest)
        {
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login should succeed for already-registered user");
            var loginToken = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
            return loginToken!.AccessToken;
        }

        regResponse.StatusCode.Should().Be(HttpStatusCode.OK, "registration should succeed");
        var token = await regResponse.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        return token!.AccessToken;
    }

    [Fact]
    public async Task Upload_Document_Should_Return_Created_With_ExpiryDate_And_AuditEvents()
    {
        var token = await GetTokenViaRegisterAsync("upload-test@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = new MultipartFormDataContent();
        var fileBytes = "fake PDF content for testing"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "test-passport.pdf");
        content.Add(new StringContent("Test Passport"), "title");
        content.Add(new StringContent("Passport"), "documentType");

        var uploadResponse = await _client.PostAsync("/api/documents", content);

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        doc.Should().NotBeNull();
        doc!.Title.Should().Be("Test Passport");
        doc.ExpiryDate.Should().NotBeNull("the fake OCR service returns an expiry date");
        doc.ExpiryDate!.Value.Year.Should().Be(2025);
        doc.RawExtractedText.Should().Contain("Expiry Date");

        var getResponse = await _client.GetAsync($"/api/documents/{doc.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);
        fetched!.Id.Should().Be(doc.Id);
        fetched.ExpiryDate.Should().Be(doc.ExpiryDate);

        // Phase 1 acceptance: verify two AuditEvents (DocumentUploaded + DocumentViewed) exist in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditEvents = await db.AuditEvents
            .Where(a => a.DocumentId == doc.Id)
            .OrderBy(a => a.CreatedUtc)
            .ToListAsync();

        auditEvents.Should().HaveCountGreaterThanOrEqualTo(2,
            "upload should write DocumentUploaded and the GET should write DocumentViewed");
        auditEvents.Should().Contain(a => a.Action == nameof(AuditAction.DocumentUploaded));
        auditEvents.Should().Contain(a => a.Action == nameof(AuditAction.DocumentViewed));
    }

    [Fact]
    public async Task Upload_Then_List_Should_Return_Documents()
    {
        var token = await GetTokenViaRegisterAsync("list-test@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = new MultipartFormDataContent();
        var fileBytes = "test content"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "license.jpg");
        content.Add(new StringContent("Driver License"), "title");
        content.Add(new StringContent("License"), "documentType");

        await _client.PostAsync("/api/documents", content);

        var listResponse = await _client.GetAsync("/api/documents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<DocumentListResponse>(JsonOptions);
        list.Should().NotBeNull();
        list!.Items.Should().NotBeEmpty();
        list.TotalCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Unauthenticated_Request_Should_Return_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/documents");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Document_Should_Soft_Delete()
    {
        var token = await GetTokenViaRegisterAsync("delete-test@test.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var content = new MultipartFormDataContent();
        var fileBytes = "delete test"u8.ToArray();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "warranty.png");
        content.Add(new StringContent("Warranty Card"), "title");
        content.Add(new StringContent("Warranty"), "documentType");

        var uploadResponse = await _client.PostAsync("/api/documents", content);
        var doc = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions);

        var deleteResponse = await _client.DeleteAsync($"/api/documents/{doc!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/documents/{doc.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
