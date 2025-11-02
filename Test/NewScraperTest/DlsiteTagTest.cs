using System;
using System.Threading;
using System.Threading.Tasks;
using ScraperBackendService.Core.Net;
using ScraperBackendService.Providers.DLsite;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using ScraperBackendService.Core.Parsing;
using ScraperBackendService.Core.Normalize;

namespace Test.NewScraperTest
{
    public class DlsiteTagTest
    {
        public static async Task RunTestAsync()
        {
            Console.WriteLine("=== Testing DLsite Tag Extraction ===\n");
            
            // Create logger
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var logger = loggerFactory.CreateLogger<DlsiteProvider>();
            
            // Create network client and provider
            var networkClient = new HttpNetworkClient(loggerFactory.CreateLogger<HttpNetworkClient>());
            var provider = new DlsiteProvider(networkClient, logger);
            
            // Test the specific ID that shows empty tags
            var testId = "RJ01464954";
            Console.WriteLine($"🔍 Testing ID: {testId}");
            Console.WriteLine($"🔍 This should have tags like: 3D作品, ASMR, etc.");
            
            try
            {
                var detailUrl = $"https://www.dlsite.com/maniax/work/=/product_id/{testId}.html";
                Console.WriteLine($"📝 Testing URL: {detailUrl}");
                
                // First test: Use the provider directly
                Console.WriteLine("\n--- Testing Provider Method ---");
                var metadata = await provider.FetchDetailAsync(detailUrl, CancellationToken.None);
                
                if (metadata != null)
                {
                    Console.WriteLine($"✅ Metadata fetched successfully");
                    Console.WriteLine($"🏷️  ID: {metadata.ID}");
                    Console.WriteLine($"📝 Title: {metadata.Title}");
                    Console.WriteLine($"🏷️  Tags Count: {metadata.Tags.Count}");
                    
                    if (metadata.Tags.Count > 0)
                    {
                        Console.WriteLine("🎉 SUCCESS: Tags found via Provider!");
                        for (int i = 0; i < metadata.Tags.Count; i++)
                        {
                            Console.WriteLine($"   [{i + 1}] {metadata.Tags[i]}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ ERROR: No tags found via Provider");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to fetch metadata via Provider");
                }
                
                // Second test: Manual HTML analysis
                Console.WriteLine("\n--- Testing Manual HTML Analysis ---");
                var rawHtml = await networkClient.GetHtmlAsync(detailUrl, CancellationToken.None);
                if (!string.IsNullOrEmpty(rawHtml))
                {
                    Console.WriteLine("✅ Raw HTML retrieved successfully");
                    
                    var doc = new HtmlDocument();
                    doc.LoadHtml(rawHtml);
                    
                    // Test different XPath selectors
                    var selectors = new[]
                    {
                        "//table[@id='work_outline']//tr[.//th[contains(normalize-space(.),'ジャンル')]]//td//div[contains(@class,'main_genre')]//a",
                        "//table[@id='work_outline']//tr[.//th[contains(text(),'ジャンル')]]//td//a",
                        "//div[contains(@class,'main_genre')]//a",
                        "//table[@id='work_outline']//a[contains(@href,'genre')]",
                        "//a[contains(@href,'/genre_')]"
                    };
                    
                    for (int i = 0; i < selectors.Length; i++)
                    {
                        Console.WriteLine($"\n🔍 Testing Selector {i + 1}: {selectors[i]}");
                        
                        var nodes = HtmlEx.SelectNodes(doc, selectors[i]);
                        Console.WriteLine($"   Found {nodes.Count()} nodes");
                        
                        var tags = new List<string>();
                        foreach (var a in nodes)
                        {
                            var g = TextNormalizer.Clean(a.InnerText);
                            if (!string.IsNullOrEmpty(g))
                            {
                                tags.Add(g);
                                Console.WriteLine($"   - {g}");
                            }
                        }
                        
                        if (tags.Count > 0)
                        {
                            Console.WriteLine($"✅ Selector {i + 1} found {tags.Count} tags!");
                        }
                        else
                        {
                            Console.WriteLine($"❌ Selector {i + 1} found no tags");
                        }
                    }
                    
                    // Check for genre section existence
                    Console.WriteLine("\n--- HTML Structure Analysis ---");
                    
                    var genreHeader = doc.DocumentNode.SelectSingleNode("//th[contains(normalize-space(.),'ジャンル')]");
                    if (genreHeader != null)
                    {
                        Console.WriteLine("✅ Found genre header (ジャンル)");
                        
                        var genreRow = genreHeader.ParentNode;
                        if (genreRow != null)
                        {
                            Console.WriteLine("✅ Found genre row");
                            var genreTd = genreRow.SelectSingleNode(".//td");
                            if (genreTd != null)
                            {
                                Console.WriteLine("✅ Found genre cell");
                                Console.WriteLine($"📝 Genre cell innerHTML: {genreTd.InnerHtml.Substring(0, Math.Min(200, genreTd.InnerHtml.Length))}...");
                                
                                var mainGenreDivs = genreTd.SelectNodes(".//div[contains(@class,'main_genre')]");
                                if (mainGenreDivs != null && mainGenreDivs.Count > 0)
                                {
                                    Console.WriteLine($"✅ Found {mainGenreDivs.Count} main_genre divs");
                                    foreach (var div in mainGenreDivs)
                                    {
                                        var links = div.SelectNodes(".//a");
                                        if (links != null)
                                        {
                                            Console.WriteLine($"   - Div has {links.Count} links");
                                            foreach (var link in links)
                                            {
                                                Console.WriteLine($"     * Link: {link.InnerText} (href: {link.GetAttributeValue("href", "N/A")})");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"   - Div has no links: {div.InnerText}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("❌ No main_genre divs found");
                                }
                            }
                            else
                            {
                                Console.WriteLine("❌ Genre cell not found");
                            }
                        }
                        else
                        {
                            Console.WriteLine("❌ Genre row not found");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Genre header not found");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Failed to retrieve raw HTML");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception: {ex.Message}");
                Console.WriteLine($"💡 Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}