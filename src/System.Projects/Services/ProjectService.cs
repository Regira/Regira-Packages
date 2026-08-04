using System.Xml.Linq;
using Regira.IO.Storage;
using Regira.IO.Storage.Abstractions;
using Regira.System.Projects.Models;

namespace Regira.System.Projects.Services;

public class ProjectService(ProjectParser parser, ITextFileService fileService)
{
    public async Task<Project> Details(string uri)
    {
        var xml = await Load(uri);
        var project = parser.Parse(xml);
        project.ProjectFile = fileService.GetAbsoluteUri(uri);
        project.Title ??= Path.GetFileNameWithoutExtension(project.ProjectFile);
        return project;
    }

    public async Task<IEnumerable<Project>> List()
    {
        var projectUris = await fileService.List(new FileSearchObject
        {
            Recursive = true,
            Type = FileEntryTypes.Files,
            Extensions = [".csproj"]
        });
        var projects = await Task.WhenAll(projectUris.Select(Details));
        return projects;
    }
    public async Task Save(Project item)
    {
        if (string.IsNullOrWhiteSpace(item.ProjectFile))
        {
            throw new NullReferenceException($"{nameof(item.ProjectFile)} is empty");
        }

        var source = await Load(item.ProjectFile);
        var doc = parser.Update(source, item);
        var contents = doc.ToString(SaveOptions.OmitDuplicateNamespaces);
        await fileService.Save(item.ProjectFile, contents);
    }

    protected async Task<XDocument> Load(string uri)
    {
        var content = await fileService.GetContents(uri);
        return XDocument.Parse(content!);
    }
}