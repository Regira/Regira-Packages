namespace Regira.Web.Analytics.Services;

/// <summary>
/// Compiled in rather than packed as a content file: NuGet content does not reach a consuming project
/// the way a ProjectReference's does. Extend or replace via Analytics:BotDetection.
/// </summary>
internal static class BotDetectorDefaults
{
    /// <summary>
    /// Tokens every real browser puts in its user agent. An agent naming none of them is not a browser,
    /// which covers the whole long tail of HTTP clients and one-off crawlers without naming them.
    /// </summary>
    public static readonly string[] BrowserTokens = ["mozilla/", "opera/"];

    /// <summary>"cubot" (a phone brand) would otherwise trip the broad "bot" marker.</summary>
    public static readonly string[] Exceptions = ["cubot"];

    /// <summary>
    /// Targets no visitor of an ASP.NET Core site can have navigated to, matched against path + query.
    /// Kept to what is unambiguous: a scanner also sweeps <c>/admin</c>, <c>/login</c> and <c>/graphql</c>,
    /// but those are somebody's real page, and a host that wants them flagged adds them to
    /// Analytics:BotDetection.
    /// </summary>
    public static readonly string[] ProbePaths =
    [
        // PHP and CMS paths a .NET host never serves
        "/wp-admin",
        "/wp-content",
        "/wp-includes",
        "/wp-json",
        "/wp-login",
        "/wp-links-opml",
        "/xmlrpc.php",
        "/index.php",
        "/app_dev.php",
        "/administrator/index.php",
        "/phpmyadmin",
        "/phpinfo",
        "/cgi-bin",
        "/vendor/",
        // dev-server internals, reachable only from a build tool left running
        "/@fs/",
        "/@vite/",
        "/gradio_api",
        // debug and introspection endpoints of other stacks
        "/actuator",
        "/_profiler",
        "/_debugbar",
        "/_ignition",
        "/telescope/requests",
        "/__clockwork",
        "/__debug__",
        "/debug/pprof",
        "/debug/vars",
        "/debug/default/view",
        "/rails/info",
        "/management/env",
        "/horizon/dashboard",
        "/log-viewer",
        "/error_log",
        "/server-status",
        "/server-info",
        "/nginx_status",
        // build and deployment metadata
        "/dockerfile",
        "/jenkinsfile",
        // keys, credentials and host files
        "aws/credentials",
        "aws_credentials",
        "aws-credentials",
        "/id_rsa",
        "/id_dsa",
        "/id_ecdsa",
        "/id_ed25519",
        "/private-key",
        "/etc/passwd",
        "/proc/self/",
        "/proc/1/"
    ];

    public static readonly string[] Markers =
    [
        // generic
        "bot",
        "crawler",
        "spider",
        "crawl",
        "slurp",
        "scraper",
        "scanner",
        "archiver",
        "fetcher",
        // The "+contact-url" convention: crawlers announce where to complain, browsers never do.
        "+http",
        // search engines / previewers
        "bingpreview",
        "googleother",
        "google-inspectiontool",
        "facebookexternalhit",
        "meta-externalagent",
        "embedly",
        "quora link preview",
        "skypeuripreview",
        "whatsapp",
        "telegrambot",
        "slackbot",
        "discordbot",
        "linkedinbot",
        // AI crawlers — the "-user" family fetches on behalf of a prompt and names no bot
        "gptbot",
        "chatgpt-user",
        "claudebot",
        "claude-web",
        "claude-user",
        "anthropic-ai",
        "perplexity-user",
        "mistralai-user",
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
        "newsai/",
        // SEO / marketing
        "ahrefs",
        "semrush",
        "mj12",
        "dotbot",
        "petalbot",
        "dataforseo",
        "dataprovider",
        "blexbot",
        "seokicks",
        "serpstat",
        "megaindex",
        "zoominfobot",
        "barkrowler",
        // feed readers
        "simplepie",
        "feedparser",
        "feedly",
        "inoreader",
        "newsblur",
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
        // vulnerability scanners that disguise themselves as a browser
        "nikto",
        "sqlmap",
        "nuclei",
        "wpscan",
        "acunetix",
        "netsparker",
        "zaproxy",
        "l9explore",
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
        "selenium",
        "webdriver",
        "lighthouse",
        "chrome-lighthouse"
    ];
}
