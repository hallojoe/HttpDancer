// using HttpDancer.Core.Naming;
// using HttpDancer.Core.Naming.Hashing;
// using HttpDancer.Core.Naming.Query;
// using HttpDancer.Core.Naming.Segments;
// using HttpDancer.Core.Tests.TestData;
// using Microsoft.Extensions.Options;
//
// namespace HttpDancer.Core.Tests;
//
// public class UrlNamerTests
// {
//     private static UrlNamer CreateNamer(UrlNamingOptions options)
//     {
//         var pathSegmentNormalizer = new PathSegmentNormalizer();
//         return new UrlNamer(
//             Options.Create(options),
//             new PathSegmentFilter(),
//             pathSegmentNormalizer,
//             new QueryStringProcessor(pathSegmentNormalizer, new Base62HashGenerator()));
//     }
//
//     [TestCaseSource(typeof(NamingTestData), nameof(NamingTestData.GetCases))]
//     public void Urls_Are_Named_As_Configured(string configName, UrlNamingOptions options, UrlCase testCase)
//     {
//         var namer = CreateNamer(options);
//
//         var result = namer.GetNameAndPath(testCase.Url);
//
//         if (testCase.ExpectedPath is not null)
//         {
//             Assert.That(result.Path, Is.EqualTo(testCase.ExpectedPath));
//         }
//
//         if (testCase.ExpectedPathStartsWith is not null)
//         {
//             Assert.That(result.Path, Does.StartWith(testCase.ExpectedPathStartsWith));
//         }
//
//         if (testCase.ExpectedName is not null)
//         {
//             Assert.That(result.Name, Is.EqualTo(testCase.ExpectedName));
//         }
//
//         if (testCase.ExpectedNameStartsWith is not null)
//         {
//             Assert.That(result.Name, Does.StartWith(testCase.ExpectedNameStartsWith));
//         }
//
//         if (testCase.ExpectedQueryHashLength.HasValue)
//         {
//             var length = testCase.ExpectedQueryHashLength.Value;
//             if (length == 0)
//             {
//                 Assert.That(result.QueryHash, Is.Null.Or.Length.EqualTo(0));
//             }
//             else
//             {
//                 Assert.That(result.QueryHash, Is.Not.Null.And.Length.EqualTo(length));
//             }
//         }
//     }
// }
