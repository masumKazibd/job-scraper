using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

class JobScraper
{
    // --- কনফিগারেশন ---
    private static readonly string SearchKeyword = "JAVA";
    private static readonly List<string> SkillsToFind = new List<string>
    {
        "java", "spring", "springboot", "spring boot", "hibernate", "jpa",
        "microservices", "rest", "sql", "oracle", "mysql", "postgresql"
    };

    // ফলাফল সংরক্ষণের জন্য
    private static Dictionary<string, HashSet<string>> companySkillsDb = new Dictionary<string, HashSet<string>>();

    static void Main(string[] args)
    {
        Console.WriteLine("--- Initializing Selenium WebDriver ---");

        // Chrome ড্রাইভার সেটআপ
        IWebDriver driver = new ChromeDriver();

        Console.WriteLine($"--- Starting Scraper for Keyword: '{SearchKeyword}' ---");

        int maxPages = 1; // টেস্ট করার জন্য শুধু ১টি পেইজ
        for (int pageNum = 1; pageNum <= maxPages; pageNum++)
        {
            string pageUrl = $"https://jobs.bdjobs.com/jobsearch.asp?txtsearch={SearchKeyword}&fcat=-1&qOT=0&iCat=0&Country=0&qPosted=0&qDeadline=0&Newspaper=0&qJobNature=0&qJobLevel=0&qExp=0&qAge=0&hidOrder=&pg={pageNum}&rpp=50&hidJobSearch=JobSearch&MPostings=&ver=&strFlid_fvalue=&strFilterName=&hClickLog=0&earlyAccess=0&fcatId=&hPopUpVal=1";
            //string pageUrl = $"https://jobs.bdjobs.com/jobsearch.asp?txtsearch={SearchKeyword}&pg={pageNum}&rpp=50";
            Console.WriteLine($"\n[INFO] Scraping Page: {pageNum}");

            try
            {
                driver.Navigate().GoToUrl(pageUrl);

                // জাভাস্ক্রিপ্ট লোড হওয়ার জন্য ৫ সেকেন্ড অপেক্ষা করা
                Console.WriteLine("[INFO] Waiting for JavaScript to load content...");
                Thread.Sleep(5000); // 5 সেকেন্ড অপেক্ষা

                // পেইজের সম্পূর্ণ HTML নেওয়া
                string pageHtml = driver.PageSource;
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(pageHtml);

                // সঠিক কন্টেইনার ক্লাস ব্যবহার করে জব পোস্টগুলো খুঁজে বের করা
                var jobContainers = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'norm-jobs-wrapper')]");

                if (jobContainers != null)
                {
                    Console.WriteLine($"[INFO] Found {jobContainers.Count} jobs on this page.");

                    foreach (var container in jobContainers)
                    {
                        // কন্টেইনারের ভেতর থেকে লিঙ্ক (a ট্যাগ) খুঁজে বের করা
                        var linkNode = container.SelectSingleNode(".//a");
                        if (linkNode != null)
                        {
                            string jobUrl = linkNode.GetAttributeValue("href", string.Empty);
                            Console.WriteLine($"\n  [INFO] Processing job: {jobUrl}");

                            // নতুন ট্যাবে জব ডিটেইলস পেইজ খোলা এবং ডেটা সংগ্রহ করা
                            ProcessJobDetailsPage(driver, jobUrl);
                            Thread.Sleep(2000);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("[WARNING] No job containers found.");
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] An error occurred on page {pageNum}. Error: {ex.Message}");
            }
        }

        driver.Quit();

        // --- ফলাফল প্রিন্ট করা ---
        Console.WriteLine("\n\n--- Final Company-Skill Analysis ---");
        if (companySkillsDb.Count == 0)
        {
            Console.WriteLine("Analysis complete. No companies with the specified skills were found.");
        }
        else
        {
            foreach (var entry in companySkillsDb.OrderBy(kvp => kvp.Key))
            {
                Console.WriteLine($"\nCompany: {entry.Key}");
                Console.WriteLine($"  Skills: {string.Join(", ", entry.Value.OrderBy(s => s))}");
            }
        }
    }

    private static void ProcessJobDetailsPage(IWebDriver driver, string url)
    {
        // নতুন ট্যাব খুলে সেখানে যাওয়া
        driver.SwitchTo().NewWindow(WindowType.Tab);
        driver.Navigate().GoToUrl(url);
        Thread.Sleep(3000); // পেইজ লোডের জন্য অপেক্ষা

        try
        {
            var detailDoc = new HtmlDocument();
            detailDoc.LoadHtml(driver.PageSource);

            // কোম্পানির নাম এবং স্কিল খুঁজে বের করা
            string companyName = detailDoc.DocumentNode.SelectSingleNode("//h2[contains(@class, 'company-name')]")?.InnerText.Trim() ?? "Unknown Company";
            string descriptionText = detailDoc.DocumentNode.SelectSingleNode("//div[contains(@class, 'job-description')]")?.InnerText.ToLower() ?? "";

            var foundSkills = new HashSet<string>();
            foreach (var skill in SkillsToFind)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(descriptionText, $@"\b{skill}\b"))
                {
                    foundSkills.Add(skill);
                }
            }

            if (companyName != "Unknown Company" && foundSkills.Count > 0)
            {
                if (!companySkillsDb.ContainsKey(companyName))
                {
                    companySkillsDb[companyName] = new HashSet<string>();
                }
                companySkillsDb[companyName].UnionWith(foundSkills);
                Console.WriteLine($"  ✅ SUCCESS: Found skills for '{companyName}': {string.Join(", ", foundSkills)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Error processing job details: {ex.Message}");
        }
        finally
        {
            driver.Close();
            driver.SwitchTo().Window(driver.WindowHandles.First());
        }
    }
}