using System.Xml.Linq;
using System.Xml.XPath;
using Regira.System.Projects.Models;

namespace Regira.System.Projects.Services;

public class ProjectParser
{
    public Project Parse(XDocument xml)
    {
        var propertyGroupEl = xml.XPathSelectElement("*/PropertyGroup")!;
        if (propertyGroupEl == null)
        {
            // .Net Framework project...
            propertyGroupEl = xml.Root?.Elements().FirstOrDefault(el => el.Name.LocalName == "PropertyGroup");
            return new Project
            {
                Id = propertyGroupEl?.Elements().FirstOrDefault(el => el.Name.LocalName == "AssemblyName")?.Value,
                RootNamespace = propertyGroupEl?.Elements().FirstOrDefault(el => el.Name.LocalName == "RootNamespace")?.Value,
                AssemblyName = propertyGroupEl?.Elements().FirstOrDefault(el => el.Name.LocalName == "AssemblyName")?.Value,
            };
        }
        Version.TryParse(propertyGroupEl.Element("Version")?.Value, out var version);
        bool.TryParse(propertyGroupEl.Element("GeneratePackageOnBuild")?.Value, out _);

        var project = new Project
        {
            Id = propertyGroupEl.Element("PackageId")?.Value,
            TargetFrameworks = (propertyGroupEl.Elements("TargetFrameworks").FirstOrDefault() ?? propertyGroupEl.Elements("TargetFramework").FirstOrDefault())?.Value.Split(';').ToList() ??
                               [],
            RootNamespace = propertyGroupEl.Element("RootNamespace")?.Value,
            AssemblyName = propertyGroupEl.Element("AssemblyName")?.Value,
            Authors = propertyGroupEl.Elements("Authors").Select(el => el.Value).ToArray(),
            IsClassLibrary = IsClassLibrary(propertyGroupEl),
            Dependencies = xml.Descendants("ProjectReference")
                .Select(el => el.Attribute("Include")?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray()!
        };
        if (version != null)
        {
            project.Version = version;
        }
        return project;
    }
    public XDocument Update(XDocument source, Project item)
    {
        var target = new XDocument(source);

        var propertyGroupEl = target.XPathSelectElement("*/PropertyGroup")!;
        var packageIdEl = propertyGroupEl.Element("PackageId");
        var rootNamespaceEl = propertyGroupEl.Element("RootNamespace");
        var assemblyNameEl = propertyGroupEl.Element("AssemblyName");
        var versionEl = propertyGroupEl.Element("Version");
        var authorsEl = propertyGroupEl.Elements("Authors").FirstOrDefault();
        var generatePackageEl = propertyGroupEl.Element("GeneratePackageOnBuild");
        var implicitUsingsEl = propertyGroupEl.Element("ImplicitUsings");
        var nullableEl = propertyGroupEl.Element("Nullable");
        var langVersionEl = propertyGroupEl.Element("LangVersion");
        var generateDocumentationEl = propertyGroupEl.Element("GenerateDocumentationFile");
        var noWarnEl = propertyGroupEl.Element("NoWarn");

        var isClassLibrary = IsClassLibrary(propertyGroupEl);

        if (item.TargetFrameworks?.Any() == true)
        {
            var targetFrameworkEl = propertyGroupEl.Elements("TargetFramework").FirstOrDefault();
            var targetFrameworksEl = propertyGroupEl.Elements("TargetFrameworks").FirstOrDefault();
            if (item.TargetFrameworks.Count > 1)
            {
                if (targetFrameworksEl == null)
                {
                    targetFrameworksEl = new XElement("TargetFrameworks");
                    propertyGroupEl.Add(targetFrameworksEl);
                }

                if (targetFrameworkEl != null)
                {
                    targetFrameworkEl.Remove();
                }

                targetFrameworksEl.Value = string.Join(";", item.TargetFrameworks);
            }
            else
            {
                if (targetFrameworkEl == null)
                {
                    targetFrameworkEl = new XElement("TargetFramework");
                    propertyGroupEl.Add(targetFrameworkEl);
                }

                if (targetFrameworksEl != null)
                {
                    targetFrameworksEl.Remove();
                }

                targetFrameworkEl.Value = item.TargetFrameworks.First();
            }
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            if (packageIdEl == null)
            {
                packageIdEl = new XElement("PackageId");
                propertyGroupEl.Add(packageIdEl);
            }
            packageIdEl.Value = item.Id;
        }

        if (!string.IsNullOrWhiteSpace(item.RootNamespace))
        {
            if (rootNamespaceEl == null)
            {
                rootNamespaceEl = new XElement("RootNamespace");
                propertyGroupEl.Add(rootNamespaceEl);
            }
            rootNamespaceEl.Value = item.RootNamespace;
        }

        if (!string.IsNullOrWhiteSpace(item.AssemblyName))
        {
            if (assemblyNameEl == null)
            {
                assemblyNameEl = new XElement("AssemblyName");
                propertyGroupEl.Add(assemblyNameEl);
            }
            assemblyNameEl.Value = item.AssemblyName;
        }

        if (versionEl == null)
        {
            versionEl = new XElement("Version");
            propertyGroupEl.Add(versionEl);
        }
        versionEl.Value = item.Version.ToString();

        if (item.Authors?.Any() == true)
        {
            if (authorsEl == null)
            {
                authorsEl = new XElement("Authors");
                propertyGroupEl.Add(authorsEl);
            }
            authorsEl.Value = string.Join(",", item.Authors);
        }

        if (isClassLibrary && generatePackageEl == null)
        {
            generatePackageEl = new XElement("GeneratePackageOnBuild", "true");
            propertyGroupEl.Add(generatePackageEl);
        }

        if (implicitUsingsEl == null)
        {
            propertyGroupEl.Add(new XElement("ImplicitUsings", "enable"));
        }

        if (nullableEl == null)
        {
            propertyGroupEl.Add(new XElement("Nullable", "enable"));
        }

        if (langVersionEl == null)
        {
            propertyGroupEl.Add(new XElement("LangVersion", "latest"));
        }

        if (generateDocumentationEl == null)
        {
            propertyGroupEl.Add(new XElement("GenerateDocumentationFile", "true"));
        }

        if (noWarnEl == null)
        {
            propertyGroupEl.Add(new XElement("NoWarn", "1701;1702;1591"));
        }

        return target;
    }

    public bool IsClassLibrary(XElement propertyGroupEl)
    {
        var outputTypeEl = propertyGroupEl.Element("OutputType");
        var outputType = outputTypeEl?.Value;
        var isPackableEl = propertyGroupEl.Element("IsPackable");
        var isPackable = bool.Parse(isPackableEl?.Value ?? "true");
        var isClassLibrary = isPackable && !"Exe".Equals(outputType, StringComparison.InvariantCultureIgnoreCase);
        return isClassLibrary;
    }
}
