using System;
using ScraperBackendService.Core.Routing;

namespace Test.NewScraperTest
{
    public class DlsiteUrlTest
    {
        public static void TestUrlBuilding()
        {
            Console.WriteLine("Testing DLsite URL building...");
            
            // Test RJ ID - should go to Maniax
            var rjUrl = IdParsers.BuildDlsiteDetailUrl("RJ123456", preferManiax: false);
            Console.WriteLine($"RJ123456 -> {rjUrl}");
            Console.WriteLine($"Expected: https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");
            Console.WriteLine($"Correct: {rjUrl.Contains("/maniax/")}");
            
            // Test VJ ID - should go to Pro
            var vjUrl = IdParsers.BuildDlsiteDetailUrl("VJ123456", preferManiax: false);
            Console.WriteLine($"VJ123456 -> {vjUrl}");
            Console.WriteLine($"Expected: https://www.dlsite.com/pro/work/=/product_id/VJ123456.html");
            Console.WriteLine($"Correct: {vjUrl.Contains("/pro/")}");
            
            // Test with preferManiax true for RJ (should still go to Maniax)
            var rjUrlPreferManiax = IdParsers.BuildDlsiteDetailUrl("RJ123456", preferManiax: true);
            Console.WriteLine($"RJ123456 (prefer Maniax) -> {rjUrlPreferManiax}");
            Console.WriteLine($"Correct: {rjUrlPreferManiax.Contains("/maniax/")}");
            
            // Test with preferManiax true for VJ (should still go to Pro)
            var vjUrlPreferManiax = IdParsers.BuildDlsiteDetailUrl("VJ123456", preferManiax: true);
            Console.WriteLine($"VJ123456 (prefer Maniax) -> {vjUrlPreferManiax}");
            Console.WriteLine($"Correct: {vjUrlPreferManiax.Contains("/pro/")}");
        }
    }
}