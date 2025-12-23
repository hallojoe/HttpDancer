// using HttpDancer.Utilities;
//
// namespace HttpDancer.Utilities.Tests;
//
// public class UrlNamingTests
// {
//     private readonly ICreateUrlPath _urlNaming = new UrlNaming();
//
//     [Test]
//     public void GetName_UsesPenultimateSegmentWhenNoFileExtension()
//     {
//         const string url = "https://www.example.com/da/Organdonation/Godt-at-vide-om-organdonation/Donation-efter-cirkulatorisk-doed";
//
//         var result = _urlNaming.GetName(url, includeQueryString: false);
//
//         Assert.That(result, Is.EqualTo("godt-at-vide-om-organdonation"));
//     }
//
//     [Test]
//     public void GetName_IncludesNormalizedQueryPartsWhenRequested()
//     {
//         const string url = "/-/media/Organdonation/TO-DOOO-Organdonation-Illustration_1600pixel.ashx?h=791&iar=0&w=1600&hash=9c1f387e";
//
//         var result = _urlNaming.GetName(url, includeQueryString: true, baseUrl: "https://www.example.com");
//
//         Assert.That(result, Is.EqualTo("to-dooo-organdonation-illustration-1600pixel.ashx.h.791.iar.0.w.1600.hash.9c1f387e"));
//     }
//
//     [Test]
//     public void GetName_OmitsQueryWhenNotRequested()
//     {
//         const string url = "https://example.com/files/document.pdf?download=true&size=full";
//
//         var result = _urlNaming.GetName(url, includeQueryString: false);
//
//         Assert.That(result, Is.EqualTo("document.pdf"));
//     }
//
//     [Test]
//     public void GetPath_RemovesExcludedSegmentsAndFileName()
//     {
//         const string url = "/da/-/media/folder/sub/xxx.ext?query=0";
//
//         var result = _urlNaming.GetPath(url, new[] { "-/media", "da" });
//
//         Assert.That(result, Is.EqualTo("/folder/sub/"));
//     }
//
//     [Test]
//     public void GetPath_NormalizesWithBaseUrlAndSpacing()
//     {
//         const string url = "library/My File_Name.pdf";
//
//         var result = _urlNaming.GetPath(url, Array.Empty<string>(), baseUrl: "https://example.com/root/");
//
//         Assert.That(result, Is.EqualTo("/root/library/"));
//     }
//
//     [Test]
//     public void GetName_ThrowsOnMissingUrl()
//     {
//         Assert.That(() => _urlNaming.GetName("", false), Throws.ArgumentException);
//     }
//
//     [Test]
//     public void GetPath_ThrowsOnMissingUrl()
//     {
//         Assert.That(() => _urlNaming.GetPath("   ", Array.Empty<string>()), Throws.ArgumentException);
//     }
// }
