# Console App with xUnit v3 Tests Template

[![NuGet Version](https://img.shields.io/nuget/v/console-with-tests)](https://www.nuget.org/packages/console-with-tests/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/console-with-tests)](https://www.nuget.org/packages/console-with-tests/)
[![License](https://img.shields.io/github/license/mrviduus/ConsoleWithTests.Template)](LICENSE)
[![CI/CD](https://github.com/mrviduus/ConsoleWithTests.Template/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/mrviduus/ConsoleWithTests.Template/actions)
[![.NET](https://img.shields.io/badge/.NET-10%20%7C%209%20%7C%208-512BD4)](https://dotnet.microsoft.com/)

A production-ready .NET template that creates a console application with xUnit v3 test project structure, following best practices and clean architecture.

## Features

- ✅ Console application with sample code
- ✅ xUnit v3 test project with examples
- ✅ Solution file linking both projects
- ✅ Microsoft Testing Platform integration
- ✅ Sample tests demonstrating Fact and Theory patterns
- ✅ Support for .NET 10 (default), .NET 9, and .NET 8

## Installation

```bash
dotnet new install console-with-tests
```

## Usage

### Create a New Project

```bash
dotnet new console-with-tests -n MyProject
```

This creates:

```
MyProject/
  MyProject.sln                 # Solution file
  src/
    App/
      App.csproj               # Console application
      Program.cs               # Main program with sample Add method
  tests/
    App.Tests/
      App.Tests.csproj         # xUnit v3 test project
      UnitTest1.cs             # Sample unit tests
      xunit.runner.json        # xUnit configuration
```

### Framework Options

```bash
# .NET 10 (default)
dotnet new console-with-tests -n MyProject

# .NET 9
dotnet new console-with-tests -n MyProject --Framework net9.0

# .NET 8
dotnet new console-with-tests -n MyProject --Framework net8.0
```

### Build and Test

```bash
cd MyProject
dotnet build
dotnet test
```

## Requirements

- .NET SDK 8.0 or later
- .NET 10.0 SDK for net10.0 projects (default)

## Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## Security

Found a security issue? See [SECURITY.md](SECURITY.md) for reporting procedures.

## License

MIT License - see [LICENSE](LICENSE) for details.

## Support

- 🐛 [Report Issues](https://github.com/mrviduus/ConsoleWithTests.Template/issues)
- 💬 [Discussions](https://github.com/mrviduus/ConsoleWithTests.Template/discussions)
- 📖 [Wiki](https://github.com/mrviduus/ConsoleWithTests.Template/wiki)

---

<div align="center">
  Made with ❤️ for the .NET Community
  <br>
  Star ⭐ this repository if you find it helpful!
</div>