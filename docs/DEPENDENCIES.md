# M0 dependency inventory

Restored and inspected 29 October 2026. These packages are private to test projects. Source: resolved NuGet lock files plus the downloaded owner-supplied nuspec license expressions. No production project has a PackageReference.

| Package | Locked version | Kind | Declared license |
|---|---|---|---|
| Microsoft.NET.Test.Sdk | 18.11.0 | Direct | MIT |
| MSTest.TestAdapter | 4.5.2 | Direct | MIT |
| MSTest.TestFramework | 4.5.2 | Direct | MIT |
| Microsoft.ApplicationInsights | 2.23.0 | Transitive | MIT |
| Microsoft.CodeCoverage | 18.11.0 | Transitive | MIT |
| Microsoft.Testing.Extensions.Telemetry | 2.5.2 | Transitive | MIT |
| Microsoft.Testing.Extensions.TrxReport.Abstractions | 2.5.2 | Transitive | MIT |
| Microsoft.Testing.Platform | 2.5.2 | Transitive | MIT |
| Microsoft.Testing.Platform.MSBuild | 2.5.2 | Transitive | MIT |
| Microsoft.TestPlatform.ObjectModel | 18.11.0 | Transitive | MIT |
| Microsoft.TestPlatform.TestHost | 18.11.0 | Transitive | MIT |
| MSTest.Analyzers | 4.5.2 | Transitive | MIT |
| System.Diagnostics.DiagnosticSource | 5.0.0 | Transitive | MIT |

The cached ObjectModel 18.10.1 package is not the final resolved version; the lock resolves 18.11.0. Test-platform telemetry/ApplicationInsights assets are present only in the testing dependency graph, with CLI and testing telemetry explicitly opted out for every invoked build/test process and CI. MTP runner is disabled; .NET 8 uses VSTest. They are absent from the application output. No optional coverage collector is configured; Microsoft.CodeCoverage is transitive from the disclosed test SDK.

CI-only dependencies, verified by read-only git ls-remote against their official repositories:

- actions/checkout v7.0.1: 3d3c42e5aac5ba805825da76410c181273ba90b1 (https://github.com/actions/checkout)
- actions/setup-dotnet v6.0.0: a98b56852c35b8e3190ac28c8c2271da59106c68 (https://github.com/actions/setup-dotnet)
- actions/upload-artifact v7.0.2: cf430e030ddbb5b0abf93d22962f4752f3646cd9 (https://github.com/actions/upload-artifact)

No external workflow has run. GitHub runner image: windows-2022; official image inventory: https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md.
