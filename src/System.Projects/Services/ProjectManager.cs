using Regira.System.Projects.Models;

namespace Regira.System.Projects.Services;

public class ProjectManager(ProjectService projectService)
{
    public async Task<ProjectTree> BuildTree()
    {
        var projects = (await projectService.List())
            .Where(x => x.IsClassLibrary == true)
            .ToArray();

        // Project Tree
        return ProjectTree.Load(projects);
    }

    public async Task SaveProjectFiles(IList<Project> projects)
    {
        foreach (var project in projects)
        {
            await projectService.Save(project);
        }
    }
}