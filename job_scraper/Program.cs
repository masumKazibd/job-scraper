using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

class JobScraper
{
    private static readonly string SearchKeyword = "Software";
    private static readonly List<string> SkillsToFind = new List<string>
{
    // C# and .NET Ecosystem
    "c#", ".net", "dotnet", ".net core", "asp.net", "asp.net core", "dotnet core",
    "asp.net mvc", "mvc", "web api", "restful api", "rest", "api",
    "entity framework", "ef core", "dapper", "ado.net", "linq",
    "blazor", "razor pages", "signalr", "wpf", "wcf", "f#", "vb.net",

    // Node.js & JavaScript Backend
    "node.js", "nodejs", "node", "express.js", "express", "nestjs",
    "npm", "yarn",

    // Frontend & Angular Ecosystem
    "angular", "angularjs", "typescript", "javascript", "rxjs",
    "ngrx", "ngxs", "angular material", "primeng", "karma", "jasmine",
    "react", "vue", "jquery", "html", "css", "scss",
    
    // Programming Languages (Other)
    "java", "python", "dart", "go", "kotlin", "php", "scala",

    // Frameworks & Platforms (Java)
    "spring", "springboot", "spring boot", "hibernate", "jpa", "maven",

    // Architecture & Concepts
    "oop", "microservices", "clean architecture", "domain driven design", "ddd",
    "solid", "design patterns", "state management",

    // Databases
    "sql", "sql server", "mssql", "mysql", "postgresql", "oracle",
    "mongodb", "redis", "elasticsearch", "nosql",
    
    // DevOps & Tools
    "docker", "kubernetes", "k8s", "jenkins", "git", "gitlab", "cicd", "ci/cd",
    "azure devops", "terraform",
    
    // Cloud
    "aws", "azure", "gcp"
};

    // ফলাফল সংরক্ষণের জন্য
    private static Dictionary<string, HashSet<string>> companySkillsDb = new Dictionary<string, HashSet<string>>();

    static void Main(string[] args)
    {
        Console.WriteLine("--- Initializing Selenium WebDriver ---");

        // Chrome ড্রাইভার সেটআপ
        var options = new ChromeOptions();
        options.AddArgument("--headless"); // ব্রাউজার উইন্ডো না দেখিয়ে ব্যাকগ্রাউন্ডে চালানোর জন্য
        options.AddArgument("--log-level=3"); // কনসোলে অপ্রয়োজনীয় বার্তা না দেখানোর জন্য
        IWebDriver driver = new ChromeDriver(options);

        Console.WriteLine($"--- Starting Scraper for Keyword: '{SearchKeyword}' ---");

        int maxPages = 2; // ২টি পেইজ থেকে ডেটা সংগ্রহ করা হবে
        for (int pageNum = 1; pageNum <= maxPages; pageNum++)
        {
            string pageUrl = $"https://jobs.bdjobs.com/jobsearch.asp?txtsearch={SearchKeyword}&fcat=-1&qOT=0&iCat=0&Country=0&qPosted=0&qDeadline=0&Newspaper=0&qJobNature=0&qJobLevel=0&qExp=0&qAge=0&hidOrder=&pg={pageNum}&rpp=50&hidJobSearch=JobSearch";
            Console.WriteLine($"\n[INFO] Scraping Page: {pageNum}");

            try
            {
                driver.Navigate().GoToUrl(pageUrl);
                Console.WriteLine("[INFO] Waiting for page content to load...");
                Thread.Sleep(5000);

                string pageHtml = driver.PageSource;
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(pageHtml);

                var jobContainers = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'norm-jobs-wrapper')]");

                if (jobContainers != null)
                {
                    Console.WriteLine($"[INFO] Found {jobContainers.Count} jobs on this page.");

                    foreach (var container in jobContainers)
                    {
                        var linkNode = container.SelectSingleNode(".//a");
                        if (linkNode != null)
                        {
                            string jobUrl = linkNode.GetAttributeValue("href", string.Empty);
                            ProcessJobDetailsPage(driver, jobUrl);
                            Thread.Sleep(1000);
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
        var originalTab = driver.CurrentWindowHandle;
        try
        {
            driver.SwitchTo().NewWindow(WindowType.Tab);
            driver.Navigate().GoToUrl(url);
            Thread.Sleep(3000);

            var detailDoc = new HtmlDocument();
            detailDoc.LoadHtml(driver.PageSource);

            string companyName = detailDoc.DocumentNode.SelectSingleNode("(//h2)[1]")?.InnerText.Trim() ?? "Unknown Company";
            string descriptionText = detailDoc.GetElementbyId("responsibilitiesSection")?.InnerText.ToLower() ?? "";

            var foundSkills = new HashSet<string>();
            if (!string.IsNullOrEmpty(descriptionText))
            {
                foreach (var skill in SkillsToFind)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(descriptionText, $@"\b{skill}\b"))
                    {
                        foundSkills.Add(skill);
                    }
                }
            }

            if (companyName != "Unknown Company" && foundSkills.Count > 0)
            {
                if (!companySkillsDb.ContainsKey(companyName))
                {
                    companySkillsDb[companyName] = new HashSet<string>();
                }
                companySkillsDb[companyName].UnionWith(foundSkills);
                Console.WriteLine($"  ✅ SUCCESS: Found skills for '{companyName}'");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Error processing job details: {ex.Message}");
        }
        finally
        {
            driver.Close();
            driver.SwitchTo().Window(originalTab);
        }
    }
}