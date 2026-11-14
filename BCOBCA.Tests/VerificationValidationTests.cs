using BCOBCA.BLL.Common;
using BCOBCA.BLL.DTOs;
using BCOBCA.BLL.Services;
using Xunit;

namespace BCOBCA.Tests;

public sealed class VerificationValidationTests
{
    private static VerificationFileInput File(string name = "document.pdf", string mime = "application/pdf", byte[]? content = null)
        => new("license", name, mime, content ?? "%PDF-1.7\nexample\n%%EOF"u8.ToArray());

    [Fact]
    public void AcceptsPdfJpegAndPngWithMatchingSignatures()
    {
        VerificationDocuments.Validate(File());
        VerificationDocuments.Validate(File("document.jpg", "image/jpeg", [255, 216, 255, 224]));
        VerificationDocuments.Validate(File("document.png", "image/png", [137, 80, 78, 71, 13, 10, 26, 10]));
    }

    [Theory]
    [InlineData("fake.pdf", "application/pdf", "<html>fake</html>")]
    [InlineData("fake.png", "image/png", "%PDF-1.7")]
    [InlineData("fake.svg", "image/svg+xml", "<svg/>")]
    [InlineData("fake.pdf", "image/png", "%PDF-1.7")]
    public void RejectsSpoofedOrUnsupportedContent(string name, string mime, string content)
        => Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File(name, mime, System.Text.Encoding.UTF8.GetBytes(content))));

    [Fact]
    public void RejectsEmptyOversizedAndUnsafeNames()
    {
        Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File(content: [])));
        Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File(content: new byte[VerificationDocuments.MaxFileBytes + 1])));
        Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File("../private.pdf")));
        Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File("bad\r\nname.pdf")));
        Assert.Throws<BusinessException>(() => VerificationDocuments.Validate(File(new string('a', 256) + ".pdf")));
    }
}
