using CodeCiir.Application.Projects;
using NSubstitute;

namespace CodeCiir.Application.Tests.Projects.ProjectsServiceTests;

public abstract class BaseProjectsServiceTests
{
    protected IProjectsRepository ProjectsRepository { get; } = Substitute.For<IProjectsRepository>();

    protected ProjectsService Sut => new(ProjectsRepository);
}
