using System.Net.Http.Headers;

namespace Opsynex.IntegrationTests.Uploads;

internal static class UploadClient
{
    public static readonly string SampleDirectory = Path.Combine(AppContext.BaseDirectory, "sample-data");

    public static Task<HttpResponseMessage> UploadSampleAsync(this HttpClient client, string file, Guid? companyId = null, string? fileType = null) =>
        client.UploadBytesAsync(File.ReadAllBytes(Path.Combine(SampleDirectory, file)), file, companyId, fileType);

    public static async Task<HttpResponseMessage> UploadBytesAsync(this HttpClient client, byte[] content, string fileName, Guid? companyId = null, string? fileType = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (companyId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "companyId");
        }

        if (fileType is not null)
        {
            form.Add(new StringContent(fileType), "fileType");
        }

        return await client.PostAsync("/api/v1/uploads", form, TestContext.Current.CancellationToken);
    }
}
