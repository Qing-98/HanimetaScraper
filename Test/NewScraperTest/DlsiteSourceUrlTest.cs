using System;
using System.Threading;
using System.Threading.Tasks;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers.DLsite;
using Microsoft.Extensions.Logging;

namespace Test.NewScraperTest
{
    public class DlsiteSourceUrlTest
    {
        public static async Task RunTestAsync()
        {
            Console.WriteLine("=== Testing DLsite SourceUrls Fix ===\n");
            
            // Create logger
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            var logger = loggerFactory.CreateLogger<DlsiteProvider>();
            
            // Create network client and provider
            var networkClient = new HttpNetworkClient(loggerFactory.CreateLogger<HttpNetworkClient>());
            var provider = new DlsiteProvider(networkClient, logger);
            
            // Test VJ ID to check if SourceUrls now contains only one URL
            var testId = "VJ01003150";
            Console.WriteLine($"🔍 Testing ID: {testId}");
            
            try
            {
                var detailUrl = provider.BuildDetailUrlById(testId);
                Console.WriteLine($"📝 Built URL: {detailUrl}");
                
                var metadata = await provider.FetchDetailAsync(detailUrl, CancellationToken.None);
                
                if (metadata != null)
                {
                    Console.WriteLine($"✅ Metadata fetched successfully");
                    Console.WriteLine($"🏷️  ID: {metadata.ID}");
                    Console.WriteLine($"📝 Title: {metadata.Title}");
                    Console.WriteLine($"🔗 SourceUrls Count: {metadata.SourceUrls.Count}");
                    
                    for (int i = 0; i < metadata.SourceUrls.Count; i++)
                    {
                        Console.WriteLine($"   [{i + 1}] {metadata.SourceUrls[i]}");
                    }
                    
                    // Check if only one URL is returned
                    if (metadata.SourceUrls.Count == 1)
                    {
                        Console.WriteLine("🎉 SUCCESS: Only one SourceUrl returned (as expected)");
                        
                        // Check if the URL is the correct one for VJ (should be pro site)
                        if (metadata.SourceUrls[0].Contains("/pro/"))
                        {
                            Console.WriteLine("✅ CORRECT: VJ ID correctly maps to pro site");
                        }
                        else
                        {
                            Console.WriteLine("❌ ERROR: VJ ID should map to pro site");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ ERROR: Expected 1 SourceUrl, got {metadata.SourceUrls.Count}");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to fetch metadata");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception: {ex.Message}");
            }
            
            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}