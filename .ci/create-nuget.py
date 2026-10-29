# coding: utf-8
"""Script for generating files with NuGet package metadata."""

import datetime
import sys
from pathlib import Path
from shutil import copyfile

if __name__ == "__main__":
    source = Path(sys.argv[1])
    nuget_dir = Path(__file__).absolute().parent / "nuget"
    print(f"Creating nuget directory '{nuget_dir}'")
    build_folder_path = nuget_dir / "build"
    build_folder_path.mkdir(parents=True, exist_ok=True)
    print(f"Looking for libraries in '{source}'")
    runtime_assets = (
        ("linux-amd64-liblightgbm", "linux-x64", "lib_lightgbm.so"),
        ("linux-aarch64-liblightgbm", "linux-arm64", "lib_lightgbm.so"),
        ("macosx-amd64-liblightgbm", "osx-x64", "lib_lightgbm.dylib"),
        ("macosx-arm64-liblightgbm", "osx-arm64", "lib_lightgbm.dylib"),
        (
            "windows-amd64-cli-and-liblightgbm",
            "win-x64",
            "lib_lightgbm.dll",
            "lightgbm.exe",
        ),
        (
            "windows-arm64-cli-and-liblightgbm",
            "win-arm64",
            "lib_lightgbm.dll",
            "lightgbm.exe",
        ),
    )
    for artifact, runtime, *libraries in runtime_assets:
        native_folder = nuget_dir / "runtimes" / runtime / "native"
        native_folder.mkdir(parents=True, exist_ok=True)
        for library in libraries:
            copyfile(source / artifact / library, native_folder / library)
    version = (nuget_dir.parents[1] / "VERSION.txt").read_text(encoding="utf-8").strip().replace("rc", "-rc")
    print(f"Setting version to '{version}'")
    nuget_str = rf"""<?xml version="1.0"?>
    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
    <metadata>
        <id>LightGBM</id>
        <version>{version}</version>
        <authors>Guolin Ke</authors>
        <owners>Guolin Ke</owners>
        <license type="expression">MIT</license>
        <projectUrl>https://github.com/lightgbm-org/LightGBM</projectUrl>
        <requireLicenseAcceptance>false</requireLicenseAcceptance>
        <description>A fast, distributed, high performance gradient boosting framework</description>
        <copyright>Copyright {datetime.datetime.now().year} @ Microsoft</copyright>
        <tags>machine-learning data-mining distributed native boosting gbdt</tags>
        <dependencies> </dependencies>
    </metadata>
        <files>
        <file src="build\**" target="build"/>
        <file src="runtimes\**" target="runtimes"/>
        </files>
    </package>
    """
    prop_str = r"""
    <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <ItemGroup Condition="Exists('packages.config') OR
                            Exists('$(MSBuildProjectName).packages.config') OR
                            Exists('packages.$(MSBuildProjectName).config')">
        <Content Include="$(MSBuildThisFileDirectory)/../runtimes/win-x64/native/*.dll"
                Condition="'$(PlatformTarget)' == 'x64'">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Visible>false</Visible>
        </Content>
        <Content Include="$(MSBuildThisFileDirectory)/../runtimes/win-x64/native/*.exe"
                Condition="'$(PlatformTarget)' == 'x64'">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Visible>false</Visible>
        </Content>
        <Content Include="$(MSBuildThisFileDirectory)/../runtimes/win-arm64/native/*.dll"
                Condition="'$(PlatformTarget)' == 'ARM64'">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Visible>false</Visible>
        </Content>
        <Content Include="$(MSBuildThisFileDirectory)/../runtimes/win-arm64/native/*.exe"
                Condition="'$(PlatformTarget)' == 'ARM64'">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        <Visible>false</Visible>
        </Content>
    </ItemGroup>
    </Project>
    """
    target_str = r"""
    <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <EnableLightGBMUnsupportedPlatformTargetCheck Condition="'$(EnableLightGBMUnsupportedPlatformTargetCheck)' == ''">true</EnableLightGBMUnsupportedPlatformTargetCheck>
    </PropertyGroup>
    <Target Name="_LightGBMCheckForUnsupportedPlatformTarget"
            Condition="'$(EnableLightGBMUnsupportedPlatformTargetCheck)' == 'true'"
            AfterTargets="_CheckForInvalidConfigurationAndPlatform">
        <Error Condition="'$(PlatformTarget)' != 'x64' AND '$(PlatformTarget)' != 'ARM64' AND
                        ('$(OutputType)' == 'Exe' OR '$(OutputType)'=='WinExe') AND
                        !('$(TargetFrameworkIdentifier)' == '.NETCoreApp' AND '$(PlatformTarget)' == '')"
            Text="LightGBM currently supports 'x64' and 'ARM64' processor architectures. Please ensure your application is targeting a supported processor architecture." />
    </Target>
    </Project>
    """
    (nuget_dir / "LightGBM.nuspec").write_text(nuget_str, encoding="utf-8")
    (nuget_dir / "build" / "LightGBM.props").write_text(prop_str, encoding="utf-8")
    (nuget_dir / "build" / "LightGBM.targets").write_text(target_str, encoding="utf-8")
    print("Done creating NuGet package")
