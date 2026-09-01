namespace Regira.Web.Analytics.Services;

/// <summary>
/// Compiled in rather than packed as a content file: NuGet content does not reach a consuming project
/// the way a ProjectReference's does. Extend or replace via Analytics:BotDetection.
/// </summary>
internal static class BotDetectorDefaults
{
    /// <summary>"cubot" (a phone brand) would otherwise trip the broad "bot" marker.</summary>
    public static readonly string[] Exceptions = ["cubot"];

    public static readonly string[] Markers =
    [
        // generic
        "bot",
        "crawler",
        "spider",
        "crawl",
        "slurp",
        "scraper",
        "archiver",
        "fetcher",
        // search engines / previewers
        "bingpreview",
        "facebookexternalhit",
        "embedly",
        "quora link preview",
        "skypeuripreview",
        "whatsapp",
        "telegrambot",
        "slackbot",
        "discordbot",
        "linkedinbot",
        "pinterest",
        // AI crawlers
        "gptbot",
        "claudebot",
        "claude-web",
        "anthropic-ai",
        "ccbot",
        "perplexitybot",
        "bytespider",
        "google-extended",
        "cohere-ai",
        "diffbot",
        "omgili",
        "timpibot",
        "youbot",
        "amazonbot",
        // SEO / marketing
        "ahrefs",
        "semrush",
        "mj12",
        "dotbot",
        "petalbot",
        "dataforseo",
        "blexbot",
        "seokicks",
        "serpstat",
        "megaindex",
        "zoominfobot",
        "barkrowler",
        // monitoring / scanning
        "pingdom",
        "uptimerobot",
        "statuscake",
        "site24x7",
        "newrelic",
        "datadog",
        "censys",
        "zgrab",
        "masscan",
        "nmap",
        "expanse",
        "internet-measurement",
        // http clients & tooling
        "curl/",
        "wget",
        "python-requests",
        "python-urllib",
        "aiohttp",
        "httpx",
        "scrapy",
        "java/",
        "okhttp",
        "go-http-client",
        "node-fetch",
        "axios",
        "got (",
        "libwww",
        "lwp-trivial",
        "postmanruntime",
        "insomnia",
        "httpclient",
        "restsharp",
        "guzzlehttp",
        "apache-httpclient",
        // headless browsers
        "headlesschrome",
        "phantomjs",
        "puppeteer",
        "playwright",
        "lighthouse",
        "chrome-lighthouse"
    ];
}